[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'Sdd-Evidence.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$work=Resolve-WorkspacePath $root ('.sdd/.test-work/evidence-'+[Guid]::NewGuid().ToString('N'))
$pwsh=(Get-Process -Id $PID).Path
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Pass,[string]$Detail='') {
    $checks.Add(@{name=$Name;passed=$Pass;detail=$Detail})
    if (-not $Pass) { throw "$Name failed: $Detail" }
}
function Run([string]$Action,[string[]]$Extra=@()) {
    return Invoke-Captured $pwsh (@('-NoProfile','-File',(Join-Path $work '.sdd/scripts/manage-sdd-evidence.ps1'),'-Action',$Action,'-Feature','probe')+$Extra) $work 120
}
[IO.Directory]::CreateDirectory((Join-Path $work '.sdd')) | Out-Null
foreach ($folder in @('scripts','contracts','roles','adapters','skills')) { Copy-Item -LiteralPath (Join-Path $root ".sdd/$folder") -Destination (Join-Path $work '.sdd') -Recurse }
Copy-Item -LiteralPath (Join-Path $root '.sdd/catalog.json') -Destination (Join-Path $work '.sdd/catalog.json')
# Stable synthetic artifacts exercise real gates; production features are not test dependencies.
Write-Utf8 (Join-Path $work '.specs/probe/spec.md') (Read-Utf8 (Join-Path $root '.sdd/evals/evidence/spec.md'))
Write-Utf8 (Join-Path $work '.specs/probe/plan.md') (Read-Utf8 (Join-Path $root '.sdd/evals/evidence/plan.md'))
Write-Utf8 (Join-Path $work 'src/Domain.cs') 'public record Label(string Value);'
$initialGate=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $work '.sdd/scripts/check-sdd-gates.ps1'),'spec','-Feature','probe') $work 120
if ($initialGate.exitCode -ne 0) { throw "Fixture spec gate failed: $($initialGate.stdout) $($initialGate.stderr)" }
Write-Utf8 (Join-Path $work 'spec-gate.log') ($initialGate.stdout+$initialGate.stderr)
$record=@{outcome='Fixture structural proof only';checks=@(@{id='spec-gate';command='pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 spec -Feature probe';exitCode=0;logPath='spec-gate.log'});sourcePaths=@('src')}
Write-Json (Join-Path $work 'record.json') $record
foreach ($badChecks in @(@{name='Empty';value=@()},@{name='Failed';value=@(@{id='failed';command='fixture failure';exitCode=1;logPath='spec-gate.log'})},@{name='Untyped';value=@{command='fixture'}})) {
    $badRecord=$record.Clone(); $badRecord.checks=$badChecks.value
    Write-Json (Join-Path $work 'bad-record.json') $badRecord
    $r=Run 'record' @('-Stage','spec','-RequestPath','bad-record.json')
    Check "$($badChecks.name) checks cannot publish stage evidence" ($r.exitCode -ne 0 -and -not (Test-Path (Join-Path $work '.specs/probe/evidence/state.json')))
}
$badRecord=$record.Clone(); $badRecord.checks=@(@{id='missing';command='fixture';exitCode=0;logPath='missing.log'})
Write-Json (Join-Path $work 'bad-record.json') $badRecord
$r=Run 'record' @('-Stage','spec','-RequestPath','bad-record.json'); Check 'Missing check log rejected' ($r.exitCode -ne 0 -and $r.stderr -match 'log missing or empty') $r.stderr
$r=Run 'record' @('-Stage','spec','-RequestPath','record.json')
Check 'Real spec gate captured before evidence publication' ($r.exitCode -eq 0) ($r.stdout+$r.stderr)
$r=Run 'check' @('-Stage','spec'); Check 'Fresh evidence accepted' ($r.exitCode -eq 0) $r.stderr
$state=Read-SddEvidence $work 'probe'
Check 'Receipt carries actual gate output' ($state.stages.spec.gateReceipts[0].stdout -match 'clean' -and $state.stages.spec.gateReceipts[0].exitCode -eq 0)
Check 'Caller check log automatically fingerprinted' ($state.stages.spec.sources.Contains('spec-gate.log'))
foreach ($dependency in @('Test-Proof.ps1','check-worker-scope.ps1','format-changes.ps1')) {
    $dependencyPath=Join-Path $work ".sdd/scripts/$dependency"
    $savedDependency=[IO.File]::ReadAllBytes($dependencyPath)
    Write-Utf8 $dependencyPath ((Read-Utf8 $dependencyPath)+"`n# Changed verification dependency.`n")
    $r=Run 'check' @('-Stage','spec')
    Check "Changed $dependency invalidates stage evidence" ($r.exitCode -ne 0)
    [IO.File]::WriteAllBytes($dependencyPath,$savedDependency)
    $r=Run 'check' @('-Stage','spec')
    Check "Restoring $dependency bytes restores fresh evidence" ($r.exitCode -eq 0) $r.stderr
}
$savedGateLog=Read-Utf8 (Join-Path $work 'spec-gate.log')
Write-Utf8 (Join-Path $work 'spec-gate.log') 'Replaced execution evidence.'
$r=Run 'check' @('-Stage','spec'); Check 'Replaced check log invalidates recorded stage' ($r.exitCode -ne 0)
Write-Utf8 (Join-Path $work 'spec-gate.log') $savedGateLog
$r=Run 'record' @('-Stage','plan','-RequestPath','record.json'); Check 'Real plan gate captured for upstream baseline' ($r.exitCode -eq 0) ($r.stdout+$r.stderr)
$handoff=@{role='shop-application-implementer';runtime='codex';mode='full';language='English';ownedPaths=@('src/Application.cs');sourcePaths=@('src/Domain.cs');planSections=@('4');literalApis='public record Label(string Value);';outcome='Domain ready';changedFiles=@('src/Domain.cs');checks='fixture only';unresolved='none';nextAction='Implement Application from exact API'}
Write-Json (Join-Path $work 'handoff.json') $handoff
$r=Run 'handoff' @('-RequestPath','handoff.json'); Check 'Handoff created' ($r.exitCode -eq 0) $r.stderr
$id=[IO.Path]::GetFileNameWithoutExtension($r.stdout.Trim())
$r=Run 'check-handoff' @('-Id',$id); Check 'Cross-runtime handoff source check' ($r.exitCode -eq 0) $r.stderr
$temporary=Read-SddEvidence $work 'probe'
$temporary.stages.test=$temporary.stages.spec.Clone(); $temporary.stages.test.stale=$true
Write-Json (Join-Path $work '.specs/probe/evidence/state.json') $temporary
$r=Run 'check-handoff' @('-Id',$id); Check 'Stale downstream test does not deadlock implementation handoff' ($r.exitCode -eq 0) $r.stderr
$temporary.stages.Remove('test'); Write-Json (Join-Path $work '.specs/probe/evidence/state.json') $temporary
$saved=Read-Utf8 (Join-Path $work ".specs/probe/evidence/handoffs/$id.json") | ConvertFrom-Json -AsHashtable
Check 'API text preserved exactly' ($saved.payload.literalApis -ceq $handoff.literalApis)
# Baseline tests run inside Implement, before separate Test-stage evidence exists.
$baselineHandoff=$handoff.Clone(); $baselineHandoff.role='shop-test-writer'; $baselineHandoff.purpose='implementation-tests'; $baselineHandoff.ownedPaths=@('tests/TheShop.Domain.Tests/ProbeTests.cs')
Write-Json (Join-Path $work 'baseline-handoff.json') $baselineHandoff
$r=Run 'handoff' @('-RequestPath','baseline-handoff.json'); Check 'Implementation test writer accepts fresh Plan baseline' ($r.exitCode -eq 0) $r.stderr
$baselineId=[IO.Path]::GetFileNameWithoutExtension($r.stdout.Trim())
$r=Run 'check-handoff' @('-Id',$baselineId); Check 'Implementation test purpose survives persisted continuation' ($r.exitCode -eq 0) $r.stderr
$baselineHandoff.role='shop-test-runner'; Write-Json (Join-Path $work 'baseline-handoff.json') $baselineHandoff
$r=Run 'handoff' @('-RequestPath','baseline-handoff.json'); Check 'Implementation test runner accepts fresh Plan baseline' ($r.exitCode -eq 0) $r.stderr
$baselineHandoff.Remove('purpose'); Write-Json (Join-Path $work 'baseline-handoff.json') $baselineHandoff
$r=Run 'handoff' @('-RequestPath','baseline-handoff.json'); Check 'Separate test runner still requires Implement evidence' ($r.exitCode -ne 0 -and $r.stderr -match 'Missing upstream evidence') $r.stderr
$invalidPurpose=$handoff.Clone(); $invalidPurpose.purpose='implementation-tests'; Write-Json (Join-Path $work 'bad-purpose.json') $invalidPurpose
$r=Run 'handoff' @('-RequestPath','bad-purpose.json'); Check 'Implementation-test exception rejects other roles' ($r.exitCode -ne 0 -and $r.stderr -match 'Unsupported handoff purpose') $r.stderr
$badRecord=$record.Clone(); $badRecord.purpose='implementation-tests'; Write-Json (Join-Path $work 'bad-record.json') $badRecord
$r=Run 'record' @('-Stage','test','-RequestPath','bad-record.json'); Check 'Implementation-test purpose cannot satisfy Test stage' ($r.exitCode -ne 0 -and $r.stderr -match 'cannot record a workflow stage') $r.stderr
$r=Run 'record' @('-Stage','implement','-RequestPath','record.json'); Check 'Implement fixture captures actual structural gates' ($r.exitCode -eq 0) $r.stderr
$baselineHandoff.role='shop-test-writer'; Write-Json (Join-Path $work 'baseline-handoff.json') $baselineHandoff
$r=Run 'handoff' @('-RequestPath','baseline-handoff.json'); Check 'Separate test writer accepts Implement evidence' ($r.exitCode -eq 0) $r.stderr
Write-Utf8 (Join-Path $work 'tests/TheShop.Domain.Tests/ProbeTests.cs') '// Fixture writer output; no application test execution claimed.'
$baselineHandoff.role='shop-test-runner'; Write-Json (Join-Path $work 'baseline-handoff.json') $baselineHandoff
$r=Run 'handoff' @('-RequestPath','baseline-handoff.json'); Check 'Writer additions permit normal runner handoff' ($r.exitCode -eq 0) $r.stderr
$testPaths=Get-SddStagePaths 'probe' 'test'; $excluded=Get-SddStageExclusions 'test'
$testSnapshot=Get-SddInputSnapshot $work $testPaths $excluded
Write-Utf8 (Join-Path $work 'tests/TheShop.E2E.Tests/Journey.cs') '// Fixture journey output.'
Check 'E2E additions preserve unit/component Test fingerprint' (Test-SddSnapshot $testSnapshot (Get-SddInputSnapshot $work $testPaths $excluded))
Write-Utf8 (Join-Path $work 'tests/TheShop.Domain.Tests/ProbeTests.cs') '// Changed unit assertion fixture.'
Check 'Unit test changes invalidate Test fingerprint' (-not (Test-SddSnapshot $testSnapshot (Get-SddInputSnapshot $work $testPaths $excluded)))
$trace='tests/TheShop.E2E.Tests/bin/traces/probe.log'
Write-Utf8 (Join-Path $work $trace) 'Original trace fixture.'
$traceSnapshot=Get-SddInputSnapshot $work @($trace)
Check 'Explicit bin log receives source hash' ($traceSnapshot.Contains($trace))
Write-Utf8 (Join-Path $work $trace) 'Changed trace fixture.'
Check 'Explicit bin log replacement invalidates evidence' (-not (Test-SddSnapshot $traceSnapshot (Get-SddInputSnapshot $work @($trace))))
Write-Utf8 (Join-Path $work 'src/Domain.cs') "/// <summary>Fixture label.</summary>`npublic record Label(string Value);"
$r=Run 'check' @('-Stage','implement'); Check 'XML documentation edits require explicit upstream revalidation' ($r.exitCode -ne 0)
Write-Utf8 (Join-Path $work 'src/Domain.cs') 'public record Label(string Value);'
Write-Utf8 (Join-Path $work 'src/Domain.cs') 'public record Label(string Value, bool Done);'
$r=Run 'check-handoff' @('-Id',$id); Check 'Changed upstream API blocks continuation' ($r.exitCode -ne 0 -and $r.stderr -match 'Stale handoff') $r.stderr
$r=Run 'check' @('-Stage','spec'); Check 'Uncommitted source edits invalidate evidence' ($r.exitCode -ne 0)
Write-Utf8 (Join-Path $work 'src/Domain.cs') 'public record Label(string Value);'
Write-Utf8 (Join-Path $work 'src/Added.cs') 'public record Added();'
$r=Run 'check' @('-Stage','spec'); Check 'New source file invalidates directory snapshot' ($r.exitCode -ne 0)
# Move only explicitly named fixture file, preserving it outside captured source tree.
Move-Item -LiteralPath (Resolve-WorkspacePath $work 'src/Added.cs') -Destination (Resolve-WorkspacePath $work 'Added.saved')
$r=Run 'check' @('-Stage','spec'); Check 'Matching source bytes regain freshness absent amendment' ($r.exitCode -eq 0)
Move-Item -LiteralPath (Resolve-WorkspacePath $work 'src/Domain.cs') -Destination (Resolve-WorkspacePath $work 'Domain.saved')
$r=Run 'check' @('-Stage','spec'); Check 'Deleted source detected' ($r.exitCode -ne 0)
Move-Item -LiteralPath (Resolve-WorkspacePath $work 'Domain.saved') -Destination (Resolve-WorkspacePath $work 'src/Domain.cs')
$handoff.sourcePaths=@('../outside'); Write-Json (Join-Path $work 'bad.json') $handoff
$r=Run 'handoff' @('-RequestPath','bad.json'); Check 'Traversal rejected' ($r.exitCode -ne 0)
$specPath=Join-Path $work '.specs/probe/spec.md'
$before=Get-SddFileHash $specPath
Check 'Original spec retained byte-for-byte' ((Get-SddFileHash (Join-Path $work ".specs/probe/evidence/artifacts/$before/spec.md")) -ceq $before)
$original=Read-Utf8 $specPath
Write-Utf8 $specPath ($original+"`nRecorded fixture amendment.`n")
Write-Json (Join-Path $work 'amend.json') @{target='spec';reason='Fixture amendment';decision='Authorized fixture only';changedIds=@('FR-1');beforeHash=$before;afterHash=(Get-SddFileHash $specPath);evidence='Original fixture source retained in repository'}
$r=Run 'amend' @('-RequestPath','amend.json'); Check 'Deliberate amendment accepted with matching hashes' ($r.exitCode -eq 0) $r.stderr
Write-Utf8 $specPath $original
$r=Run 'check' @('-Stage','spec'); Check 'Reverting bytes cannot silently clear amendment invalidation' ($r.exitCode -ne 0)
$r=Run 'record' @('-Stage','spec','-RequestPath','record.json'); Check 'Real gate rerun clears target invalidation' ($r.exitCode -eq 0) $r.stderr
foreach ($classification in @('implementation-defect','test-defect','requirement-gap','environment-problem')) {
    Write-Json (Join-Path $work 'failure.json') @{classification=$classification;stage='spec';evidence='Fixture failure';reason='Classification exercise';nextAction='Rerun fixture gate'}
    $r=Run 'failure' @('-RequestPath','failure.json'); Check "Failure recorded: $classification" ($r.exitCode -eq 0) $r.stderr
}
$r=Run 'check' @('-Stage','spec'); Check 'Failure blocks stale success' ($r.exitCode -ne 0)
$r=Run 'record' @('-Stage','spec','-RequestPath','record.json'); Check 'Successful structural rerun cannot silently resolve failures' ($r.exitCode -ne 0 -and $r.stderr -match 'explicit resolveFailures') $r.stderr
$failedState=Read-SddEvidence $work 'probe'
$resolvedRecord=$record.Clone()
$resolvedRecord.resolveFailures=@($failedState.failures | Where-Object {-not $_.resolved} | ForEach-Object {@{id=$_.id;reason='Fixture-only classification resolved by fresh spec structural gate; no production recovery claimed.';checkIds=@('spec-gate')}})
Write-Json (Join-Path $work 'resolved-record.json') $resolvedRecord
$badResolution=$record.Clone(); $badResolution.resolveFailures=@(@{id=$failedState.failures[0].id;reason='Fixture invalid evidence link.';checkIds=@('unrecorded-check')})
Write-Json (Join-Path $work 'bad-resolution.json') $badResolution
$r=Run 'record' @('-Stage','spec','-RequestPath','bad-resolution.json'); Check 'Unknown failure-resolution check cannot clear failure' ($r.exitCode -ne 0 -and $r.stderr -match 'Unknown failure-resolution check ID') $r.stderr
Write-Utf8 $specPath 'Invalid spec.'
$r=Run 'record' @('-Stage','spec','-RequestPath','resolved-record.json'); Check 'Failed gate cannot refresh evidence or resolve failures' ($r.exitCode -ne 0 -and $r.stderr -match 'Gate spec failed') $r.stderr
Check 'Rejected record preserves unresolved failure history' (@((Read-SddEvidence $work 'probe').failures | Where-Object {-not $_.resolved}).Count -eq 4)
Write-Utf8 $specPath $original
$r=Run 'record' @('-Stage','spec','-RequestPath','resolved-record.json'); Check 'Explicit successful evidence resolves named failures' ($r.exitCode -eq 0) $r.stderr
$resolvedState=Read-SddEvidence $work 'probe'
Check 'Failure resolution retains reason and linked check IDs' (@($resolvedState.failures | Where-Object {$_.resolved -and $_.resolution.checkIds[0] -ceq 'spec-gate'}).Count -eq 4)
Write-Utf8 $specPath 'Invalid spec.'
Check 'Historical state preserved' (@(Get-ChildItem (Join-Path $work '.specs/probe/evidence/history') -File).Count -gt 0)
# Exercise dependency ordering without pretending a fixture ran all production gates.
$state=Read-SddEvidence $work 'probe'
$state.stages.plan=$state.stages.spec.Clone()
Write-Json (Join-Path $work '.specs/probe/evidence/state.json') $state
$r=Run 'record' @('-Stage','plan','-RequestPath','record.json'); Check 'Stale upstream blocks downstream recording' ($r.exitCode -ne 0 -and $r.stderr -match 'Revalidate upstream first') $r.stderr
$gate=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $work '.sdd/scripts/check-sdd-gates.ps1'),'evidence','-Feature','probe') $work 120
Check 'Canonical evidence gate fails stale sidecar' ($gate.exitCode -ne 0)
$empty=[ordered]@{schemaVersion=1;feature='probe';stages=[ordered]@{};failures=@();amendments=@()}
Write-Json (Join-Path $work '.specs/probe/evidence/state.json') $empty
Write-Json (Join-Path $work 'failure.json') @{classification='environment-problem';stage='implement';evidence='No previous stage record';reason='Fixture unavailable tool';nextAction='Restore environment'}
$r=Run 'failure' @('-RequestPath','failure.json')
$r=Run 'check'; Check 'Failure blocks completion even without earlier evidence record' ($r.exitCode -ne 0)
$linkRoot=Resolve-WorkspacePath $work 'links'
[IO.Directory]::CreateDirectory($linkRoot) | Out-Null
$linkType=if($IsWindows){'Junction'}else{'SymbolicLink'}
New-Item -ItemType $linkType -Path (Join-Path $linkRoot 'nested') -Target (Join-Path $work '.sdd/contracts') | Out-Null
$rejected=$false
try { Get-SddInputSnapshot $work @('links') | Out-Null } catch { $rejected=$_.Exception.Message -match 'Reparse point' }
Check 'Nested directory link rejected before source hashing' $rejected
Write-Json (Join-Path $root '.sdd/reports/evidence-validation.json') @{schemaVersion=1;checkedUtc=[DateTime]::UtcNow.ToString('o');passed=$true;checks=$checks;fixture=$work}
Write-Output "$($checks.Count) evidence checks passed. Fixture: $work"
