[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$work=Resolve-WorkspacePath $root ('.sdd/.test-work/release-'+[Guid]::NewGuid().ToString('N'))
$safe="safe.directory=$($root.Replace('\','/'))"
$tracked=Invoke-Captured 'git' @('-c',$safe,'ls-files','-z','--cached') $root
$added=Invoke-Captured 'git' @('-c',$safe,'ls-files','-z','--others','--exclude-standard','--','.sdd','.claude','.agents/skills','.codex/agents','.github/workflows/sdd-portability.yml','AGENTS.md') $root
if ($tracked.exitCode -ne 0 -or $added.exitCode -ne 0) { throw 'Cannot inventory candidate checkout.' }
$hashes=[ordered]@{}
foreach ($relative in @(($tracked.stdout+$added.stdout).Split([char]0) | Where-Object {$_} | Sort-Object -Unique)) {
    if ($relative -match '(^|/)(\.env($|\.)|\.e2e-env$)|(^|/)(bin|obj)/|^graphify-out/') { continue }
    $from=Resolve-WorkspacePath $root $relative
    if (-not (Test-Path -LiteralPath $from -PathType Leaf)) { continue }
    $to=Resolve-WorkspacePath $work $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null
    [IO.File]::Copy($from,$to)
    $hashes[$relative]=(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash.ToLowerInvariant()
}
foreach ($arguments in @(
    ,@('init','--quiet')
    ,@('-c',"safe.directory=$($work.Replace('\','/'))",'add','--all')
    ,@('-c',"safe.directory=$($work.Replace('\','/'))",'-c','user.name=Release fixture','-c','user.email=fixture@localhost','-c','commit.gpgsign=false','commit','--quiet','-m','test: capture release candidate')
)) {
    $r=Invoke-Captured 'git' $arguments $work
    if ($r.exitCode -ne 0) { throw "Fixture Git setup failed: $($r.stderr)" }
}
$head=Invoke-Captured 'git' @('-c',"safe.directory=$($work.Replace('\','/'))",'rev-parse','HEAD') $work
$state=Invoke-Captured 'git' @('-c',"safe.directory=$($work.Replace('\','/'))",'status','--porcelain') $work
if ($state.exitCode -ne 0 -or $state.stdout.Trim()) { throw 'Fixture is not clean before validation.' }
Write-Json (Join-Path $root '.sdd/reports/release-fixture.json') @{createdUtc=[DateTime]::UtcNow.ToString('o');fixture=$work;head=$head.stdout.Trim();cleanBeforeChecks=$true;sourceFiles=$hashes;excludes=@('ignored files','untracked non-SDD files','environment files','build outputs','graph output');publication='Local candidate snapshot only; no remote CI or release claim.'}
Write-Output $work
