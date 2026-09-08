[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('handoff','check-handoff','record','check','failure','amend')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('\A[a-z0-9]+(-[a-z0-9]+)*\z')][string]$Feature,
    [string]$RequestPath,
    [string]$Id,
    [ValidateSet('spec','plan','implement','test','verify','review','document')][string]$Stage,
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..')
)
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'Sdd-Evidence.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('/','\')
$state=Read-SddEvidence $root $Feature
$base=".specs/$Feature/evidence"
$statePath=Resolve-WorkspacePath $root "$base/state.json"
$request=if($RequestPath){Read-Utf8 (Resolve-WorkspacePath $root $RequestPath) | ConvertFrom-Json -AsHashtable}else{$null}
function Require-Fields($Value,[string[]]$Fields) {
    if (-not $Value) { throw 'Request JSON required.' }
    foreach ($field in $Fields) {
        if (-not $Value.Contains($field) -or $null -eq $Value[$field] -or ($Value[$field] -is [string] -and [string]::IsNullOrWhiteSpace($Value[$field]))) { throw "Missing request field: $field" }
    }
}
function Validate-Checks($Checks) {
    if ($Checks -isnot [Collections.IList] -or $Checks.Count -eq 0) { throw 'checks must be a nonempty array of successful check receipts.' }
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($check in $Checks) {
        if ($check -isnot [Collections.IDictionary]) { throw 'Each check must be an object.' }
        Require-Fields $check @('id','command','exitCode','logPath')
        foreach ($field in @('id','command','logPath')) { if ($check[$field] -isnot [string]) { throw "Check $field must be a string." } }
        if (-not $ids.Add($check.id)) { throw "Duplicate check ID: $($check.id)" }
        if (($check.exitCode -isnot [int] -and $check.exitCode -isnot [long]) -or $check.exitCode -ne 0) { throw "Check $($check.id) did not report integer exitCode 0. Record failures separately." }
        $log=Resolve-WorkspacePath $root $check.logPath
        if (-not (Test-Path -LiteralPath $log -PathType Leaf) -or (Get-Item -LiteralPath $log).Length -eq 0) { throw "Check log missing or empty: $($check.logPath)" }
    }
}
function Save-State {
    # One orchestrator owns feature evidence. Keep prior state recoverable; publish atomically.
    $stamp=[Guid]::NewGuid().ToString('N')
    if (Test-Path -LiteralPath $statePath) {
        $archive=Resolve-WorkspacePath $root "$base/history/$stamp.json"
        Write-Utf8 $archive (Read-Utf8 $statePath)
    }
    $pending=Resolve-WorkspacePath $root "$base/$stamp.pending"
    Write-Json $pending $state
    [IO.File]::Move($pending,$statePath,$true)
}
function Invalidate([string[]]$Stages,[string]$Reason) {
    foreach ($name in $Stages) {
        if ($state.stages.Contains($name)) { $state.stages[$name].stale=$true; $state.stages[$name].staleReason=$Reason }
    }
}
function Head {
    $result=Invoke-Captured 'git' @('-c',"safe.directory=$root",'-C',$root,'rev-parse','HEAD') $root 30
    if ($result.exitCode -eq 0) { return $result.stdout.Trim() }
    return $null
}
$stale=@(Get-SddStaleStages $root $Feature $state)
switch ($Action) {
    'handoff' {
        Require-Fields $request @('role','runtime','mode','language','ownedPaths','sourcePaths','planSections','literalApis','outcome','changedFiles','checks','unresolved','nextAction')
        if ($request.runtime -notin @('claude','codex')) { throw 'Unknown runtime.' }
        if ($request.mode -notin @('lite','full','ultra','wenyan-lite','wenyan-full','wenyan-ultra','off')) { throw 'Unknown communication mode.' }
        foreach ($owned in $request.ownedPaths) { Resolve-WorkspacePath $root $owned | Out-Null }
        $rolePath=".sdd/roles/$($request.role).md"
        $catalog=Read-Utf8 (Join-Path $root '.sdd/catalog.json') | ConvertFrom-Json -AsHashtable
        if ($request.role -notin @($catalog.roles.id)) { throw 'Handoff role must exist in core catalog.' }
        $purpose=if($request.Contains('purpose')){[string]$request.purpose}else{''}
        $upstream=Get-SddHandoffUpstream $request.role $purpose
        $required=@($script:SddStages | Select-Object -First ([Array]::IndexOf($script:SddStages,$upstream)+1))
        $missing=@($required | Where-Object {-not $state.stages.Contains($_)})
        if ($missing.Count) { throw "Missing upstream evidence: $($missing -join ', '). Run producing checks and record before delegation." }
        $relevant=@($stale | Where-Object { [Array]::IndexOf($script:SddStages,$_) -le [Array]::IndexOf($script:SddStages,$upstream) })
        if ($relevant.Count) { throw "Stale upstream evidence: $($relevant -join ', '). Revalidate before delegation." }
        $paths=@($request.sourcePaths)+@(".specs/$Feature/spec.md",".specs/$Feature/plan.md",'.sdd/contracts',$rolePath,".sdd/adapters/$($request.runtime)/runtime.md")
        foreach ($path in $paths) { if (-not (Test-Path -LiteralPath (Resolve-WorkspacePath $root $path))) { throw "Missing handoff source: $path" } }
        $id=[Guid]::NewGuid().ToString('N')
        Write-Json (Resolve-WorkspacePath $root "$base/handoffs/$id.json") ([ordered]@{ schemaVersion=1; feature=$Feature; id=$id; createdUtc=[DateTime]::UtcNow.ToString('o'); gitHead=(Head); paths=$paths; sources=(Get-SddInputSnapshot $root $paths); payload=$request })
        Write-Output "$base/handoffs/$id.json"
    }
    'check-handoff' {
        if ($Id -notmatch '\A[a-f0-9]{32}\z') { throw 'Expected handoff ID.' }
        $handoff=Read-Utf8 (Resolve-WorkspacePath $root "$base/handoffs/$Id.json") | ConvertFrom-Json -AsHashtable
        if ($handoff.schemaVersion -ne 1 -or $handoff.feature -cne $Feature -or $handoff.id -cne $Id) { throw 'Invalid handoff identity.' }
        $purpose=if($handoff.payload.Contains('purpose')){[string]$handoff.payload.purpose}else{''}
        $upstream=Get-SddHandoffUpstream $handoff.payload.role $purpose
        $required=@($script:SddStages | Select-Object -First ([Array]::IndexOf($script:SddStages,$upstream)+1))
        if (@($required | Where-Object {-not $state.stages.Contains($_)}).Count) { throw 'Missing upstream evidence. Reestablish producing checks before continuation.' }
        $relevant=@($stale | Where-Object { [Array]::IndexOf($script:SddStages,$_) -le [Array]::IndexOf($script:SddStages,$upstream) })
        if ($relevant.Count -or -not (Test-SddSnapshot $handoff.sources (Get-SddInputSnapshot $root @($handoff.paths)))) { throw 'Stale handoff. Reread changed sources, reconcile APIs, rerun affected checks, and create a new handoff.' }
        Write-Output 'Handoff source hashes match. Verify live capabilities and ownership before work.'
    }
    'check' {
        $required=if($Stage){@($script:SddStages | Select-Object -First ([Array]::IndexOf($script:SddStages,$Stage)+1))}else{$script:SddStages}
        $missing=@($required | Where-Object {-not $state.stages.Contains($_)})
        if ($missing.Count) { throw "Missing stage evidence: $($missing -join ', '). Unrecorded stages have no hash-backed proof." }
        if ($Stage) {
            if (-not $state.stages.Contains($Stage)) { throw "No recorded evidence for $Stage." }
            $stale=@($stale | Where-Object { [Array]::IndexOf($script:SddStages,$_) -le [Array]::IndexOf($script:SddStages,$Stage) })
        }
        if ($stale.Count) { throw "Stale evidence: $($stale -join ', '). Rerun producing stages; do not trust ledger success." }
        Write-Output "Evidence current. Recorded stages: $($state.stages.Keys -join ', '). Unrecorded stages have no hash-backed proof."
    }
    'record' {
        if (-not $Stage) { throw 'record requires -Stage.' }
        Require-Fields $request @('outcome','checks','sourcePaths')
        Validate-Checks $request.checks
        if ($request.Contains('purpose') -and $request.purpose -ceq 'implementation-tests') { throw 'Implementation-test handoffs cannot record a workflow stage. Run separate producing workflow checks.' }
        $resolutions=@()
        if ($request.Contains('resolveFailures')) { $resolutions=$request.resolveFailures }
        if ($resolutions -isnot [Collections.IList]) { throw 'resolveFailures must be an array.' }
        $openFailures=@($state.failures | Where-Object { $_.report.stage -ceq $Stage -and (-not $_.Contains('resolved') -or -not $_.resolved) })
        $resolvedIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($resolution in $resolutions) {
            if ($resolution -isnot [Collections.IDictionary]) { throw 'Each failure resolution must be an object.' }
            Require-Fields $resolution @('id','reason','checkIds')
            if ($resolution.id -isnot [string] -or $resolution.reason -isnot [string]) { throw 'Failure resolution id and reason must be strings.' }
            if (-not $resolvedIds.Add($resolution.id) -or $resolution.id -cnotin @($openFailures.id)) { throw 'Failure resolution must identify a distinct unresolved failure in this stage.' }
            if ($resolution.checkIds -isnot [Collections.IList] -or $resolution.checkIds.Count -eq 0) { throw 'Failure resolution needs successful checkIds.' }
            foreach ($checkId in $resolution.checkIds) { if ($checkId -isnot [string] -or $checkId -cnotin @($request.checks.id)) { throw "Unknown failure-resolution check ID: $checkId" } }
        }
        if (@($openFailures | Where-Object {-not $resolvedIds.Contains($_.id)}).Count) { throw 'Unresolved stage failures require explicit resolveFailures entries and successful check evidence.' }
        $required=@($script:SddStages | Select-Object -First ([Array]::IndexOf($script:SddStages,$Stage)))
        $missing=@($required | Where-Object {-not $state.stages.Contains($_)})
        if ($missing.Count) { throw "Record upstream evidence first: $($missing -join ', ')." }
        $earlier=@($stale | Where-Object { [Array]::IndexOf($script:SddStages,$_) -lt [Array]::IndexOf($script:SddStages,$Stage) })
        if ($earlier.Count) { throw "Revalidate upstream first: $($earlier -join ', ')." }
        # Capture actual deterministic gate results; caller's checks retain additional evidence, never substitute for these.
        $modes=switch($Stage){'spec'{@('spec')};'plan'{@('spec','plan')};'implement'{@('spec','plan')};'test'{@('manifest','compile')};'verify'{@('e2e')};'review'{@('manifest')};'document'{@('spec','plan')}}
        if ($Stage -eq 'verify' -and $request.Contains('disposition') -and $request.disposition -ceq 'skipped') {
            Require-Fields $request @('skipReason')
            $unitManifest=Read-Utf8 (Resolve-WorkspacePath $root ".specs/$Feature/test-manifest.json") | ConvertFrom-Json -AsHashtable
            if (@($unitManifest.acceptanceCriteria | Where-Object { $_.Contains('proof') -and $_.proof -in @('e2e','manual') }).Count) {
                throw 'Cannot skip Verify while deferred acceptance proof remains.'
            }
            $modes=@('spec','plan')
        }
        $paths=(Get-SddStagePaths $Feature $Stage)+@($request.sourcePaths)+@($request.checks.logPath)
        $excluded=@(Get-SddStageExclusions $Stage)
        $before=Get-SddInputSnapshot $root $paths $excluded
        $receipts=@(foreach ($mode in $modes) {
            $run=Invoke-Captured (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/check-sdd-gates.ps1'),$mode,'-Feature',$Feature) $root
            if ($run.exitCode -ne 0) { throw "Gate $mode failed: $($run.stdout) $($run.stderr)" }
            @{ mode=$mode; exitCode=$run.exitCode; stdout=$run.stdout; stderr=$run.stderr }
        })
        if (-not (Test-SddSnapshot $before (Get-SddInputSnapshot $root $paths $excluded))) { throw 'Inputs changed during gates. Rerun with stable sources.' }
        if ($Stage -in @('spec','plan')) {
            $artifact=".specs/$Feature/$Stage.md"
            $retained=Resolve-WorkspacePath $root "$base/artifacts/$($before[$artifact])/$Stage.md"
            if (Test-Path -LiteralPath $retained) {
                if ((Get-SddFileHash $retained) -cne $before[$artifact]) { throw 'Retained artifact was modified; restore original before recording.' }
            } else {
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($retained)) | Out-Null
                [IO.File]::Copy((Resolve-WorkspacePath $root $artifact),$retained)
            }
        }
        Invalidate (Get-SddDescendants $Stage) "Upstream stage $Stage rerecorded; inspect downstream evidence."
        $state.stages[$Stage]=[ordered]@{ recordedUtc=[DateTime]::UtcNow.ToString('o'); gitHead=(Head); paths=$paths; excludedDirectories=$excluded; sources=$before; stale=$false; staleReason=$null; gateReceipts=$receipts; report=$request }
        foreach ($failure in $openFailures) {
            $failure.resolved=$true
            $failure.resolution=@($resolutions | Where-Object {$_.id -ceq $failure.id})[0]
            $failure.resolvedUtc=[DateTime]::UtcNow.ToString('o')
        }
        Save-State
        Write-Output "Recorded $Stage hashes and gate receipts. Existing workflow gates and human approvals remain required."
    }
    'failure' {
        Require-Fields $request @('classification','stage','evidence','reason','nextAction')
        if ($request.classification -notin @('implementation-defect','test-defect','requirement-gap','environment-problem')) { throw 'Unknown failure classification.' }
        if ($request.stage -notin $script:SddStages) { throw 'Unknown failure stage.' }
        $state.failures+=@{ id=[Guid]::NewGuid().ToString('N'); createdUtc=[DateTime]::UtcNow.ToString('o'); resolved=$false; report=$request }
        Invalidate (@($request.stage)+(Get-SddDescendants $request.stage)) "Failure: $($request.classification)"
        Save-State
        Write-Output 'Failure recorded. Classification changes no requirement or approval.'
    }
    'amend' {
        Require-Fields $request @('target','reason','decision','changedIds','beforeHash','afterHash','evidence')
        if ($request.target -notin @('spec','plan')) { throw 'Amendment target must be spec or plan.' }
        $target=".specs/$Feature/$($request.target).md"
        if ($request.beforeHash -notmatch '\A[a-f0-9]{64}\z' -or $request.afterHash -notmatch '\A[a-f0-9]{64}\z' -or $request.beforeHash -ceq $request.afterHash) { throw 'Amendment needs distinct before/after SHA-256 hashes.' }
        if ((Get-SddFileHash (Resolve-WorkspacePath $root $target)) -cne $request.afterHash) { throw 'Amendment afterHash does not match current artifact.' }
        if (-not $state.stages.Contains($request.target) -or $state.stages[$request.target].sources[$target] -cne $request.beforeHash) { throw 'Amendment beforeHash must match previously recorded artifact.' }
        $retained=Resolve-WorkspacePath $root "$base/artifacts/$($request.beforeHash)/$($request.target).md"
        if (-not (Test-Path -LiteralPath $retained) -or (Get-SddFileHash $retained) -cne $request.beforeHash) { throw 'Original amendment artifact missing or modified.' }
        $state.amendments+=@{ id=[Guid]::NewGuid().ToString('N'); createdUtc=[DateTime]::UtcNow.ToString('o'); report=$request }
        Invalidate (@($request.target)+(Get-SddDescendants $request.target)) "Amended $($request.target): $($request.changedIds -join ', ')"
        Save-State
        Write-Output 'Amendment recorded; target and downstream evidence stale. Reconcile artifact status and rerun stages in order.'
    }
}
