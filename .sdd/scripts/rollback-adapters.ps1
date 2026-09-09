# Restore the migration baseline only after checking the entire owned write set.
[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'), [switch]$Apply)
. (Join-Path $PSScriptRoot 'Extensions.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$inventory=Get-Content (Join-Path $root '.sdd/baseline/inventory.json') -Raw | ConvertFrom-Json -AsHashtable
$archive=Join-Path $root '.sdd/baseline/originals.zip'
if ((Get-FileHash $archive).Hash.ToLowerInvariant() -ne $inventory.archiveSha256) { throw 'Baseline archive hash mismatch.' }
$manifestPath=Resolve-WorkspacePath $root '.sdd/generated-files.json'
$manifest=Get-Content $manifestPath -Raw | ConvertFrom-Json -AsHashtable
$original=@{}
foreach ($file in $inventory.sourceFiles) { $original[$file.path]=$file.sha256 }
$conflicts=@()
foreach ($path in $manifest.files.Keys) {
    if ($path -notmatch '^(AGENTS\.md|CLAUDE\.md|\.claude/(skills|agents|commands|scripts)/.+|\.claude/settings\.json|\.agents/skills/[^/]+/.+|\.codex/agents/[^/]+\.toml)$') { throw "Unexpected manifest path: $path" }
    $absolute=Resolve-WorkspacePath $root $path
    if (Test-Path -LiteralPath $absolute) {
        $isGenerated=(Get-GeneratedDigest $absolute $manifest $path) -eq $manifest.files[$path]
        $isOriginal=$original.ContainsKey($path) -and (Get-FileHash $absolute).Hash.ToLowerInvariant() -eq $original[$path]
        if (-not $isGenerated -and -not $isOriginal) { $conflicts += $path }
    }
}
foreach ($path in $original.Keys) {
    Resolve-WorkspacePath $root $path | Out-Null
    if (-not $manifest.files.ContainsKey($path)) { throw "Baseline path missing from generated ownership: $path" }
}
if ($conflicts.Count) { throw "Rollback blocked by independent edits:`n$($conflicts -join "`n")" }
$remove=@($manifest.files.Keys | Where-Object { -not $original.ContainsKey($_) })
Write-Output "[sdd-rollback] restore $($original.Count) original files; remove $($remove.Count) generated files. Shared core and unrelated files remain."
if (-not $Apply) { Write-Output 'Preview only. Use -Apply for the reviewed rollback.'; exit 0 }
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($path in $original.Keys) {
        $entry=$zip.GetEntry($path)
        if (-not $entry) { throw "Archive entry missing: $path" }
    }
    foreach ($path in $original.Keys) {
        $absolute=Resolve-WorkspacePath $root $path
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($absolute)) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($path),$absolute,$true)
    }
} finally { $zip.Dispose() }
foreach ($path in $remove) {
    $absolute=Resolve-WorkspacePath $root $path
    if (Test-Path -LiteralPath $absolute) { Remove-Item -LiteralPath $absolute }
}
Remove-Item -LiteralPath $manifestPath
Write-Output '[sdd-rollback] original adapters restored.'
