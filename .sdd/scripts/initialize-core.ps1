# One-time extraction from the immutable migration archive. Re-running never overwrites shared sources.
[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root = [IO.Path]::GetFullPath($RepositoryRoot)
if (Test-Path (Join-Path $root '.sdd/catalog.json')) { throw 'Shared core already exists.' }
$inventory = Get-Content (Join-Path $root '.sdd/baseline/inventory.json') -Raw | ConvertFrom-Json -AsHashtable
$archive = Join-Path $root '.sdd/baseline/originals.zip'
if ((Get-FileHash $archive).Hash.ToLowerInvariant() -ne $inventory.archiveSha256) { throw 'Baseline archive hash mismatch.' }
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
$originals = @{}
try {
    foreach ($entry in $zip.Entries) {
        $reader = [IO.StreamReader]::new($entry.Open(),[Text.Encoding]::UTF8)
        try { $originals[$entry.FullName] = $reader.ReadToEnd().Replace("`r`n","`n") } finally { $reader.Dispose() }
    }
} finally { $zip.Dispose() }
$skills = @()
$roles = @()
$claude = [ordered]@{ skills=[ordered]@{}; roles=[ordered]@{}; tools=[ordered]@{} }
$pathMap = [ordered]@{}
$nameMap = [ordered]@{}
foreach ($path in ($originals.Keys | Sort-Object)) {
    if ($path -match '^\.claude/skills/([^/]+)/SKILL\.md$') {
        $oldName = $Matches[1]
        $id = if ($oldName -eq 'graphify') { 'graphify-windows' } else { $oldName.Replace('.','-') }
        $skills += @{ id=$id; original=$path; source=".sdd/skills/$id/SKILL.md"; legacyKind='skill'; legacyName=$oldName }
        $pathMap[".claude/skills/$oldName/"] = ".sdd/skills/$id/"
        if ($oldName -ne 'graphify') { $nameMap[$oldName] = $id }
    } elseif ($path -match '^\.claude/commands/(theshop\.[^/]+)\.md$') {
        $oldName = $Matches[1]
        $id = $oldName.Replace('.','-')
        $skills += @{ id=$id; original=$path; source=".sdd/skills/$id/SKILL.md"; legacyKind='command'; legacyName=$oldName }
        $nameMap[$oldName] = $id
    } elseif ($path -match '^\.claude/agents/([^/]+)\.md$') {
        $roles += @{ id=$Matches[1]; original=$path; source=".sdd/roles/$($Matches[1]).md" }
    }
}
$pathMap['.claude/scripts/'] = '.sdd/scripts/'
$pathMap['.claude/agents/'] = '.sdd/roles/'
$rewrites = @()
function Convert-Portable([string]$Text) {
    foreach ($old in $pathMap.Keys) { $Text = $Text.Replace($old,$pathMap[$old]) }
    foreach ($old in ($nameMap.Keys | Sort-Object Length -Descending)) {
        $Text = $Text.Replace('/' + $old, '{{command:' + $nameMap[$old] + '}}')
        $Text = $Text.Replace($old,$nameMap[$old])
    }
    $Text = $Text.Replace('$ARGUMENTS','{arguments}')
    $Text = $Text.Replace('Task tool','delegation capability').Replace('`Task` calls','delegation calls').Replace('subagent_type','role')
    $Text = $Text.Replace('`role="general-purpose"`','`role="general-worker"`').Replace('role="general-purpose"','role="general-worker"')
    $Text = $Text.Replace('Do NOT use `Explore` - it is read-only','Do NOT use a read-only explorer - it is read-only')
    $Text = $Text.Replace('General-purpose has Write and Bash access','The general worker needs file-write and shell capabilities')
    $Text = [regex]::Replace($Text,'mcp__([A-Za-z0-9_-]+)__([A-Za-z0-9_]+)',{
        param($m)
        $key = $m.Groups[1].Value + '.' + $m.Groups[2].Value
        $claude.tools[$key] = $m.Value
        return '{{tool:' + $key + '}}'
    })
    $Text = $Text.Replace('If `CLAUDE.md` is present, it''s already in context (loaded automatically by Claude Code).','Read the shared project instructions in `AGENTS.md` if they are not already in context.')
    $Text = $Text.Replace("If the user has selected the highest-capability model (Opus) and enabled extended thinking, you're already in the right setup. If not, the plan will still get written — just less thoroughly. Don't compromise on rigor regardless.", 'Use the model and reasoning settings authorized in the current runtime. Preserve planning rigor regardless of provider.')
    $Text = $Text.Replace('A Stop hook (configured separately in `.claude/settings.json`) runs `dotnet format` after your turn ends.', 'Run `.sdd/scripts/format-changes.ps1` before final verification; repeat the final build if formatting changes code.')
    $Text = $Text.Replace('## Phase 4 — Format (runs automatically after your turn)', '## Phase 4 — Format and final verification')
    $Text = $Text.Replace('You do not run `dotnet format` yourself. A `Stop` hook configured in `.claude/settings.json` runs `dotnet format` once your turn ends. Mention this in the final output so the user understands why a formatting commit may appear.', 'Run `pwsh -NoProfile -File .sdd/scripts/format-changes.ps1`, then repeat the solution build and any verification affected by formatting. A failed formatter or verification blocks completion. Report the observed result.')
    $Text = $Text.Replace(', no manual `dotnet format` (Stop hook handles it)', '. Run `.sdd/scripts/format-changes.ps1` before final verification')
    $Text = $Text.Replace('| 4. Format (Stop hook) | `dotnet format` | will run when this turn ends |','| 4. Format | `dotnet format` | {observed formatter and post-format build result} |')
    $Text = $Text.Replace('| Format (Stop hook) | runs when this turn ends |','| Format | {observed formatter and post-format build result} |')
    $Text = $Text.Replace('`dotnet format` will run automatically when this turn ends (Stop hook).','Formatting and post-format verification must pass before reporting completion.')
    $Text = $Text.Replace('`dotnet format` runs automatically (Stop hook).','Formatting and post-format verification must pass before reporting completion.')
    # Runtime-neutral names do not claim to be callable native tools.
    return $Text
}
foreach ($definition in @($skills) + @($roles)) {
    $text = $originals[$definition.original]
    $match = [regex]::Match($text,'\A---\n(?<header>.*?)\n---\n(?<body>.*)\z','Singleline')
    if (-not $match.Success) { throw "Cannot parse $($definition.original)" }
    $fields = [ordered]@{}
    foreach ($line in ($match.Groups['header'].Value -split "`n")) {
        if ($line -match '^([\w-]+):\s*(.*)$') { $fields[$Matches[1]] = $Matches[2] }
        elseif ($line.Trim()) { throw "Unsupported frontmatter in $($definition.original)" }
    }
    $description = $fields.description
    if ($description.StartsWith('"')) { $description = $description | ConvertFrom-Json }
    $body = Convert-Portable $match.Groups['body'].Value.TrimStart("`n")
    $definition.description = Convert-Portable $description
    if ($definition.ContainsKey('legacyKind')) {
        $definition.explicitOnly = ($fields.Contains('disable-model-invocation') -and $fields['disable-model-invocation'] -eq 'true') -or $definition.id -in @('theshop-start','theshop-ship','theshop-document')
        $claude.skills[$definition.id] = $fields
        $header = "---`nname: $($definition.id)`ndescription: $($definition.description | ConvertTo-Json -Compress)`n---`n`n"
        Write-Utf8 (Join-Path $root $definition.source) ($header + $body)
    } else {
        $claude.roles[$definition.id] = $fields
        $definition.readOnly = $definition.id -in @('shop-code-quality-review','shop-code-security-reviewer')
        Write-Utf8 (Join-Path $root $definition.source) $body
    }
    $rewrites += @{ original=$definition.original; source=$definition.source; originalBodySha256=(Get-Digest $match.Groups['body'].Value.TrimStart("`n")); portableBodySha256=(Get-Digest $body) }
}
foreach ($path in ($originals.Keys | Sort-Object)) {
    if ($path -match '^\.claude/skills/[^/]+/(?!SKILL\.md$).+') {
        $target = $path
        foreach ($old in $pathMap.Keys) { $target = $target.Replace($old,$pathMap[$old]) }
        Write-Utf8 (Join-Path $root $target) (Convert-Portable $originals[$path])
    }
}
Write-Utf8 (Join-Path $root '.sdd/contracts/project.md') (Convert-Portable $originals['CLAUDE.md'])
Write-Utf8 (Join-Path $root '.sdd/scripts/check-sdd-gates.ps1') (Convert-Portable $originals['.claude/scripts/check-sdd-gates.ps1'])
$design = Convert-Portable $originals['.claude/scripts/check-design-rules.ps1']
$design = $design.Substring(0,$design.IndexOf('# ---- Hook mode ----')) + "throw 'Path mode requires -Path. Runtime hooks must decode their own input and pass explicit paths.'`n"
Write-Utf8 (Join-Path $root '.sdd/scripts/check-design-rules.ps1') $design
Write-Utf8 (Join-Path $root '.sdd/adapters/claude/settings.json') $originals['.claude/settings.json']
Write-Json (Join-Path $root '.sdd/catalog.json') ([ordered]@{ schemaVersion=1; skills=@($skills | Sort-Object id); roles=@($roles | Sort-Object id) })
Write-Json (Join-Path $root '.sdd/adapters/claude/definitions.json') $claude
Write-Json (Join-Path $root '.sdd/baseline/extraction.json') $rewrites
Write-Output "Extracted $($skills.Count) skills, $($roles.Count) roles, references, and gates."
