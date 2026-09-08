# Native smoke test: generated skill, delegated reviewer, scoped prose modes, and unchanged review input.
[CmdletBinding()]
param(
    [ValidateSet('claude','codex')][string]$Runtime,
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [string]$Executable,
    [switch]$RecheckDelegation,
    [ValidateRange(30,600)][int]$TimeoutSeconds=360
)
. (Join-Path $PSScriptRoot 'Extensions.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
if ($RecheckDelegation) {
    $reportPath=Join-Path $root ".sdd/reports/extension-native-$Runtime.json"
    $report=Read-Utf8 $reportPath | ConvertFrom-Json -AsHashtable
    $logs=Resolve-WorkspacePath $root $report.logs
    if ($report.runtime -cne $Runtime -or $report.logs.Replace('\','/') -notmatch ('^\.sdd/\.test-work/extension-native/'+$Runtime+'-[a-f0-9]{32}$')) { throw 'Unexpected replay report.' }
    $evidence=Get-ExtensionDelegationEvidence (Read-Utf8 (Join-Path $logs 'session-trace.jsonl'))
    Write-Json (Join-Path $logs 'delegation-evidence.json') $evidence
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $logs ('report-before-recheck-'+[Guid]::NewGuid().ToString('N')+'.json'))
    $report.checks.delegationTrace=$evidence.passed
    $report.passed=@($report.checks.Keys | Where-Object {-not $report.checks[$_]}).Count -eq 0
    $report.delegationRecheckedUtc=[DateTime]::UtcNow.ToString('o')
    Write-Json $reportPath $report
    Write-Output "[extension-native/$Runtime] replayed actual session evidence; passed=$($report.passed)."
    if(-not $report.passed){exit 1}; exit 0
}
$fixture=Resolve-WorkspacePath ([IO.Path]::GetTempPath()) ('TheShop-extension-eval-'+[Guid]::NewGuid().ToString('N'))
$logs=Resolve-WorkspacePath $root ('.sdd/.test-work/extension-native/'+$Runtime+'-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($logs) | Out-Null
$generation=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-OutputRoot',$fixture) $root
if ($generation.exitCode -ne 0) { throw $generation.stderr }
foreach ($directory in @('contracts','skills','roles','adapters','scripts')) { Copy-Item (Join-Path $root ".sdd/$directory") (Join-Path $fixture '.sdd') -Recurse }
[IO.Directory]::CreateDirectory((Join-Path $fixture '.sdd/extensions')) | Out-Null
Copy-Item (Join-Path $root '.sdd/extensions/packages') (Join-Path $fixture '.sdd/extensions') -Recurse
Copy-Item (Join-Path $root '.sdd/extensions/registry.json') (Join-Path $fixture '.sdd/extensions/registry.json')
Copy-Item (Join-Path $root '.sdd/catalog.json') (Join-Path $fixture '.sdd/catalog.json')
Write-Utf8 (Join-Path $fixture 'token.js') "export function isActive(now, expiresAt) {`n  return now <= expiresAt;`n}`n"
Write-Utf8 (Join-Path $fixture 'review-request.md') 'Review only token.js. Contract: a token is inactive at and after its expiration time. Inputs are finite integer timestamps in identical units. This is a proposed new file. Do not edit it.'
$protected=@{}
foreach ($file in Get-ChildItem -LiteralPath $fixture -Recurse -File -Force) { $protected[[IO.Path]::GetRelativePath($fixture,$file.FullName)]=(Get-FileHash $file.FullName).Hash }
$invocation=if($Runtime -eq 'claude'){'/sdd-caveman-help'}else{'$sdd-caveman-help'}
$prompt=@'
Authorized isolated extension evaluation. Work only in this fixture. Load generated project extension __HELP__ and its required references. Do not load optional global skills, change settings, install dependencies, access connectors, or perform git mutations.

Produce three short help answers covering default, explicit lite for wishlist including saved spec.md, off, and auto-clarity. First answer uses project default, second uses explicit Caveman lite, third uses explicit normal English. Each mode request applies only to its answer; resume project default afterward. Execute the skill's procedure for each answer. Skill itself stays read-only; you, the evaluation orchestrator, may save its returned answers as full.md, lite.md, off.md.

Then delegate an actual child to sdd-cavecrew-reviewer. Review review-request.md and token.js. Give child scoped Caveman lite, English, and read-only scope. Use generated native role by name if available. If host exposes only generic workers, load complete decoded generated role instructions and pass them as active adapter specifies. Wait for result; do not perform review inline or invent a child result. Save returned findings as review.md. No reviewer edits. No other subagents.

Save evaluation.json with fields: defaultMode, liteMode, offMode, resumedMode, delegated (boolean), reviewerMode, securityExplanationMode, and loadedExtensionPaths (array). Report actual modes and execution; do not claim unavailable delegation succeeded. Final response lists saved paths and actual blockers, then stop.
'@
$prompt=$prompt.Replace('__HELP__',$invocation)
Write-Utf8 (Join-Path $logs 'prompt.md') $prompt
if (-not $Executable) { $Executable=Join-Path ([Environment]::GetFolderPath('UserProfile')) "AppData/Roaming/npm/$Runtime.ps1" }
if ($Runtime -eq 'codex') {
    # Keep this evaluation session: current CLI JSON can omit spawn events. Inspect only its exact thread's retained trace.
    $arguments=@('exec','--skip-git-repo-check','--sandbox','workspace-write','--json','--output-last-message',(Join-Path $logs 'last-message.txt'),'-C',$fixture,$prompt)
} else {
    $arguments=@('-p',$prompt,'--output-format','stream-json','--verbose','--permission-mode','default','--allowedTools','Read,Glob,Grep,Write,Edit,Skill,Agent,Task')
}
$launcher=$Executable
if ($Executable.EndsWith('.ps1')) { $launcher=$pwsh; $arguments=@('-NoProfile','-File',$Executable)+$arguments }
$timer=[Diagnostics.Stopwatch]::StartNew()
$result=Invoke-Captured $launcher $arguments $fixture $TimeoutSeconds
$timer.Stop()
Write-Utf8 (Join-Path $logs 'events.jsonl') $result.stdout
Write-Utf8 (Join-Path $logs 'errors.txt') $result.stderr
$checks=[ordered]@{processExit=$result.exitCode -eq 0}
foreach ($file in @('full.md','lite.md','off.md','review.md','evaluation.json')) {
    $checks[$file]=Test-Path (Join-Path $fixture $file)
    if ($checks[$file]) { Copy-Item (Join-Path $fixture $file) (Join-Path $logs $file) }
}
$events=@()
foreach ($line in $result.stdout -split "`n") { if($line.Trim()){try{$events += ConvertFrom-Json $line -AsHashtable -ErrorAction Stop}catch{}} }
$sessionTrace=$null
if ($Runtime -eq 'codex') {
    $thread=@($events | Where-Object { $_.type -eq 'thread.started' })
    if ($thread.Count -eq 1 -and $thread[0].thread_id -match '^[a-f0-9-]{36}$') {
        $sessionRoot=if($env:CODEX_HOME){Join-Path $env:CODEX_HOME 'sessions'}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/sessions'}
        $traces=@(Get-ChildItem -LiteralPath $sessionRoot -Recurse -File -Filter "*$($thread[0].thread_id)*.jsonl" -ErrorAction SilentlyContinue)
        if ($traces.Count -eq 1) {
            $sessionTrace=Read-Utf8 $traces[0].FullName
            Write-Utf8 (Join-Path $logs 'session-trace.jsonl') $sessionTrace
        }
    }
}
$delegated=$false
foreach ($event in $events) {
    if ($event -isnot [System.Collections.IDictionary]) { continue }
    if ($event.Contains('item') -and $event.item -is [System.Collections.IDictionary] -and $event.item.Contains('type') -and $event.item.type -eq 'collab_tool_call' -and ($event.item | ConvertTo-Json -Depth 20) -match 'spawn_agent') { $delegated=$true }
    if ($event.Contains('message') -and $event.message -is [System.Collections.IDictionary] -and $event.message.Contains('content')) {
        foreach ($content in $event.message.content) { if ($content.type -eq 'tool_use' -and $content.name -in @('Agent','Task')) { $delegated=$true } }
    }
}
$checks.delegationTrace=$delegated
if ($Runtime -eq 'claude') {
    $completedResults=@($events | Where-Object { $_.type -eq 'result' -and $_.Contains('subagent_stats') })
    $checks.delegationTrace=$delegated -and $completedResults.Count -gt 0 -and $completedResults[-1].subagent_stats.completed -gt 0
    if ($completedResults.Count) { Write-Json (Join-Path $logs 'delegation-evidence.json') $completedResults[-1].subagent_stats }
}
if ($sessionTrace) {
    $evidence=Get-ExtensionDelegationEvidence $sessionTrace
    $checks.delegationTrace=$evidence.passed
    Write-Json (Join-Path $logs 'delegation-evidence.json') $evidence
}
if ($checks['evaluation.json']) {
    try {
        $answer=Read-Utf8 (Join-Path $fixture 'evaluation.json') | ConvertFrom-Json -AsHashtable
        $checks.modeRecognition=$answer.defaultMode -eq 'full' -and $answer.liteMode -eq 'lite' -and $answer.offMode -in @('off','normal English','normal') -and $answer.resumedMode -eq 'full' -and $answer.reviewerMode -eq 'lite' -and $answer.delegated -eq $true
    } catch { $checks.modeRecognition=$false }
}
if ($checks['review.md']) {
    $review=Read-Utf8 (Join-Path $fixture 'review.md')
    $checks.expiryFinding=$review -match 'token\.js:2' -and $review -match '(?i)expir' -and $review -match '<'
}
$changed=@($protected.Keys | Where-Object { $path=Join-Path $fixture $_; -not (Test-Path $path) -or (Get-FileHash $path).Hash -cne $protected[$_] })
$checks.readOnlyInputs=$changed.Count -eq 0
$failed=@($checks.Keys | Where-Object {-not $checks[$_]})
$blocker=if($result.stdout -match '"error"\s*:\s*"rate_limit"'){'Client session quota exhausted.'}elseif($result.exitCode -eq 124){'Native run timed out.'}else{$null}
$report=[ordered]@{runtime=$Runtime;checkedUtc=[DateTime]::UtcNow.ToString('o');passed=$failed.Count -eq 0;processExitCode=$result.exitCode;blocker=$blocker;checks=$checks;elapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2);fixture=$fixture;logs=[IO.Path]::GetRelativePath($root,$logs);changedInputs=$changed;styleValidation='Mode recognition and delegation evidence are mechanical. Inspect saved prose and actual child trace separately; no fixed style or savings guarantee.'}
Write-Json (Join-Path $root ".sdd/reports/extension-native-$Runtime.json") $report
Write-Output "[extension-native/$Runtime] passed=$($report.passed); failed=$($failed -join ','); logs=$logs"
if($failed.Count){exit 1}
