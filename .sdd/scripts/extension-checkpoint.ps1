# Preserve phase 6 separately from the original Claude-only migration baseline.
[CmdletBinding()]
param(
    [ValidateSet('Capture','Seal','Check','Restore')][string]$Action='Check',
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [ValidatePattern('^candidate(?:-[a-z0-9-]+)?\.json$')][string]$CandidateFile='candidate.json',
    [switch]$Apply
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$checkpoint=Resolve-WorkspacePath $root '.sdd/baseline/extensions'
$inventoryPath=Join-Path $checkpoint 'inventory.json'
$archivePath=Join-Path $checkpoint 'phase6.zip'
$sealPath=Join-Path $checkpoint $CandidateFile
function Managed-Files {
    $paths=@('.sdd/catalog.json','.sdd/generated-files.json','.sdd/README.md','.github/workflows/sdd-portability.yml')
    foreach ($directory in @('contracts','skills','roles','adapters','scripts','evals','extensions')) {
        $absolute=Join-Path $root ".sdd/$directory"
        if (Test-Path -LiteralPath $absolute) {
            $paths += Get-ChildItem -LiteralPath $absolute -Recurse -File -Force | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/') }
        }
    }
    $manifest=Get-Content -LiteralPath (Join-Path $root '.sdd/generated-files.json') -Raw | ConvertFrom-Json -AsHashtable
    $paths += $manifest.files.Keys
    return @($paths | Sort-Object -Unique | Where-Object { Test-Path -LiteralPath (Resolve-WorkspacePath $root $_) })
}
function File-Records([string[]]$Paths) {
    return @($Paths | ForEach-Object { [ordered]@{ path=$_; sha256=(Get-FileHash -LiteralPath (Resolve-WorkspacePath $root $_)).Hash.ToLowerInvariant() } })
}
if ($Action -eq 'Capture') {
    if ((Test-Path $inventoryPath) -or (Test-Path $archivePath)) { throw 'Extension baseline exists; never recapture over it.' }
    [IO.Directory]::CreateDirectory($checkpoint) | Out-Null
    $paths=Managed-Files
    $zip=[IO.Compression.ZipFile]::Open($archivePath,[IO.Compression.ZipArchiveMode]::Create)
    try { foreach ($path in $paths) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Resolve-WorkspacePath $root $path),$path) | Out-Null } }
    finally { $zip.Dispose() }
    $protected=@()
    foreach ($directory in @('src','tests','.specs')) {
        $protected += Get-ChildItem -LiteralPath (Join-Path $root $directory) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules)[\\/]' } | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/') }
    }
    Write-Json $inventoryPath ([ordered]@{ schemaVersion=1; capturedUtc=[DateTime]::UtcNow.ToString('o'); archiveSha256=(Get-FileHash $archivePath).Hash.ToLowerInvariant(); files=(File-Records $paths); protectedFiles=(File-Records $protected) })
    Write-Output "[extension-checkpoint] captured $($paths.Count) files; phase 6 restore point ready."
    exit 0
}
if (-not (Test-Path $inventoryPath)) { throw 'Capture the phase 6 baseline first.' }
$inventory=Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json -AsHashtable
if ((Get-FileHash $archivePath).Hash.ToLowerInvariant() -cne $inventory.archiveSha256) { throw 'Baseline archive hash mismatch.' }
if ($Action -eq 'Seal') {
    # Refuse replacing an existing seal: that would authorize overwriting later independent edits.
    if (Test-Path $sealPath) { throw 'Candidate already sealed. Review changes before establishing a new migration.' }
    Write-Json $sealPath ([ordered]@{ schemaVersion=1; sealedUtc=[DateTime]::UtcNow.ToString('o'); files=(File-Records (Managed-Files)) })
    Write-Output '[extension-checkpoint] candidate sealed for conflict-safe rollback.'
    exit 0
}
if ($Action -eq 'Check') {
    $changed=@($inventory.protectedFiles | Where-Object { $file=Resolve-WorkspacePath $root $_.path; -not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -cne $_.sha256 })
    if ($changed.Count) { throw "Protected application/test/spec files changed: $($changed.path -join ', ')" }
    Write-Output "[extension-checkpoint] archive verified; $($inventory.protectedFiles.Count) protected files unchanged."
    exit 0
}
if (-not (Test-Path $sealPath)) { throw 'Seal verified candidate before using Restore.' }
$seal=Get-Content -LiteralPath $sealPath -Raw | ConvertFrom-Json -AsHashtable
$baseline=@{}; foreach ($file in $inventory.files) { $baseline[$file.path]=$file.sha256 }
$sealed=@{}; foreach ($file in $seal.files) { $sealed[$file.path]=$file.sha256 }
$restore=[ordered]@{}
$zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($entry in $zip.Entries) {
        $path=$entry.FullName
        Resolve-WorkspacePath $root $path | Out-Null
        if (-not $baseline.ContainsKey($path) -or $restore.Contains($path)) { throw "Unexpected or duplicate archive path: $path" }
        $memory=[IO.MemoryStream]::new(); $stream=$entry.Open()
        try { $stream.CopyTo($memory); $bytes=$memory.ToArray() } finally { $stream.Dispose(); $memory.Dispose() }
        $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        if ($digest -cne $baseline[$path]) { throw "Archive entry hash mismatch: $path" }
        $restore[$path]=$bytes
    }
} finally { $zip.Dispose() }
if ($restore.Count -ne $baseline.Count) { throw 'Archive is incomplete.' }
$remove=@($sealed.Keys | Where-Object { -not $baseline.ContainsKey($_) })
$conflicts=@()
foreach ($path in @(@($baseline.Keys)+@($sealed.Keys) | Sort-Object -Unique)) {
    $absolute=Resolve-WorkspacePath $root $path
    if (-not (Test-Path -LiteralPath $absolute)) {
        if ($baseline.ContainsKey($path)) { $conflicts += $path }
        continue
    }
    $digest=(Get-FileHash -LiteralPath $absolute).Hash.ToLowerInvariant()
    if (($baseline.ContainsKey($path) -and $digest -ceq $baseline[$path]) -or ($sealed.ContainsKey($path) -and $digest -ceq $sealed[$path])) { continue }
    $conflicts += $path
}
if ($conflicts.Count) { throw "Independent edits block rollback before writes: $($conflicts -join ', ')" }
Write-Output "[extension-checkpoint] restore $($restore.Count) phase 6 files; remove $($remove.Count) Extension-owned files."
if (-not $Apply) { Write-Output 'Preview only. Add -Apply to restore.'; exit 0 }
foreach ($path in $restore.Keys) {
    $absolute=Resolve-WorkspacePath $root $path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($absolute)) | Out-Null
    [IO.File]::WriteAllBytes($absolute,$restore[$path])
}
foreach ($path in $remove) {
    $absolute=Resolve-WorkspacePath $root $path
    if (Test-Path -LiteralPath $absolute) { Remove-Item -LiteralPath $absolute }
}
Write-Output '[extension-checkpoint] restored phase 6; unrelated files and migration evidence retained.'
