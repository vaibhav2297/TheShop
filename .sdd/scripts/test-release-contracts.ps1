[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'Test-Proof.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed,[string]$Detail='') {
    $checks.Add(@{name=$Name;passed=$Passed;detail=$Detail})
    if (-not $Passed) { throw "$Name failed: $Detail" }
}
function Proof([string]$Json,[string]$Browser='null') {
    @(Get-TestProofIssues ($Json | ConvertFrom-Json) ($Browser | ConvertFrom-Json))
}
$legacy='{"acceptanceCriteria":[{"id":"AC-1","tests":[]}]}'
$deferred='{"acceptanceCriteria":[{"id":"AC-1","tests":[],"proof":"e2e","reason":"Real navigation crosses mocked router."}]}'
Check 'Legacy empty unit mapping stays structurally valid; runner must report Not Covered' (@(Proof $legacy).Count -eq 0)
Check 'Justified browser deferral accepted structurally' (@(Proof $deferred).Count -eq 0)
Check 'Unexplained deferral rejected' (@(Proof '{"acceptanceCriteria":[{"id":"AC-1","tests":[],"proof":"manual"}]}').Count -eq 1)
Check 'Unknown classification rejected' (@(Proof ($deferred.Replace('"e2e"','"skip"'))).Count -eq 1)
Check 'Duplicate IDs rejected' (@(Proof '{"acceptanceCriteria":[{"id":"AC-1","tests":[]},{"id":"AC-1","tests":[]}]}').Count -eq 1)
Check 'Supporting unit tests cannot discharge browser deferral' (@(Proof $deferred '{"acceptanceCriteria":[{"id":"AC-1","coverage":"unit"}]}').Count -eq 1)
Check 'Matching browser classification accepted' (@(Proof $deferred '{"acceptanceCriteria":[{"id":"AC-1","coverage":"e2e"}]}').Count -eq 0)
Check 'Missing deferred browser ID rejected' (@(Proof $deferred '{"acceptanceCriteria":[]}').Count -eq 1)
Check 'Human deferral may receive automated browser proof' (@(Proof ($deferred.Replace('"e2e"','"manual"')) '{"acceptanceCriteria":[{"id":"AC-1","coverage":"e2e"}]}').Count -eq 0)
$work=Resolve-WorkspacePath $root ('.sdd/.test-work/release-contracts-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
$pwsh=(Get-Process -Id $PID).Path
$git=Invoke-Captured 'git' @('init','--quiet') $work
if ($git.exitCode -ne 0) { throw $git.stderr }
Write-Utf8 (Join-Path $work '.gitignore') ".sdd/.test-work/`n"
Write-Utf8 (Join-Path $work 'src/TheShop.Domain/Item.cs') 'original domain'
Write-Utf8 (Join-Path $work 'src/TheShop.Web/Page.cs') 'original web'
$git=Invoke-Captured 'git' @('-c',"safe.directory=$($work.Replace('\','/'))",'add','.') $work
if ($git.exitCode -ne 0) { throw $git.stderr }
Write-Utf8 (Join-Path $work 'src/TheShop.Web/Page.cs') 'already dirty web'
$scriptPath=Join-Path $root '.sdd/scripts/check-worker-scope.ps1'
function Scope([string]$Action,[string]$Phase='domain') {
    Invoke-Captured $pwsh @('-NoProfile','-File',$scriptPath,'-RepositoryRoot',$work,'-Action',$Action,'-SnapshotPath','.sdd/.test-work/scope.json','-Phase',$Phase) $work
}
$r=Scope 'snapshot'; Check 'Byte snapshot created on dirty checkout' ($r.exitCode -eq 0) $r.stderr
$r=Scope 'check'; Check 'Untouched dirty file permitted' ($r.exitCode -eq 0) $r.stderr
Write-Utf8 (Join-Path $work 'src/TheShop.Domain/Item.cs') 'changed domain'
$r=Scope 'check'; Check 'Owned edit permitted' ($r.exitCode -eq 0) $r.stderr
Write-Utf8 (Join-Path $work 'src/TheShop.Web/Page.cs') 'second dirty web edit'
$r=Scope 'check'; Check 'Already-dirty out-of-scope edit rejected' ($r.exitCode -ne 0 -and $r.stderr -match 'Page.cs') $r.stderr
Write-Utf8 (Join-Path $work 'src/TheShop.Web/Page.cs') 'already dirty web'
Write-Utf8 (Join-Path $work 'tests/ItemTests.cs') 'forbidden worker test'
$r=Scope 'check'; Check 'Layer worker test write rejected' ($r.exitCode -ne 0 -and $r.stderr -match 'ItemTests.cs') $r.stderr
# Restore fixture deliberately; no production files touched.
[IO.File]::Delete((Resolve-WorkspacePath $work 'tests/ItemTests.cs'))
[IO.File]::Delete((Resolve-WorkspacePath $work 'src/TheShop.Web/Page.cs'))
$r=Scope 'check'; Check 'Out-of-scope deletion rejected' ($r.exitCode -ne 0 -and $r.stderr -match 'Page.cs') $r.stderr
Write-Utf8 (Join-Path $work 'src/TheShop.Web/Page.cs') 'already dirty web'
Write-Utf8 (Join-Path $work 'src/TheShop.Web/Resources/Strings.resx') 'resource key'
$r=Scope 'check' 'web'; Check 'Web cannot write Application-owned resources' ($r.exitCode -ne 0 -and $r.stderr -match 'Strings.resx') $r.stderr
Write-Utf8 (Join-Path $work 'src/TheShop.Domain/Item.cs') 'original domain'
$r=Scope 'check' 'application'; Check 'Application may supply resource keys' ($r.exitCode -eq 0) $r.stderr
$r=Scope 'snapshot'; Check 'Existing snapshot cannot be overwritten' ($r.exitCode -ne 0) $r.stderr
Write-Json (Join-Path $root '.sdd/reports/release-contracts-validation.json') @{checkedUtc=[DateTime]::UtcNow.ToString('o');passed=$true;checks=@($checks)}
Write-Output "$($checks.Count) release-contract checks passed."
