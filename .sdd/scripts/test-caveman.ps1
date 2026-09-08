[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),[switch]$Published,[switch]$MigrationAudit)
. (Join-Path $PSScriptRoot 'Extensions.ps1')
. (Join-Path $PSScriptRoot 'Instruction-References.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
$checks=[Collections.Generic.List[object]]::new()
function Assert([string]$Name,[bool]$Passed,[string]$Evidence) {
    $checks.Add([ordered]@{ name=$Name; passed=$Passed; evidence=$Evidence })
    Write-Output "[$(if($Passed){'PASS'}else{'FAIL'})] $Name"
}
$original=@{}
$archive=Join-Path $root '.sdd/baseline/caveman/phase5.zip'
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -notmatch '\.(md|json|ps1|yml|yaml|toml)$') { continue }
        $reader=[IO.StreamReader]::new($entry.Open(),[Text.Encoding]::UTF8)
        try { $original[$entry.FullName]=$reader.ReadToEnd().Replace("`r`n","`n") } finally { $reader.Dispose() }
    }
} finally { $zip.Dispose() }
$catalog=Get-SddCatalog $root
$oldCatalog=$original['.sdd/catalog.json'] | ConvertFrom-Json -AsHashtable
$current=@($catalog.skills)+@($catalog.roles); $old=@($oldCatalog.skills)+@($oldCatalog.roles)
$unchanged=$true
foreach ($item in $old) {
    $candidate=@($current | Where-Object { $_.id -ceq $item.id })
    if ($candidate.Count -ne 1) { $unchanged=$false; continue }
    foreach ($key in $item.Keys) { if ($key -ne 'description' -and $candidate[0][$key] -cne $item[$key]) { $unchanged=$false } }
}
Assert 'Workflow identities, invocation settings, aliases, and role ownership preserved' $unchanged 'Compare every catalog field except description against phase 5.'
$source=Get-Content (Join-Path $root '.sdd/adapters/caveman/source.json') -Raw | ConvertFrom-Json
$snapshot=Join-Path $root ('.sdd/adapters/caveman/'+$source.snapshot)
Assert 'Caveman source is content pinned' ((Get-FileHash $snapshot).Hash.ToLowerInvariant() -ceq $source.sha256) 'Upstream snapshot SHA-256 matches recorded source; no startup download.'
$metrics=@()
foreach ($oldItem in $old) {
    $item=$current | Where-Object { $_.id -ceq $oldItem.id }
    if (-not $item) { continue }
    $before=$original[$item.source]
    $after=Read-Utf8 (Join-Path $root $item.source)
    $metrics += [pscustomobject][ordered]@{ id=$item.id; descriptionBeforeBytes=[Text.Encoding]::UTF8.GetByteCount($oldItem.description); descriptionAfterBytes=[Text.Encoding]::UTF8.GetByteCount($item.description); entryBeforeBytes=[Text.Encoding]::UTF8.GetByteCount($before); entryAfterBytes=[Text.Encoding]::UTF8.GetByteCount($after) }
}
$beforeDescriptions=($metrics | Measure-Object descriptionBeforeBytes -Sum).Sum
$afterDescriptions=($metrics | Measure-Object descriptionAfterBytes -Sum).Sum
Assert 'Discovery descriptions shrink while remaining distinct' ($afterDescriptions -lt $beforeDescriptions -and @($current.description | Select-Object -Unique).Count -eq $current.Count) "$beforeDescriptions to $afterDescriptions UTF-8 bytes; bytes are not tokens."
# Exact command/API examples and executable verification scripts are protected from compression.
$lost=@()
foreach ($item in $old) {
    $candidate=(Get-InstructionClosure -RepositoryRoot $root -Source $item.source).text
    foreach ($block in [regex]::Matches($original[$item.source],'(?ms)^```(?:csharp|cs|sql|json|yaml|toml|bash|powershell)\s*\n.*?^```')) {
        # Graph construction moved to a nested reference with explicit native-renderable paths.
        $literal=$block.Value
        if ($item.id -eq 'graphify-windows') { $literal=$literal.Replace('references/','.sdd/skills/graphify-windows/references/') }
        if (-not $candidate.Contains($literal)) { $lost += $item.id; break }
    }
}
Assert 'Executable examples and literal API blocks survive refactoring' ($lost.Count -eq 0) ($lost -join ', ')
$changedGates=@('check-design-rules.ps1','format-changes.ps1') | Where-Object { (Read-Utf8 (Join-Path $root ".sdd/scripts/$_")) -cne $original[".sdd/scripts/$_"] }
# Phase 10 deliberately adds evidence dispatch. Preserve every original gate function exactly;
# validate new behavior through test-evidence.ps1 instead of freezing whole script forever.
$tokens=$null; $parseErrors=$null
$oldGate=[Management.Automation.Language.Parser]::ParseInput($original['.sdd/scripts/check-sdd-gates.ps1'],[ref]$tokens,[ref]$parseErrors)
$newGate=[Management.Automation.Language.Parser]::ParseInput((Read-Utf8 (Join-Path $root '.sdd/scripts/check-sdd-gates.ps1')),[ref]$tokens,[ref]$parseErrors)
$oldFunctions=@($oldGate.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$true))
$newFunctions=@($newGate.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$true))
$changedFunctions=@(foreach($function in $oldFunctions){$current=@($newFunctions|Where-Object Name -CEQ $function.Name);if($current.Count -ne 1 -or $current[0].Extent.Text -cne $function.Extent.Text){$function.Name}})
Assert 'Existing gate functions, design checks, and formatter preserved' (@($changedGates).Count -eq 0 -and $changedFunctions.Count -eq 0 -and $parseErrors.Count -eq 0) (($changedGates+$changedFunctions) -join ', ')
$templateChanges=@(foreach($path in $original.Keys | Where-Object {$_ -match '^\.sdd/skills/.+/templates/'}) {
    $candidate=Read-Utf8 (Join-Path $root $path)
    $beforeHeadings=@([regex]::Matches($original[$path],'(?m)^## (?:\d+\.|Assumptions & Open Questions)[^\r\n]*') | ForEach-Object Value)
    $afterHeadings=@([regex]::Matches($candidate,'(?m)^## (?:\d+\.|Assumptions & Open Questions)[^\r\n]*') | ForEach-Object Value)
    if (($beforeHeadings -join "`n") -cne ($afterHeadings -join "`n")) {$path}
})
Assert 'Artifact templates preserve mandatory numbered sections and appendix' ($templateChanges.Count -eq 0) ($templateChanges -join ', ')
# Historical prose/report examples are not live policy. Release correctness fixes deliberately
# change template guidance and observed-result placeholders; live gates validate saved artifacts.
$work=Resolve-WorkspacePath $root ('.sdd/.test-work/caveman-static-'+[Guid]::NewGuid().ToString('N'))
$generation=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-OutputRoot',$work) $root
Assert 'Both runtime adapters generate successfully' ($generation.exitCode -eq 0) ($generation.stdout+$generation.stderr)
if ($generation.exitCode -eq 0) {
    $unresolved=@()
    foreach ($directory in @('.claude/skills','.agents/skills')) {
        foreach ($file in Get-ChildItem (Join-Path $work $directory) -Recurse -File -Filter '*.md' | Where-Object { $_.FullName -notmatch '[\\/]upstream[\\/]' }) {
            $text=Read-Utf8 $file.FullName
            if ($text -match '\{\{(?:command|tool):') { $unresolved += $file.FullName }
            foreach ($match in [regex]::Matches($text,'(?:\.claude|\.agents)/skills/[a-zA-Z0-9_./-]+\.(?:md|json)')) {
                if (-not (Test-Path -LiteralPath (Join-Path $work $match.Value))) { $unresolved += $match.Value }
            }
        }
    }
    Assert 'Rendered reference paths exist and contain no runtime macros' ($unresolved.Count -eq 0) (($unresolved | Select-Object -Unique) -join ', ')
    $entrypoints=@()
    foreach ($skill in $catalog.skills) { $entrypoints += ".claude/skills/$($skill.id)/SKILL.md"; $entrypoints += ".agents/skills/$($skill.id)/SKILL.md" }
    foreach ($role in $catalog.roles) { $entrypoints += ".claude/agents/$($role.id).md"; $entrypoints += ".codex/agents/$($role.id).toml" }
    $missing=@($entrypoints | Where-Object { $entry=Read-Utf8 (Join-Path $work $_); -not $entry.Contains('.sdd/contracts/execution.md') -or -not $entry.Contains('.sdd/contracts/communication.md') })
    $execution=Read-Utf8 (Join-Path $root '.sdd/contracts/execution.md')
    $project=Read-Utf8 (Join-Path $work 'AGENTS.md')
    Assert 'All workflows and workers reach shared communication policy' ($missing.Count -eq 0 -and $execution.Contains('.sdd/contracts/communication.md') -and $project.Contains('.sdd/contracts/communication.md')) "$($entrypoints.Count) native entry points explicitly load policy; project guidance covers ordinary chat."
}
if ($MigrationAudit) {
    $preservation=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/caveman-checkpoint.ps1'),'-Action','Check') $root
    Assert 'Phase 5 checkpoint and application artifacts preserved' ($preservation.exitCode -eq 0) ($preservation.stdout+$preservation.stderr)
    # Restore real candidate bytes only in an isolated fixture, including an independent-edit rejection.
    $rollbackRoot=Resolve-WorkspacePath $work 'rollback'
    $inventory=Get-Content (Join-Path $root '.sdd/baseline/caveman/inventory.json') -Raw | ConvertFrom-Json -AsHashtable
    $copyPaths=@($inventory.files.path)
    foreach ($directory in @('contracts','skills','roles','adapters','scripts','evals')) {
        $absolute=Join-Path $root ".sdd/$directory"
        if (Test-Path $absolute) { $copyPaths += Get-ChildItem $absolute -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/') } }
    }
    foreach ($path in @($copyPaths | Sort-Object -Unique)) {
        $from=Resolve-WorkspacePath $root $path
        $to=Resolve-WorkspacePath $rollbackRoot $path
        if (Test-Path -LiteralPath $from) { [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null; Copy-Item -LiteralPath $from -Destination $to }
    }
    $fixtureGeneration=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-OutputRoot',$rollbackRoot) $root
    if ($fixtureGeneration.exitCode -ne 0) { throw $fixtureGeneration.stderr }
    foreach ($file in @('inventory.json','phase5.zip')) {
        $to=Resolve-WorkspacePath $rollbackRoot ".sdd/baseline/caveman/$file"
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null
        Copy-Item -LiteralPath (Join-Path $root ".sdd/baseline/caveman/$file") -Destination $to
    }
    $checkpointScript=Join-Path $root '.sdd/scripts/caveman-checkpoint.ps1'
    $seal=Invoke-Captured $pwsh @('-NoProfile','-File',$checkpointScript,'-RepositoryRoot',$rollbackRoot,'-Action','Seal') $root
    if ($seal.exitCode -ne 0) { throw $seal.stderr }
    $edited=Join-Path $rollbackRoot '.sdd/contracts/communication.md'; $saved=Read-Utf8 $edited
    Write-Utf8 $edited ($saved+"`nIndependent user edit.`n")
    $before=Get-Digest (Read-Utf8 (Join-Path $rollbackRoot 'AGENTS.md'))
    $rejected=Invoke-Captured $pwsh @('-NoProfile','-File',$checkpointScript,'-RepositoryRoot',$rollbackRoot,'-Action','Restore','-Apply') $root
    Assert 'Caveman rollback rejects independent edits before writes' ($rejected.exitCode -ne 0 -and (Read-Utf8 $edited).Contains('Independent user edit.') -and $before -ceq (Get-Digest (Read-Utf8 (Join-Path $rollbackRoot 'AGENTS.md')))) ($rejected.stdout+$rejected.stderr)
    Write-Utf8 $edited $saved
    Write-Utf8 (Join-Path $rollbackRoot 'unrelated.txt') 'Keep this file.'
    $preview=Invoke-Captured $pwsh @('-NoProfile','-File',$checkpointScript,'-RepositoryRoot',$rollbackRoot,'-Action','Restore') $root
    Assert 'Caveman rollback previews without mutation' ($preview.exitCode -eq 0 -and (Test-Path $edited)) $preview.stdout
    $restore=Invoke-Captured $pwsh @('-NoProfile','-File',$checkpointScript,'-RepositoryRoot',$rollbackRoot,'-Action','Restore','-Apply') $root
    $different=@($inventory.files | Where-Object { $file=Join-Path $rollbackRoot $_.path; -not (Test-Path $file) -or (Get-FileHash $file).Hash.ToLowerInvariant() -cne $_.sha256 })
    Assert 'Caveman rollback restores phase 5 bytes and preserves unrelated files' ($restore.exitCode -eq 0 -and $different.Count -eq 0 -and -not (Test-Path $edited) -and (Test-Path (Join-Path $rollbackRoot 'unrelated.txt'))) ($restore.stdout+$restore.stderr)
}
if ($Published) {
    $publication=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-Check') $root
    Assert 'Published adapters match candidate' ($publication.exitCode -eq 0) ($publication.stdout+$publication.stderr)
}
$failed=@($checks | Where-Object { -not $_.passed })
Write-Json (Join-Path $root '.sdd/reports/caveman-static.json') ([ordered]@{ testedUtc=[DateTime]::UtcNow.ToString('o'); passed=$failed.Count -eq 0; checks=@($checks.ToArray()); metrics=$metrics; measurement='UTF-8 byte counts, not tokenizer or billing measurements. Native usage recorded separately.' })
Write-Output "[caveman] $($checks.Count-$failed.Count)/$($checks.Count) checks passed."
if ($failed.Count) { exit 1 }
