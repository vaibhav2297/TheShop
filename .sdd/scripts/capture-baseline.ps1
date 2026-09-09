# Capture once, before replacing any native definitions. Never reads credentials or local settings.
[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$destination = Join-Path $root '.sdd/baseline'
if (Test-Path (Join-Path $destination 'inventory.json')) { throw 'Baseline already exists; it is immutable.' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$files = @('CLAUDE.md', '.claude/settings.json')
foreach ($directory in @('.claude/skills','.claude/commands','.claude/agents','.claude/scripts')) {
    $files += Get-ChildItem (Join-Path $root $directory) -Recurse -File -Force | ForEach-Object {
        [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/')
    }
}
$files = @($files | Sort-Object -Unique)
$archivePath = Join-Path $destination 'originals.zip'
$zip = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root $file),$file) | Out-Null
    }
} finally { $zip.Dispose() }
$head = Invoke-Captured 'git' @('-c',"safe.directory=$($root.Replace('\','/'))",'rev-parse','HEAD') $root
$inventory = [ordered]@{
    schemaVersion = 1
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    gitCommit = $head.stdout.Trim()
    archiveSha256 = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceFiles = @($files | ForEach-Object { @{ path=$_; sha256=(Get-FileHash (Join-Path $root $_)).Hash.ToLowerInvariant() } })
    featureArtifacts = @(Get-ChildItem (Join-Path $root '.specs') -Recurse -File | ForEach-Object {
        @{ path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/'); sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant() }
    })
    gates = @()
}
$pwsh = (Get-Process -Id $PID).Path
foreach ($feature in @('breadcrumbs','manage-brands','manage-categories')) {
    foreach ($mode in @('spec','plan','manifest','status')) {
        $result = Invoke-Captured $pwsh @('-NoProfile','-File','.claude/scripts/check-sdd-gates.ps1',$mode,'-Feature',$feature) $root
        $inventory.gates += [ordered]@{ feature=$feature; mode=$mode; result=$result }
    }
}
Write-Json (Join-Path $destination 'inventory.json') $inventory
Write-Output "Captured $($files.Count) definitions and $($inventory.gates.Count) baseline gate results."
