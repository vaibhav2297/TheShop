# Paired native evaluations in temporary workspaces outside the project instruction ancestry.
[CmdletBinding()]
param(
    [ValidateSet('baseline','candidate')][string]$Variant,
    [ValidateSet('claude','codex')][string]$Runtime,
    [ValidateSet('decisions','spec','clarify','plan')][string]$Case='decisions',
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [string]$Executable,
    [string]$ContinueFixture,
    [string]$BaselineArchive='.sdd/baseline/caveman/phase5.zip',
    [ValidatePattern('\A[a-z][a-z0-9-]*\z')][string]$ReportPrefix='caveman',
    [ValidateRange(30,600)][int]$TimeoutSeconds=300,
    [ValidateRange(1,10)][int]$Repeat=1
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
$runId="$ReportPrefix-$Variant-$Runtime-$Case-$Repeat-"+[Guid]::NewGuid().ToString('N')
$logRoot=Resolve-WorkspacePath $root ".sdd/.test-work/caveman-native/$runId"
[IO.Directory]::CreateDirectory($logRoot) | Out-Null
$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if ($ContinueFixture) {
    $fixture=[IO.Path]::GetFullPath($ContinueFixture)
    $relative=[IO.Path]::GetRelativePath($tempRoot,$fixture)
    if ($relative -notmatch '^TheShop-sdd-eval-[a-f0-9]{32}$') { throw 'Continuation requires an existing isolated SDD evaluation fixture.' }
    Resolve-WorkspacePath $tempRoot $relative | Out-Null
    $marker=Get-Content (Join-Path $fixture '.sdd/evaluation-fixture.json') -Raw | ConvertFrom-Json
    if ($marker.variant -cne $Variant -or $marker.sourceRoot -cne $root) { throw 'Continuation variant/source mismatch.' }
} else {
    $fixture=Resolve-WorkspacePath $tempRoot ('TheShop-sdd-eval-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($fixture) | Out-Null
    if ($Variant -eq 'baseline') {
        $zip=[IO.Compression.ZipFile]::OpenRead((Resolve-WorkspacePath $root $BaselineArchive))
        try {
            foreach ($entry in $zip.Entries) {
                $destination=Resolve-WorkspacePath $fixture $entry.FullName
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination)
            }
        } finally { $zip.Dispose() }
    } else {
        $generation=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-OutputRoot',$fixture) $root
        if ($generation.exitCode -ne 0) { throw $generation.stderr }
        foreach ($directory in @('skills','roles','contracts','adapters','scripts')) {
            Copy-Item -LiteralPath (Join-Path $root ".sdd/$directory") -Destination (Join-Path $fixture '.sdd') -Recurse
        }
        Copy-Item -LiteralPath (Join-Path $root '.sdd/catalog.json') -Destination (Join-Path $fixture '.sdd/catalog.json')
        if (Test-Path (Join-Path $root '.sdd/extensions/registry.json')) {
            [IO.Directory]::CreateDirectory((Join-Path $fixture '.sdd/extensions')) | Out-Null
            Copy-Item -LiteralPath (Join-Path $root '.sdd/extensions/registry.json') -Destination (Join-Path $fixture '.sdd/extensions/registry.json')
            Copy-Item -LiteralPath (Join-Path $root '.sdd/extensions/packages') -Destination (Join-Path $fixture '.sdd/extensions') -Recurse
        }
    }
    Write-Json (Join-Path $fixture '.sdd/evaluation-fixture.json') @{ variant=$Variant; sourceRoot=$root; createdUtc=[DateTime]::UtcNow.ToString('o') }
}
if (-not $Executable) { $Executable=Join-Path ([Environment]::GetFolderPath('UserProfile')) "AppData/Roaming/npm/$Runtime.ps1" }
if (-not (Test-Path -LiteralPath $Executable)) { throw "Runtime executable unavailable: $Executable" }
if ($Case -eq 'decisions') {
    $prompt=Read-Utf8 (Join-Path $root '.sdd/evals/caveman-decisions.md')
} else {
    $native=if($Runtime -eq 'claude'){'/theshop-spec'}else{'$theshop-spec'}
    if ($Case -eq 'clarify') { $native=if($Runtime -eq 'claude'){'/theshop.clarify'}else{'$theshop-clarify'} }
    if ($Case -eq 'plan') { $native=if($Runtime -eq 'claude'){'/theshop-plan'}else{'$theshop-plan'} }
    $prompt=$native+' sdd-portability-probe'
    if ($Case -eq 'spec') {
        $prompt += ' --desc A signed-in store admin maintains a private session-only checklist of up to 5 product-photo preparation labels. Add nonempty labels of at most 40 characters; mark done or undone; remove labels. Trim surrounding spaces. Reject duplicates ignoring case, empty labels, labels over 40 characters, and additions beyond 5 items with a clear message; preserve existing list on rejection. Ending session clears checklist. Other users cannot see or change it. No sharing, notifications, scheduling, persistence, or product changes. English and French text, keyboard operation, visible focus, and screen-reader announcements are required. Every product choice above is confirmed; no unresolved assumptions.'
    }
    $prompt += "`n`nAuthorized isolated evaluation. Follow fixture's generated native skill and its required references. Do not load optional global skills. Work only in this fixture. "
    if ($Case -eq 'clarify') {
        $prompt += 'Resume existing spec and ledger from the other client. All product choices are confirmed; reconcile footer and ledger if the previous stage left Draft with zero assumptions. '
    }
    if ($Case -eq 'plan') {
        $prompt += 'Use the confirmed spec already present. Technical direction: session-only checklist, no database schema or migration, reuse project layer boundaries and localized MudBlazor UI. No Figma design is supplied: explicitly skip Figma and log missing node IDs in Section 11 as the workflow requires. Fixture contains no application source; record unverified reuse choices as assumptions rather than inventing existing files. Do not expand product scope. Run actual plan and status gates. '
    } else { $prompt += 'Run actual spec and status gates. ' }
    $prompt += 'Record actual evidence. No implementation, connectors, installing tools, git actions, subagents, or external mutations. Final response: saved paths, counts, actual gate results, and loaded references. Stop after reporting.'
}
Write-Utf8 (Join-Path $logRoot 'prompt.md') $prompt
if ($Runtime -eq 'codex') {
    $arguments=@('exec','--skip-git-repo-check','--ephemeral','--sandbox','workspace-write','--json','--output-last-message',(Join-Path $logRoot 'last-message.txt'),'-C',$fixture,$prompt)
} else {
    $allowed='Read,Glob,Grep,Write,Edit,Skill'
    if ($Case -ne 'decisions') { $allowed += ',Bash(pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 *),Bash(pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 *)' }
    $arguments=@('-p',$prompt,'--output-format','stream-json','--verbose','--permission-mode','default','--allowedTools',$allowed)
}
$launcher=$Executable
if ($Executable.EndsWith('.ps1')) { $arguments=@('-NoProfile','-File',$Executable)+$arguments; $launcher=$pwsh }
$timer=[Diagnostics.Stopwatch]::StartNew()
$result=Invoke-Captured $launcher $arguments $fixture $TimeoutSeconds
$timer.Stop()
Write-Json (Join-Path $logRoot 'process.json') @{ exitCode=$result.exitCode; elapsedSeconds=$timer.Elapsed.TotalSeconds; fixture=$fixture }
Write-Utf8 (Join-Path $logRoot 'events.jsonl') $result.stdout
Write-Utf8 (Join-Path $logRoot 'errors.txt') $result.stderr
$events=@()
foreach ($line in $result.stdout -split "`n") {
    if (-not $line.Trim()) { continue }
    try { $parsed=ConvertFrom-Json -InputObject $line -AsHashtable -ErrorAction Stop; if ($parsed -is [System.Collections.IDictionary]) { $events += $parsed } } catch {}
}
$usage=@(); $models=@()
foreach ($event in $events) {
    if ($event.ContainsKey('usage') -and $event.usage) { $usage += @{ event=$event.type; usage=$event.usage } }
    if ($event.ContainsKey('model')) { $models += $event.model }
    if ($event.ContainsKey('message') -and $event.message -is [System.Collections.IDictionary] -and $event.message.ContainsKey('model')) { $models += $event.message.model }
}
$checks=[ordered]@{}
if ($Case -eq 'decisions') {
    $answerPath=Join-Path $fixture 'evaluation.json'
    if (Test-Path -LiteralPath $answerPath) {
        try {
            $answer=Get-Content -LiteralPath $answerPath -Raw | ConvertFrom-Json -AsHashtable
            $checks.product=$answer.product.mayWriteArtifacts -ceq $false
            $checks.plan=($answer.plan.mayStartImplementation -ceq $false -and $answer.plan.waiverRequiredIfProceed -ceq $true)
            $checks.domain=$answer.domain.mayWriteTests -ceq $false
            $checks.handoff=($answer.handoff.mayStartApplication -ceq $false -and $answer.handoff.retryLimit -eq 1)
            $checks.tests=($answer.tests.expected -eq 12 -and $answer.tests.discovered -eq 9 -and $answer.tests.verdict -cmatch 'NOT READY' -and $answer.tests.mayLowerManifest -ceq $false)
            $checks.review=($answer.review.verdict -cmatch 'CHANGES REQUESTED' -and $answer.review.mayApplyFix -ceq $false)
            $checks.literals=($answer.literalApi -ceq 'public Result<Money> CalculateTotal(Guid cartId, CancellationToken cancellationToken = default);' -and $answer.literalError -ceq 'Expected 12 tests; discovered 9. Feature is NOT READY.')
            if ($Variant -eq 'candidate') {
                $checks.modeSelection=($answer.style.default -ceq 'full' -and $answer.style.savedProse -ceq 'full' -and $answer.style.clarification -match 'normal' -and $answer.style.afterClarification -ceq 'full' -and $answer.style.explicitLite -ceq 'lite')
            }
            $checks.summaryExists=Test-Path (Join-Path $fixture 'summary.md')
        } catch { $checks.validAnswer=$false; Write-Utf8 (Join-Path $logRoot 'validation-error.txt') $_.ToString() }
    } else { $checks.answerExists=$false }
    foreach ($file in @('evaluation.json','summary.md')) { if (Test-Path (Join-Path $fixture $file)) { Copy-Item -LiteralPath (Join-Path $fixture $file) -Destination (Join-Path $logRoot $file) } }
} else {
    $artifactMode=if($Case -eq 'plan'){'plan'}else{'spec'}
    foreach ($mode in @($artifactMode,'status')) {
        $gate=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $fixture '.sdd/scripts/check-sdd-gates.ps1'),$mode,'-Feature','sdd-portability-probe') $fixture 60
        $checks[$mode]=$gate.exitCode -eq 0
        Write-Json (Join-Path $logRoot "$mode-gate.json") $gate
    }
    $specPath=Join-Path $fixture ".specs/sdd-portability-probe/$artifactMode.md"
    if (Test-Path $specPath) {
        $spec=Read-Utf8 $specPath
        $expected=if($Case -eq 'clarify'){'Confirmed'}else{'Draft'}
        $checks.state=$spec -match ('\*\*Status:\*\* '+$expected)
        Copy-Item -LiteralPath (Split-Path $specPath) -Destination (Join-Path $logRoot 'artifacts') -Recurse
    } else { $checks.state=$false }
}
$checks.processExit=$result.exitCode -eq 0
$failed=@($checks.Keys | Where-Object { -not $checks[$_] })
$report=[ordered]@{ checkedUtc=[DateTime]::UtcNow.ToString('o'); variant=$Variant; runtime=$Runtime; case=$Case; repeat=$Repeat; passed=$failed.Count -eq 0; checks=$checks; processExitCode=$result.exitCode; elapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2); fixture=$fixture; logs=[IO.Path]::GetRelativePath($root,$logRoot).Replace('\','/'); models=@($models | Select-Object -Unique); usage=$usage; measurement='Native runtime-reported usage; baseline/candidate compared within runtime only. Decision exercises do not claim production execution.'; styleValidation='Mode fields establish recognition only. Human review of actual artifacts assesses compression, completeness, and auto-clarity separately.' }
$report.baselineArchive=$BaselineArchive
$report.reportPrefix=$ReportPrefix
Write-Json (Join-Path $root ".sdd/reports/$ReportPrefix-$Variant-$Runtime-$Case-$Repeat.json") $report
Write-Output "[$Variant/$Runtime/$Case/$Repeat] passed=$($report.passed); exit=$($result.exitCode); failed=$($failed -join ','); fixture=$fixture"
if ($failed.Count) { exit 1 }
