[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('snapshot','check')][string]$Action,
    [Parameter(Mandatory)][string]$SnapshotPath,
    [ValidateSet('domain','application','infra','web','infra+web','all-production')][string]$Phase,
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..')
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$snapshotFile=Resolve-WorkspacePath $root $SnapshotPath
$inventory=Invoke-Captured 'git' @('-c',"safe.directory=$($root.Replace('\','/'))",'ls-files','-z','--cached','--others','--exclude-standard') $root
if ($inventory.exitCode -ne 0) { throw "Git inventory failed: $($inventory.stderr)" }
$current=[ordered]@{}
foreach ($relative in @($inventory.stdout.Split([char]0) | Where-Object { $_ } | Sort-Object -Unique)) {
    $path=Resolve-WorkspacePath $root $relative
    if ($path -ieq $snapshotFile) { continue }
    # Graph refresh is a permitted worker side effect, never production evidence.
    if ($relative -like 'graphify-out/*') { continue }
    $current[$relative]=if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'missing' }
}
if ($Action -eq 'snapshot') {
    if (Test-Path -LiteralPath $snapshotFile) { throw 'Snapshot exists; use a distinct phase snapshot.' }
    Write-Json $snapshotFile @{schemaVersion=1;root=$root;files=$current}
    Write-Output 'Worker scope byte snapshot saved.'
    exit 0
}
if (-not $Phase) { throw 'check requires -Phase.' }
$before=Read-Utf8 $snapshotFile | ConvertFrom-Json -AsHashtable
if ($before.schemaVersion -ne 1 -or $before.root -ine $root) { throw 'Snapshot root/schema mismatch.' }
$changed=@(@($before.files.Keys)+@($current.Keys) | Sort-Object -Unique | Where-Object { $before.files[$_] -cne $current[$_] })
$resources=@('src/TheShop.Web/Resources/Strings.resx','src/TheShop.Web/Resources/Strings.fr.resx')
$violations=@(foreach ($path in $changed) {
    $allowed=switch ($Phase) {
        'domain' { $path -clike 'src/TheShop.Domain/*' }
        'application' { $path -clike 'src/TheShop.Application/*' -or $path -cin $resources }
        'infra' { $path -clike 'src/TheShop.Infrastructure/*' }
        'web' { $path -clike 'src/TheShop.Web/*' -and $path -cnotin $resources }
        'infra+web' { ($path -clike 'src/TheShop.Infrastructure/*' -or $path -clike 'src/TheShop.Web/*') -and $path -cnotin $resources }
        'all-production' { $path -cmatch '^src/TheShop\.(Domain|Application|Infrastructure|Web)/' }
    }
    if (-not $allowed) { $path }
})
if ($violations.Count) { throw "Worker scope escape ($Phase): $($violations -join ', ')" }
Write-Output "Worker scope clean ($Phase): $($changed.Count) changed files; existing dirty bytes compared."
