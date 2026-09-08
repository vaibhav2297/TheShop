[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),[switch]$Published)
. (Join-Path $PSScriptRoot 'Extensions.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
$work=Resolve-WorkspacePath $root ('.sdd/.test-work/extensions-'+[Guid]::NewGuid().ToString('N'))
$fixture=Join-Path $work 'repo'
$checks=[Collections.Generic.List[object]]::new()
function Assert([string]$Name,[bool]$Passed,[string]$Evidence='') {
    $checks.Add(@{name=$Name;passed=$Passed;evidence=$Evidence})
    Write-Output "[$(if($Passed){'PASS'}else{'FAIL'})] $Name"
    if (-not $Passed) { throw "Check failed: $Name`n$Evidence" }
}
function Run([string]$Action,[string[]]$Extra=@()) {
    return Invoke-Captured $pwsh (@('-NoProfile','-File',(Join-Path $root '.sdd/scripts/manage-extensions.ps1'),'-RepositoryRoot',$fixture,'-Action',$Action)+$Extra) $root
}
function Snapshot {
    $files=[ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $fixture -Recurse -File -Force | Where-Object {
        $relative=[IO.Path]::GetRelativePath($fixture,$_.FullName).Replace('\','/')
        $relative -notmatch '^\.sdd/(\.(stage|test-work)/|extensions/history/|extensions/\.mutation-lock$)'
    }) {
        $files[[IO.Path]::GetRelativePath($fixture,$file.FullName)]=(Get-FileHash -LiteralPath $file.FullName).Hash
    }
    if ($files.Count -lt 180) { throw 'Snapshot omitted fixture files.' }
    return $files | ConvertTo-Json -Compress
}
function Package([string]$Kind,[string]$Id,[string]$Version) {
    $path=Join-Path $work "$Id-$Version"
    Write-Utf8 (Join-Path $path 'upstream/source.md') "---`nname: vendor`ndescription: >`n  Preserve multiline upstream YAML.`n---`nKeep exact literal: ``{{tool:vendor.literal}}``. Version $Version.`r`n"
    Write-Utf8 (Join-Path $path 'upstream/LICENSE') 'Local test fixture; CC0-1.0.'
    Write-Utf8 (Join-Path $path 'upstream/nested/SKILL.md') "---`nname: vendor-nested`ndescription: Vendor metadata must not enter native discovery.`n---`nRead ../source.md.`n"
    [IO.File]::WriteAllBytes((Join-Path $path 'upstream/asset.bin'),[byte[]]@(0,255,13,10,128,42))
    $hashes=[ordered]@{}
    foreach ($file in Get-ChildItem (Join-Path $path 'upstream') -File -Recurse) { $hashes[[IO.Path]::GetRelativePath((Join-Path $path 'upstream'),$file.FullName).Replace('\','/')]=(Get-FileHash $file.FullName).Hash.ToLowerInvariant() }
    $manifest=[ordered]@{schemaVersion=1;id=$Id;kind=$Kind;description="Inspect $Id fixture.";invocation='automatic';placement='standalone';scope=@{readOnly=$true;writes=@()};upstream=@{url='https://example.invalid/test-fixture';revision=('a'*40);license='CC0-1.0';entry='source.md';files=$hashes};capabilities=@('files.read');adapters=@{claude=@{supported=$true;bindings=@{'files.read'='Read'}};codex=@{supported=$true;bindings=@{'files.read'='native file reader'}}};adaptations='Use shared policy; fixture has no external effects.'}
    Write-Json (Join-Path $path 'extension.json') $manifest
    $body='Read `{{extension:upstream}}`. Report version; preserve literals.'
    if ($Kind -eq 'skill') { Write-Utf8 (Join-Path $path 'SKILL.md') "---`nname: $Id`ndescription: Inspect fixture.`n---`n$body`n" }
    else { Write-Utf8 (Join-Path $path 'ROLE.md') $body }
    return $path
}
[IO.Directory]::CreateDirectory((Join-Path $fixture '.sdd')) | Out-Null
foreach ($directory in @('contracts','skills','roles','adapters','scripts')) { Copy-Item (Join-Path $root ".sdd/$directory") (Join-Path $fixture '.sdd') -Recurse }
foreach ($relative in @('.sdd/catalog.json','.sdd/baseline/inventory.json')) {
    $to=Resolve-WorkspacePath $fixture $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null
    Copy-Item (Join-Path $root $relative) $to
}
$generation=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-RepositoryRoot',$fixture) $root
Assert 'Existing catalog generates before onboarding' ($generation.exitCode -eq 0) $generation.stderr
$legacyManifest=Read-Utf8 (Join-Path $fixture '.sdd/generated-files.json') | ConvertFrom-Json -AsHashtable
$legacyManifest.schemaVersion=1
Write-Json (Join-Path $fixture '.sdd/generated-files.json') $legacyManifest
$legacyPath=Join-Path $fixture '.claude/skills/theshop-spec/SKILL.md'
[IO.File]::WriteAllText($legacyPath,(Read-Utf8 $legacyPath).Replace("`n","`r`n"),[Text.UTF8Encoding]::new($false))
$skill=Package 'skill' 'extension-probe' 'v1'
$agent=Package 'agent' 'extension-reviewer' 'v1'
$before=Snapshot
$preview=Run 'Install' @('-PackagePath',$skill)
Assert 'Install preview leaves managed state unchanged' ($preview.exitCode -eq 0 -and (Snapshot) -ceq $before) ($preview.stdout+$preview.stderr)
$install=Run 'Install' @('-PackagePath',$skill,'-Apply')
Assert 'Alias-free skill installs into both clients' ($install.exitCode -eq 0 -and (Test-Path (Join-Path $fixture '.agents/skills/extension-probe/SKILL.md')) -and (Test-Path (Join-Path $fixture '.claude/skills/extension-probe/SKILL.md'))) ($install.stdout+$install.stderr)
Assert 'Legacy manifest migrates to versioned text/asset ownership' ((Read-Utf8 (Join-Path $fixture '.sdd/generated-files.json') | ConvertFrom-Json).schemaVersion -eq 2)
$revision=(Get-ExtensionPackage $skill).revision
$vendor=Join-Path $fixture ".sdd/extensions/packages/extension-probe/$revision/upstream"
Assert 'Vendor multiline YAML, literals, and binary assets survive byte-for-byte' ((Get-FileHash (Join-Path $vendor 'source.md')).Hash -ceq (Get-FileHash (Join-Path $skill 'upstream/source.md')).Hash -and (Get-FileHash (Join-Path $vendor 'asset.bin')).Hash -ceq (Get-FileHash (Join-Path $skill 'upstream/asset.bin')).Hash)
foreach ($runtimeRoot in @('.agents/skills','.claude/skills')) {
    $nativeRoot=Join-Path $fixture "$runtimeRoot/extension-probe"
    Assert "Vendor files stay outside discovery root: $runtimeRoot" (-not (Test-Path (Join-Path $nativeRoot 'upstream')))
    $entries=@(Get-ChildItem -LiteralPath $nativeRoot -Recurse -File -Filter SKILL.md)
    Assert "Only wrapper is discoverable and points to pinned entry: $runtimeRoot" ($entries.Count -eq 1 -and (Read-Utf8 $entries[0].FullName).Contains(".sdd/extensions/packages/extension-probe/$revision/upstream/source.md"))
}
Assert 'Vendor nested skill and its relative dependency remain intact' ((Get-FileHash (Join-Path $vendor 'nested/SKILL.md')).Hash -ceq (Get-FileHash (Join-Path $skill 'upstream/nested/SKILL.md')).Hash -and (Test-Path (Join-Path $vendor 'nested/../source.md')))
$before=Snapshot
$again=Run 'Install' @('-PackagePath',$skill,'-Apply')
Assert 'Repeated install is idempotent' ($again.exitCode -eq 0 -and (Snapshot) -ceq $before) $again.stderr
# Simulate copies owned by the previous generator; manager must retire them safely.
$legacyCopies=@('.agents/skills/extension-probe/upstream/SKILL.md','.claude/skills/extension-probe/upstream/SKILL.md')
$owned=Read-Utf8 (Join-Path $fixture '.sdd/generated-files.json') | ConvertFrom-Json -AsHashtable
foreach ($relative in $legacyCopies) {
    $target=Resolve-WorkspacePath $fixture $relative
    Write-Utf8 $target (Read-Utf8 (Join-Path $vendor 'nested/SKILL.md'))
    $owned.files[$relative]=(Get-FileHash $target).Hash.ToLowerInvariant()
    $owned.rawFiles += $relative
}
Write-Json (Join-Path $fixture '.sdd/generated-files.json') $owned
$legacyEdit=Resolve-WorkspacePath $fixture $legacyCopies[0]
$legacySaved=[IO.File]::ReadAllBytes($legacyEdit)
Write-Utf8 $legacyEdit 'Independent vendor edit.'
$before=Snapshot; $retirementBlocked=Run 'Update' @('-PackagePath',$skill,'-Apply')
Assert 'Independent vendor edit blocks retirement before writes' ($retirementBlocked.exitCode -ne 0 -and $retirementBlocked.stderr.Contains('Independent generated edit') -and (Snapshot) -ceq $before) $retirementBlocked.stderr
[IO.File]::WriteAllBytes($legacyEdit,$legacySaved)
$personal=Join-Path $fixture '.agents/skills/extension-probe/upstream/personal.txt'
Write-Utf8 $personal 'Preserve unrelated file.'
$before=Snapshot; $retirementPreview=Run 'Update' @('-PackagePath',$skill)
Assert 'Same-revision retirement preview preserves working state' ($retirementPreview.exitCode -eq 0 -and (Snapshot) -ceq $before) $retirementPreview.stderr
$retirement=Run 'Update' @('-PackagePath',$skill,'-Apply')
$remaining=@($legacyCopies | Where-Object { Test-Path (Join-Path $fixture $_) })
Assert 'Same-revision update retires owned copies and preserves unrelated files and pin' ($retirement.exitCode -eq 0 -and $remaining.Count -eq 0 -and (Read-Utf8 $personal) -ceq 'Preserve unrelated file.' -and (Get-ExtensionPackage (Split-Path $vendor)).revision -ceq $revision) $retirement.stderr
$role=Run 'Install' @('-PackagePath',$agent,'-Apply')
$native=Read-Utf8 (Join-Path $fixture '.codex/agents/extension-reviewer.toml')
Assert 'Agent installs with read-only scope and shared communication' ($role.exitCode -eq 0 -and $native.Contains('sandbox_mode = "read-only"') -and $native.Contains('.sdd/contracts/communication.md')) $role.stderr
$v2=Package 'skill' 'extension-probe' 'v2'
$edit=Join-Path $fixture '.agents/skills/extension-probe/SKILL.md'
$saved=Read-Utf8 $edit
Write-Utf8 $edit ($saved+"`nIndependent edit.`n")
$before=Snapshot
$blocked=Run 'Update' @('-PackagePath',$v2,'-Apply')
Assert 'Independent generated edit blocks update before writes' ($blocked.exitCode -ne 0 -and (Snapshot) -ceq $before) $blocked.stderr
Write-Utf8 $edit $saved
$update=Run 'Update' @('-PackagePath',$v2,'-Apply')
$v2Revision=(Get-ExtensionPackage $v2).revision
$vendor=Join-Path $fixture ".sdd/extensions/packages/extension-probe/$v2Revision/upstream"
Assert 'Update publishes changed vendor content' ($update.exitCode -eq 0 -and (Read-Utf8 (Join-Path $vendor 'source.md')).Contains('Version v2')) $update.stderr
$rollback=Run 'Rollback' @('-Id','extension-probe','-Revision',$revision,'-Apply')
$vendor=Join-Path $fixture ".sdd/extensions/packages/extension-probe/$revision/upstream"
Assert 'Version rollback restores original vendor bytes' ($rollback.exitCode -eq 0 -and (Get-FileHash (Join-Path $vendor 'source.md')).Hash -ceq (Get-FileHash (Join-Path $skill 'upstream/source.md')).Hash) $rollback.stderr
Write-Utf8 (Join-Path $fixture '.agents/skills/extension-probe/personal.txt') 'Preserve unrelated file.'
$remove=Run 'Remove' @('-Id','extension-probe','-Apply')
Assert 'Removal retires owned native files and keeps unrelated files and retained revision' ($remove.exitCode -eq 0 -and -not (Test-Path $edit) -and (Test-Path (Join-Path $fixture '.agents/skills/extension-probe/personal.txt')) -and (Test-Path (Join-Path $fixture ".sdd/extensions/packages/extension-probe/$revision/SKILL.md"))) $remove.stderr
$restore=Run 'Rollback' @('-Id','extension-probe','-Revision',$revision,'-Apply')
Assert 'Removed extension can be restored' ($restore.exitCode -eq 0 -and (Test-Path $edit)) $restore.stderr
$callerPath=Join-Path $fixture '.sdd/skills/theshop-spec/SKILL.md'; $callerSaved=Read-Utf8 $callerPath
Write-Utf8 $callerPath ($callerSaved+"`nInvoke extension-probe for this fixture.`n")
$before=Snapshot; $referenced=Run 'Remove' @('-Id','extension-probe','-Apply')
Assert 'Removal refuses a referenced pipeline dependency' ($referenced.exitCode -ne 0 -and $referenced.stderr.Contains('Reconcile caller') -and (Snapshot) -ceq $before) $referenced.stderr
Write-Utf8 $callerPath $callerSaved
$unowned=Package 'skill' 'extension-collision' 'v1'
Write-Utf8 (Join-Path $fixture '.agents/skills/extension-collision/SKILL.md') 'Independent user skill.'
$before=Snapshot; $unownedResult=Run 'Install' @('-PackagePath',$unowned,'-Apply')
Assert 'Unowned native definition cannot be overwritten' ($unownedResult.exitCode -ne 0 -and $unownedResult.stderr.Contains('Unowned native path collision') -and (Snapshot) -ceq $before) $unownedResult.stderr
$pipeline=Package 'agent' 'extension-pipeline' 'v1'
$pipelineManifest=Read-Utf8 (Join-Path $pipeline 'extension.json') | ConvertFrom-Json -AsHashtable
$pipelineManifest.placement='pipeline'; $pipelineManifest.caller='.sdd/skills/missing/SKILL.md'; Write-Json (Join-Path $pipeline 'extension.json') $pipelineManifest
$before=Snapshot; $pipelineResult=Run 'Install' @('-PackagePath',$pipeline,'-Apply')
Assert 'Pipeline registration requires a real caller reference' ($pipelineResult.exitCode -ne 0 -and $pipelineResult.stderr.Contains('Pipeline caller must explicitly invoke') -and (Snapshot) -ceq $before) $pipelineResult.stderr
$bad=Package 'skill' 'extension-bad' 'v1'
$badManifest=Join-Path $bad 'extension.json'; $m=Read-Utf8 $badManifest | ConvertFrom-Json -AsHashtable
$m.id='Extension-bad'; Write-Json $badManifest $m
$mixedCase=Run 'Inspect' @('-PackagePath',$bad)
Assert 'Mixed-case extension IDs rejected before publication' ($mixedCase.exitCode -ne 0 -and $mixedCase.stderr.Contains('Invalid extension schema or ID')) $mixedCase.stderr
$m.id="extension-bad`n"; Write-Json $badManifest $m
$newlineId=Run 'Inspect' @('-PackagePath',$bad)
Assert 'Newline-containing extension IDs rejected before publication' ($newlineId.exitCode -ne 0 -and $newlineId.stderr.Contains('Invalid extension schema or ID')) $newlineId.stderr
$m.id='extension-bad'
$m.adapters.codex.supported=$false; Write-Json $badManifest $m
$before=Snapshot; $unsupported=Run 'Install' @('-PackagePath',$bad,'-Apply')
Assert 'Unsupported runtime blocks activation with evidence' ($unsupported.exitCode -ne 0 -and $unsupported.stderr.Contains('Unsupported runtime: codex') -and (Snapshot) -ceq $before) $unsupported.stderr
$m.adapters.codex.supported=$true; $m.adapters.claude.bindings.Remove('files.read'); Write-Json $badManifest $m
$missing=Run 'Inspect' @('-PackagePath',$bad)
Assert 'Missing capability binding rejected' ($missing.exitCode -ne 0 -and $missing.stderr.Contains('Missing claude capability binding')) $missing.stderr
$m.adapters.claude.bindings['files.read']='Read'; $m.hooks=@{start='download and execute'}; Write-Json $badManifest $m
$hooks=Run 'Inspect' @('-PackagePath',$bad)
Assert 'Unadapted hooks cannot silently enter runtime settings' ($hooks.exitCode -ne 0 -and $hooks.stderr.Contains('Unsupported extension field: hooks')) $hooks.stderr
$m.Remove('hooks'); $m.upstream.files['source.md']='0'*64; Write-Json $badManifest $m
$tamper=Run 'Inspect' @('-PackagePath',$bad)
Assert 'Upstream hash mismatch rejected' ($tamper.exitCode -ne 0 -and $tamper.stderr.Contains('Upstream hash mismatch')) $tamper.stderr
$collision=Package 'skill' 'theshop-spec' 'v1'; $before=Snapshot
$duplicate=Run 'Install' @('-PackagePath',$collision,'-Apply')
Assert 'Existing workflow identity cannot be shadowed' ($duplicate.exitCode -ne 0 -and (Snapshot) -ceq $before) $duplicate.stderr
$escape=$false; try { Resolve-WorkspacePath $fixture '../outside.txt' | Out-Null } catch { $escape=$true }
Assert 'Retirement and recovery paths stay inside workspace' $escape
# Simulate process termination after one transaction write. Recovery must preserve independent edits.
$transactionId=[Guid]::NewGuid().ToString('N'); $transactionPath=Join-Path $fixture ".sdd/extensions/history/$transactionId.json"
$bytes=[IO.File]::ReadAllBytes($edit); $beforeState=[Convert]::ToBase64String($bytes)
$afterState=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('Interrupted transaction.'))
$record=@{state='pending';changes=@(@{path='.agents/skills/extension-probe/SKILL.md';before=$beforeState;after=$afterState})}
Write-Json $transactionPath $record; Write-Utf8 $edit 'Interrupted transaction.'
$blockedPending=Run 'Remove' @('-Id','extension-probe','-Apply')
Assert 'Interrupted transaction blocks later mutations' ($blockedPending.exitCode -ne 0 -and $blockedPending.stderr.Contains('Recover incomplete transaction')) $blockedPending.stderr
Write-Utf8 $edit 'User edit after interruption.'
$recoveryBlocked=Run 'Recover' @('-Transaction',$transactionId,'-Apply')
Assert 'Recovery rejects independent edits' ($recoveryBlocked.exitCode -ne 0 -and (Read-Utf8 $edit) -ceq 'User edit after interruption.') $recoveryBlocked.stderr
Write-Utf8 $edit 'Interrupted transaction.'
$recovery=Run 'Recover' @('-Transaction',$transactionId,'-Apply')
Assert 'Recovery restores pre-transaction bytes' ($recovery.exitCode -eq 0 -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($edit)) -ceq $beforeState) $recovery.stderr
$check=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-RepositoryRoot',$fixture,'-Check') $root
Assert 'Updated registry remains compatible with normal generation' ($check.exitCode -eq 0) ($check.stdout+$check.stderr)
[IO.File]::WriteAllText($edit,(Read-Utf8 $edit).Replace("`n","`r`n"),[Text.UTF8Encoding]::new($false))
$lineEndingCheck=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-RepositoryRoot',$fixture,'-Check') $root
Assert 'Text ownership tolerates checkout line-ending conversion' ($lineEndingCheck.exitCode -eq 0) ($lineEndingCheck.stdout+$lineEndingCheck.stderr)
$git=(Get-Command git).Source
$gitInit=Invoke-Captured $git @('init','--quiet',$fixture) $root
Assert 'Git checkout fixture initializes in isolation' ($gitInit.exitCode -eq 0) $gitInit.stderr
Copy-Item (Join-Path $root '.sdd/extensions/.gitattributes') (Join-Path $fixture '.sdd/extensions/.gitattributes')
$rawPaths=@(".sdd/extensions/packages/extension-probe/$revision/upstream/source.md",".sdd/extensions/packages/extension-probe/$revision/upstream/nested/SKILL.md",".sdd/extensions/packages/extension-probe/$revision/upstream/asset.bin")
$rawBefore=@{}; foreach($path in $rawPaths){$rawBefore[$path]=(Get-FileHash (Join-Path $fixture $path)).Hash}
$gitAdd=Invoke-Captured $git (@('-C',$fixture,'-c','core.autocrlf=true','add','--','.sdd/extensions/.gitattributes')+$rawPaths) $root
$gitCheckout=Invoke-Captured $git @('-C',$fixture,'-c','core.autocrlf=true','checkout-index','--force','--all') $root
$different=@($rawPaths | Where-Object {(Get-FileHash (Join-Path $fixture $_)).Hash -cne $rawBefore[$_]})
Assert 'Windows Git checkout preserves pinned vendor and binary bytes' ($gitAdd.exitCode -eq 0 -and $gitCheckout.exitCode -eq 0 -and $different.Count -eq 0) ($gitAdd.stderr+$gitCheckout.stderr)
$spawnEntry=@{type='response_item';payload=@{type='function_call';name='spawn_agent';call_id='probe';arguments=(@{task_name='probe';agent_type='sdd-cavecrew-reviewer'} | ConvertTo-Json -Compress)}} | ConvertTo-Json -Depth 8 -Compress
$replyEntry=@{type='response_item';payload=@{type='agent_message';author='/root/probe';content=@(@{type='input_text';text='Message Type: FINAL_ANSWER'})}} | ConvertTo-Json -Depth 8 -Compress
Assert 'Spawn request alone is not delegation completion' (-not (Get-ExtensionDelegationEvidence $spawnEntry).passed)
Assert 'Matched native child return proves delegation' ((Get-ExtensionDelegationEvidence ($spawnEntry+"`n"+$replyEntry)).passed)
if ($Published) {
    $catalog=Get-SddCatalog $root
    foreach ($item in @($catalog.skills)+@($catalog.roles) | Where-Object { $_.ContainsKey('extension') }) {
        $package=Get-ExtensionPackage (Join-Path $root $item.package)
        Assert "Published upstream pin: $($item.id)" ($package.revision -ceq (Split-Path $item.package -Leaf))
        if ($item.extension.kind -eq 'skill') {
            foreach ($runtimeRoot in @('.agents/skills','.claude/skills')) {
                $entries=@(Get-ChildItem -LiteralPath (Join-Path $root "$runtimeRoot/$($item.id)") -Recurse -File -Filter SKILL.md)
                Assert "Published discovery exposes wrapper only: $runtimeRoot/$($item.id)" ($entries.Count -eq 1 -and (Read-Utf8 $entries[0].FullName).Contains("$($item.package)/upstream/$($item.extension.upstream.entry)"))
            }
        }
    }
}
Write-Json (Join-Path $root '.sdd/reports/extension-static.json') @{passed=$true;checkedUtc=[DateTime]::UtcNow.ToString('o');checks=@($checks.ToArray());fixture=$fixture}
Write-Output "[extensions] $($checks.Count)/$($checks.Count) checks passed."
