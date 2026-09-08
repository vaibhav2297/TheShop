[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
$fixture=Resolve-WorkspacePath $root ('.sdd/.test-work/extension-rollback-'+[Guid]::NewGuid().ToString('N'))
$inventory=Read-Utf8 (Join-Path $root '.sdd/baseline/extensions/inventory.json') | ConvertFrom-Json -AsHashtable
$manifest=Read-Utf8 (Join-Path $root '.sdd/generated-files.json') | ConvertFrom-Json -AsHashtable
$paths=@($inventory.files.path)+@($manifest.files.Keys)+@('.sdd/generated-files.json','.sdd/catalog.json','.sdd/README.md','.github/workflows/sdd-portability.yml')
foreach ($directory in @('contracts','skills','roles','adapters','scripts','evals','extensions')) {
    $paths += Get-ChildItem -LiteralPath (Join-Path $root ".sdd/$directory") -Recurse -File -Force | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/') }
}
foreach ($path in @($paths | Sort-Object -Unique)) {
    $source=Resolve-WorkspacePath $root $path
    if (-not (Test-Path -LiteralPath $source)) { continue }
    $destination=Resolve-WorkspacePath $fixture $path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
foreach ($file in @('inventory.json','phase6.zip')) {
    $destination=Resolve-WorkspacePath $fixture ".sdd/baseline/extensions/$file"
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $root ".sdd/baseline/extensions/$file") -Destination $destination
}
$script=Join-Path $root '.sdd/scripts/extension-checkpoint.ps1'
function Run([string[]]$Arguments) { Invoke-Captured $pwsh (@('-NoProfile','-File',$script,'-RepositoryRoot',$fixture)+$Arguments) $root }
$seal=Run @('-Action','Seal'); if($seal.exitCode -ne 0){throw $seal.stderr}
$saved=Read-Utf8 (Join-Path $fixture '.sdd/README.md')
Write-Utf8 (Join-Path $fixture '.sdd/README.md') ($saved+"`nIndependent edit.`n")
$before=(Get-FileHash (Join-Path $fixture 'AGENTS.md')).Hash
$rejection=Run @('-Action','Restore','-Apply')
$rejects=$rejection.exitCode -ne 0 -and (Read-Utf8 (Join-Path $fixture '.sdd/README.md')).Contains('Independent edit.') -and (Get-FileHash (Join-Path $fixture 'AGENTS.md')).Hash -ceq $before
if(-not $rejects){throw 'Phase rollback did not preserve independent edits.'}
Write-Utf8 (Join-Path $fixture '.sdd/README.md') $saved
Write-Utf8 (Join-Path $fixture 'personal.txt') 'Preserve unrelated file.'
$preview=Run @('-Action','Restore')
$previewOnly=$preview.exitCode -eq 0 -and (Test-Path (Join-Path $fixture '.sdd/extensions/registry.json'))
if(-not $previewOnly){throw 'Phase rollback preview failed.'}
$restore=Run @('-Action','Restore','-Apply')
$different=@($inventory.files | Where-Object { $path=Join-Path $fixture $_.path; -not (Test-Path $path) -or (Get-FileHash $path).Hash.ToLowerInvariant() -cne $_.sha256 })
$restored=$restore.exitCode -eq 0 -and $different.Count -eq 0 -and (Test-Path (Join-Path $fixture 'personal.txt')) -and -not (Test-Path (Join-Path $fixture '.sdd/extensions/registry.json'))
Write-Json (Join-Path $root '.sdd/reports/extension-checkpoint.json') @{checkedUtc=[DateTime]::UtcNow.ToString('o');passed=$rejects -and $previewOnly -and $restored;checks=@{independentEditRejected=$rejects;previewOnly=$previewOnly;phase6BytesRestored=$restored};restoredFiles=$inventory.files.Count;fixture=$fixture;output=$restore.stdout;error=$restore.stderr}
if(-not $restored){throw ('Phase rollback differs: '+$restore.stderr+($different.path -join ', '))}
Write-Output "[extension-checkpoint] 3/3 checks passed; $($inventory.files.Count) phase 6 files restored in isolation."
