[CmdletBinding()]
param([Parameter(Mandatory)][string]$FixtureRoot,[string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$fixture=[IO.Path]::GetFullPath($FixtureRoot)
$relative=[IO.Path]::GetRelativePath($root,$fixture)
$fixture=Resolve-WorkspacePath $root $relative
if ($relative.Replace('\','/') -notlike '.sdd/.test-work/release-*') { throw 'Use an isolated release fixture.' }
$pwsh=(Get-Process -Id $PID).Path
$results=@()
foreach ($suite in @('test-portability','test-caveman','test-extensions','test-evidence','test-release-contracts')) {
    $arguments=@('-NoProfile','-File',".sdd/scripts/$suite.ps1")
    if ($suite -in @('test-portability','test-caveman','test-extensions')) { $arguments+='-Published' }
    $run=Invoke-Captured $pwsh $arguments $fixture 600
    $log=".sdd/.test-work/release-validation/$suite.log"
    Write-Utf8 (Join-Path $fixture $log) ($run.stdout+$run.stderr)
    $results+=@{suite=$suite;exitCode=$run.exitCode;log=$log}
    Write-Output "$suite exit=$($run.exitCode)"
}
$passed=@($results | Where-Object exitCode -ne 0).Count -eq 0
Write-Json (Join-Path $root '.sdd/reports/release-clean-validation.json') @{checkedUtc=[DateTime]::UtcNow.ToString('o');fixture=$fixture;passed=$passed;checks=$results;scope='Local clean candidate checkout; remote CI not run.'}
if (-not $passed) { exit 1 }
