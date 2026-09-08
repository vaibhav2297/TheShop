[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'), [switch]$Published, [switch]$MigrationAudit)
. (Join-Path $PSScriptRoot 'Extensions.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
$work=Join-Path $root ('.sdd/.test-work/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
$results=[Collections.Generic.List[object]]::new()
function Assert-Check([string]$Name,[bool]$Condition,[string]$Evidence) {
    $results.Add([ordered]@{ name=$Name; passed=$Condition; evidence=$Evidence })
    Write-Output "[$(if($Condition){'PASS'}else{'FAIL'})] $Name"
}
function Run-Script([string]$Script,[string[]]$Arguments,[string]$WorkingDirectory=$root) {
    return Invoke-Captured $pwsh (@('-NoProfile','-File',$Script) + $Arguments) $WorkingDirectory
}
$catalog=Get-SddCatalog $root
$baseline=Get-Content (Join-Path $root '.sdd/baseline/inventory.json') -Raw | ConvertFrom-Json -AsHashtable
$generation=Run-Script (Join-Path $root '.sdd/scripts/sync-adapters.ps1') @('-RepositoryRoot',$root,'-OutputRoot',$work)
Assert-Check 'Generate both adapters in isolation' ($generation.exitCode -eq 0) ($generation.stdout+$generation.stderr)
if ($generation.exitCode -ne 0) { throw $generation.stderr }
$first=Get-Content (Join-Path $work '.sdd/generated-files.json') -Raw
$second=Run-Script (Join-Path $root '.sdd/scripts/sync-adapters.ps1') @('-RepositoryRoot',$root,'-OutputRoot',$work)
Assert-Check 'Generation is idempotent' ($second.exitCode -eq 0 -and $first -ceq (Get-Content (Join-Path $work '.sdd/generated-files.json') -Raw) -and $second.stdout -match 'changed 0\.') $second.stdout
$check=Run-Script (Join-Path $root '.sdd/scripts/sync-adapters.ps1') @('-RepositoryRoot',$root,'-OutputRoot',$work,'-Check')
Assert-Check 'Generated output passes drift check' ($check.exitCode -eq 0) $check.stdout

$allValid=$true
$details=@()
foreach ($skill in $catalog.skills) {
    foreach ($base in @('.claude/skills','.agents/skills')) {
        $file=Join-Path $work "$base/$($skill.id)/SKILL.md"
        $definition=Read-Definition $file
        $name=$definition.Fields.name
        $description=$definition.Fields.description | ConvertFrom-Json
        $valid=$name -ceq $skill.id -and $name -match '^[a-z0-9]+(-[a-z0-9]+)*$' -and $name.Length -le 64 -and $description.Length -gt 0 -and $description.Length -le 1024
        $valid=$valid -and $definition.Body.Contains($skill.source) -and $definition.Body.Contains('.sdd/contracts/execution.md')
        if (-not $valid) { $allValid=$false; $details += "$base/$name (description length $($description.Length))" }
        $nativeOnly=$definition.Fields.Contains('disable-model-invocation')
        if ($base -eq '.claude/skills' -and $nativeOnly -ne $skill.explicitOnly) { $allValid=$false; $details += "${name}: Claude invocation policy mismatch" }
        if ($base -eq '.agents/skills') {
            $policy=Read-Utf8 (Join-Path $work "$base/$($skill.id)/agents/openai.yaml")
            $expectedPolicy=if($skill.explicitOnly){'false'}else{'true'}
            if ($policy -notmatch "allow_implicit_invocation: $expectedPolicy") { $allValid=$false; $details += "${name}: Codex invocation policy mismatch" }
            if ($definition.Body -match '\$ARGUMENTS|mcp__|Task tool|subagent_type|\{\{(command|tool):') { $allValid=$false; $details += "${name}: unresolved runtime syntax" }
        }
    }
}
Assert-Check 'Skill discovery metadata and invocation policy' $allValid ($details -join '; ')

$rolesValid=$true
$roleDetails=@()
foreach ($role in $catalog.roles) {
    $native=Read-Utf8 (Join-Path $work ".codex/agents/$($role.id).toml")
    # Generated TOML uses basic strings with JSON-compatible escaping; parse the payload independently.
    $fields=@{}
    foreach ($line in ($native -split "`n")) {
        if ($line -match '^(name|description|developer_instructions|sandbox_mode) = (.+)$') { $fields[$Matches[1]]=$Matches[2] | ConvertFrom-Json }
    }
    if (-not $fields.ContainsKey('developer_instructions') -or $fields.name -cne $role.id -or -not $fields.developer_instructions.Contains($role.source)) { $rolesValid=$false; $roleDetails += $role.id }
    if ($role.readOnly -and $fields.sandbox_mode -ne 'read-only') { $rolesValid=$false; $roleDetails += "$($role.id): missing read-only sandbox" }
    if ($fields.developer_instructions -match 'mcp__|Task tool|subagent_type|\{\{(command|tool):') { $rolesValid=$false; $roleDetails += "$($role.id): unresolved runtime syntax" }
}
Assert-Check 'All specialist roles have usable native definitions' $rolesValid ($roleDetails -join '; ')

$template=Read-Utf8 (Join-Path $work '.agents/skills/theshop-spec/templates/spec-template.md')
$specSkill=Read-Utf8 (Join-Path $work '.agents/skills/theshop-spec/SKILL.md')
Assert-Check 'Native spec references use rendered templates' ($specSkill.Contains('.agents/skills/theshop-spec/templates/spec-template.md') -and $template -notmatch '\{\{(command|tool):') 'The normal spec entry point cannot copy raw generator tokens into feature artifacts.'
$scriptTokens=@(Get-ChildItem (Join-Path $root '.sdd/scripts') -File -Filter '*.ps1' | Where-Object { $_.Name -in @('check-sdd-gates.ps1','check-design-rules.ps1','format-changes.ps1') } | Where-Object { (Read-Utf8 $_.FullName) -match '\{\{(command|tool):' })
Assert-Check 'Executable gates contain no generator tokens' ($scriptTokens.Count -eq 0) (($scriptTokens | ForEach-Object Name) -join ', ')
$contract=Read-Utf8 (Join-Path $root '.sdd/contracts/execution.md')
$settings=Read-Utf8 (Join-Path $work '.claude/settings.json')
Assert-Check 'Formatting waits for workers and design checks are explicit' ($contract.Contains('Workers never run the whole-diff formatter') -and $contract.Contains('required even when no native hook runs') -and -not $settings.Contains('format-on-stop.ps1')) 'Both adapters require explicit finalization; Claude edit hooks cannot format a sibling worker\u0027s changes.'

$changed=Join-Path $work '.agents/skills/theshop-plan/SKILL.md'
$saved=Read-Utf8 $changed
Write-Utf8 $changed ($saved + "`nIndependent local edit.`n")
$beforeFailure=Get-Digest (Read-Utf8 (Join-Path $work '.claude/skills/theshop-spec/SKILL.md'))
$rejected=Run-Script (Join-Path $root '.sdd/scripts/sync-adapters.ps1') @('-RepositoryRoot',$root,'-OutputRoot',$work)
Assert-Check 'Independent generated edits block publication without partial writes' ($rejected.exitCode -ne 0 -and (Read-Utf8 $changed).Contains('Independent local edit.') -and $beforeFailure -eq (Get-Digest (Read-Utf8 (Join-Path $work '.claude/skills/theshop-spec/SKILL.md')))) $rejected.stderr
Write-Utf8 $changed $saved

$escapeRejected=$false
try { Resolve-WorkspacePath $work '../outside.txt' | Out-Null } catch { $escapeRejected=$true }
Assert-Check 'Generated paths cannot escape the workspace' $escapeRejected 'Parent traversal rejected before writes.'

if ($MigrationAudit) {
foreach ($sample in $baseline.gates) {
    $actual=Run-Script (Join-Path $root '.sdd/scripts/check-sdd-gates.ps1') @($sample.mode,'-Feature',$sample.feature)
    $same=$actual.exitCode -eq $sample.result.exitCode -and $actual.stdout -ceq $sample.result.stdout -and $actual.stderr -ceq $sample.result.stderr
    Assert-Check "Baseline gate parity: $($sample.feature)/$($sample.mode)" $same "Baseline exit $($sample.result.exitCode); current exit $($actual.exitCode)."
}
$preserved=@($baseline.featureArtifacts | Where-Object { -not (Test-Path (Join-Path $root $_.path)) -or (Get-FileHash (Join-Path $root $_.path)).Hash.ToLowerInvariant() -ne $_.sha256 })
Assert-Check 'Existing feature artifacts remain byte-identical' ($preserved.Count -eq 0) (($preserved | ForEach-Object path) -join ', ')
}

# Exercise rejection behavior and both native entry paths on an isolated fixture.
Copy-Item -LiteralPath (Join-Path $root '.sdd/catalog.json') -Destination (Join-Path $work '.sdd/catalog.json')
[IO.Directory]::CreateDirectory((Join-Path $work '.sdd/scripts')) | Out-Null
foreach ($name in @('check-sdd-gates.ps1','check-design-rules.ps1')) { Copy-Item (Join-Path $root ".sdd/scripts/$name") (Join-Path $work ".sdd/scripts/$name") }
$missing=Run-Script (Join-Path $work '.sdd/scripts/check-sdd-gates.ps1') @('spec','-Feature','missing') $work
Assert-Check 'Missing required artifact fails the shared gate' ($missing.exitCode -ne 0) $missing.stdout
$scope=Run-Script (Join-Path $work '.sdd/scripts/check-sdd-gates.ps1') @('scope','-Phase','domain','-Files','src/TheShop.Web/Page.razor') $work
Assert-Check 'Layer escape fails the shared scope gate' ($scope.exitCode -ne 0) $scope.stdout
Write-Utf8 (Join-Path $work 'src/TheShop.Domain/Probe.cs') "using Supabase;`npublic class Probe { }`n"
$direct=Run-Script (Join-Path $work '.sdd/scripts/check-design-rules.ps1') @('-Path','src/TheShop.Domain/Probe.cs') $work
$legacy=Run-Script (Join-Path $work '.claude/scripts/check-design-rules.ps1') @('-Path','src/TheShop.Domain/Probe.cs') $work
Assert-Check 'Both runtimes reject a Domain SDK dependency' ($direct.exitCode -eq 1 -and $legacy.exitCode -eq 1 -and $direct.stdout -ceq $legacy.stdout -and $direct.stdout -match 'Rule 2') ($direct.stdout+$legacy.stderr)
$payload=@{ tool_name='Write'; tool_input=@{ file_path=(Join-Path $work 'src/TheShop.Domain/Probe.cs') } } | ConvertTo-Json -Compress
$hook=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $work '.claude/scripts/check-design-rules.ps1')) $work 30 $payload
Assert-Check 'Claude edit payload reports the same violation with hook exit 2' ($hook.exitCode -eq 2 -and $hook.stderr.Contains('Rule 2')) $hook.stderr

# Exercise the real reversible path without touching the published adapters.
[IO.Directory]::CreateDirectory((Join-Path $work '.sdd/baseline')) | Out-Null
foreach ($name in @('inventory.json','originals.zip')) { Copy-Item (Join-Path $root ".sdd/baseline/$name") (Join-Path $work ".sdd/baseline/$name") }
$preview=Run-Script (Join-Path $root '.sdd/scripts/rollback-adapters.ps1') @('-RepositoryRoot',$work)
Assert-Check 'Rollback previews without mutation' ($preview.exitCode -eq 0 -and (Test-Path (Join-Path $work 'AGENTS.md'))) $preview.stdout
Write-Utf8 $changed ($saved + "`nIndependent local edit.`n")
$rollbackConflict=Run-Script (Join-Path $root '.sdd/scripts/rollback-adapters.ps1') @('-RepositoryRoot',$work,'-Apply')
Assert-Check 'Rollback preserves independent edits' ($rollbackConflict.exitCode -ne 0 -and (Read-Utf8 $changed).Contains('Independent local edit.') -and (Test-Path (Join-Path $work 'AGENTS.md'))) $rollbackConflict.stderr
Write-Utf8 $changed $saved
$rollback=Run-Script (Join-Path $root '.sdd/scripts/rollback-adapters.ps1') @('-RepositoryRoot',$work,'-Apply')
$restored=@($baseline.sourceFiles | Where-Object { -not (Test-Path (Join-Path $work $_.path)) -or (Get-FileHash (Join-Path $work $_.path)).Hash.ToLowerInvariant() -ne $_.sha256 })
Assert-Check 'Rollback restores all original bytes and leaves unrelated files' ($rollback.exitCode -eq 0 -and $restored.Count -eq 0 -and -not (Test-Path (Join-Path $work 'AGENTS.md')) -and (Test-Path (Join-Path $work 'src/TheShop.Domain/Probe.cs'))) ($rollback.stdout+$rollback.stderr)

$syntaxErrors=@()
foreach ($script in (Get-ChildItem (Join-Path $root '.sdd') -Recurse -File -Filter '*.ps1' | Where-Object { $_.FullName -notmatch '[\\/]\.(stage|test-work)[\\/]' })) {
    $tokens=$null; $errors=$null
    [Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$errors) | Out-Null
    $syntaxErrors += @($errors | ForEach-Object { "$($script.Name): $($_.Message)" })
}
Assert-Check 'PowerShell scripts parse' ($syntaxErrors.Count -eq 0) ($syntaxErrors -join '; ')
if ($Published) {
    $publication=Run-Script (Join-Path $root '.sdd/scripts/sync-adapters.ps1') @('-RepositoryRoot',$root,'-Check')
    Assert-Check 'Published adapters match shared sources' ($publication.exitCode -eq 0) ($publication.stdout+$publication.stderr)
}
$failures=@($results | Where-Object { -not $_.passed })
$report=[ordered]@{ schemaVersion=1; testedUtc=[DateTime]::UtcNow.ToString('o'); migrationAudit=[bool]$MigrationAudit; published=[bool]$Published; passed=($failures.Count -eq 0); checks=@($results.ToArray()); liveRuntimeExecution='Recorded separately; static checks do not establish live behavior.' }
Write-Json (Join-Path $root '.sdd/reports/static-verification.json') $report
Write-Output "[sdd-portability] $($results.Count - $failures.Count)/$($results.Count) checks passed."
if ($failures.Count) { exit 1 }
