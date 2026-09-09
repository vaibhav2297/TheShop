[CmdletBinding()]
param(
    [ValidateSet('Inspect','List','Install','Update','Remove','Rollback','Recover')][string]$Action='List',
    [string]$PackagePath,
    [string]$Id,
    [string]$Revision,
    [string]$Transaction,
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [switch]$Apply
)
. (Join-Path $PSScriptRoot 'Extensions.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$pwsh=(Get-Process -Id $PID).Path
$registryRelative='.sdd/extensions/registry.json'
$registryPath=Resolve-WorkspacePath $root $registryRelative
$history=Resolve-WorkspacePath $root '.sdd/extensions/history'
function File-State([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return [Convert]::ToBase64String([IO.File]::ReadAllBytes($Path)) }
    if (Test-Path -LiteralPath $Path) { throw "Expected file: $Path" }
    return $null
}
function Set-FileState([string]$Relative,$State) {
    $path=Resolve-WorkspacePath $root $Relative
    if ($null -eq $State) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }; return }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllBytes($path,[Convert]::FromBase64String($State))
}
function Restore-Transaction($Record) {
    foreach ($change in $Record.changes) {
        if ($change.path -notmatch '^(AGENTS\.md|CLAUDE\.md|\.claude/(skills|agents|commands|scripts)/.+|\.claude/settings\.json|\.agents/skills/.+|\.codex/agents/[^/]+\.toml|\.sdd/generated-files\.json|\.sdd/extensions/(registry\.json|packages/.+))$') { throw 'Invalid transaction destination.' }
        $current=File-State (Resolve-WorkspacePath $root $change.path)
        if ($current -cne $change.before -and $current -cne $change.after) { throw "Independent edit blocks recovery: $($change.path)" }
    }
    foreach ($change in @($Record.changes)[($Record.changes.Count-1)..0]) { Set-FileState $change.path $change.before }
}
if ($Action -eq 'Inspect') {
    $package=Get-ExtensionPackage $PackagePath
    [ordered]@{ id=$package.manifest.id; revision=$package.revision; manifest=$package.manifest; files=$package.files } | ConvertTo-Json -Depth 30
    exit 0
}
$registry=if(Test-Path -LiteralPath $registryPath){Read-Utf8 $registryPath | ConvertFrom-Json -AsHashtable}else{@{schemaVersion=1;active=@{}}}
if ($Action -eq 'List') {
    $retained=[ordered]@{}
    $packageRoot=Resolve-WorkspacePath $root '.sdd/extensions/packages'
    if (Test-Path -LiteralPath $packageRoot) {
        foreach ($directory in Get-ChildItem -LiteralPath $packageRoot -Directory) {
            $retained[$directory.Name]=@(Get-ChildItem -LiteralPath $directory.FullName -Directory | Where-Object { $_.Name -match '^[a-f0-9]{64}$' } | ForEach-Object { $_.Name })
        }
    }
    [ordered]@{schemaVersion=1;active=$registry.active;retained=$retained} | ConvertTo-Json -Depth 10
    exit 0
}
if ($Action -eq 'Recover') {
    if ($Transaction -notmatch '^[a-f0-9]{32}$') { throw 'Recover requires transaction ID.' }
    $recordPath=Resolve-WorkspacePath $root ".sdd/extensions/history/$Transaction.json"
    $record=Read-Utf8 $recordPath | ConvertFrom-Json -AsHashtable
    if ($record.state -notin @('pending','recovery-required')) { throw 'Only incomplete transactions can be recovered.' }
    if (-not $Apply) { Write-Output "[extensions] recover $Transaction; $($record.changes.Count) files. Preview only; add -Apply."; exit 0 }
    $recoveryLock=[IO.File]::Open((Resolve-WorkspacePath $root '.sdd/extensions/.mutation-lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try {
        $record=Read-Utf8 $recordPath | ConvertFrom-Json -AsHashtable
        if ($record.state -notin @('pending','recovery-required')) { throw 'Transaction state changed; retry.' }
        Restore-Transaction $record
        $record.state='recovered'; Write-Json $recordPath $record
    } finally { $recoveryLock.Dispose() }
    Write-Output '[extensions] incomplete transaction recovered.'; exit 0
}
if (Test-Path -LiteralPath $history) {
    foreach ($file in Get-ChildItem -LiteralPath $history -Filter '*.json' -File) {
        $record=Read-Utf8 $file.FullName | ConvertFrom-Json -AsHashtable
        if ($record.state -in @('pending','recovery-required')) { throw "Recover incomplete transaction first: $($file.BaseName)" }
    }
}
$package=$null
if ($Action -in @('Install','Update')) {
    $package=Get-ExtensionPackage $PackagePath
    if ($Id -and $Id -cne $package.manifest.id) { throw 'Package ID mismatch.' }
    $Id=$package.manifest.id; $Revision=$package.revision
}
if ($Id -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$') { throw 'Valid extension ID required.' }
$beforeRevision=if($registry.active.Contains($Id)){$registry.active[$Id]}else{$null}
if ($Action -eq 'Install' -and $beforeRevision) {
    if ($beforeRevision -ceq $Revision) { Get-SddCatalog $root | Out-Null; Write-Output '[extensions] already installed; no changes.'; exit 0 }
    throw 'Extension exists; use Update.'
}
if ($Action -in @('Update','Remove','Rollback') -and -not $beforeRevision -and $Action -ne 'Rollback') { throw 'Extension is not active.' }
if ($Action -eq 'Remove') {
    $core=Read-Utf8 (Join-Path $root '.sdd/catalog.json') | ConvertFrom-Json -AsHashtable
    foreach ($caller in @($core.skills)+@($core.roles)) {
        if ((Read-Utf8 (Resolve-WorkspacePath $root $caller.source)) -match ('(?<![a-z0-9-])'+[regex]::Escape($Id)+'(?![a-z0-9-])')) { throw "Reconcile caller before removal: $($caller.source)" }
    }
}
if ($Action -eq 'Rollback') {
    if ($Revision -notmatch '^[a-f0-9]{64}$') { throw 'Rollback requires a retained package revision from List/history.' }
    $package=Get-ExtensionPackage (Resolve-WorkspacePath $root ".sdd/extensions/packages/$Id/$Revision")
    if ($package.manifest.id -cne $Id -or $package.revision -cne $Revision) { throw 'Rollback revision mismatch.' }
}
$registryBefore=File-State $registryPath
$manifestPath=Resolve-WorkspacePath $root '.sdd/generated-files.json'
$manifestBefore=File-State $manifestPath
$old=if($manifestBefore){Read-Utf8 $manifestPath | ConvertFrom-Json -AsHashtable}else{@{files=@{}}}
$stage=Resolve-WorkspacePath $root ('.sdd/.stage/extension-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $stage '.sdd')) | Out-Null
foreach ($directory in @('contracts','skills','roles','adapters','scripts')) {
    Copy-Item -LiteralPath (Join-Path $root ".sdd/$directory") -Destination (Join-Path $stage '.sdd') -Recurse
}
foreach ($path in @('.sdd/catalog.json','.sdd/baseline/inventory.json')) {
    $target=Resolve-WorkspacePath $stage $path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $path) -Destination $target
}
foreach ($activeId in $registry.active.Keys) {
    if ($activeId -eq $Id) { continue }
    $activeRevision=$registry.active[$activeId]
    if ($activeId -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$' -or $activeRevision -notmatch '^[a-f0-9]{64}$') { throw 'Invalid active package path.' }
    $relative=".sdd/extensions/packages/$activeId/$activeRevision"
    $source=Resolve-WorkspacePath $root $relative
    Get-ExtensionPackage $source | Out-Null
    $destination=Resolve-WorkspacePath $stage $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
}
$newPackageFiles=@()
if ($package) {
    $packageRelative=".sdd/extensions/packages/$Id/$Revision"
    $target=Resolve-WorkspacePath $stage $packageRelative
    foreach ($relative in $package.files.Keys) {
        $destination=Resolve-WorkspacePath $target $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath (Resolve-WorkspacePath $package.directory $relative) -Destination $destination -Force
        $newPackageFiles += "$packageRelative/$relative"
    }
    Write-Json (Join-Path $target '.package-lock.json') ([ordered]@{revision=$Revision;files=$package.files})
    $newPackageFiles += "$packageRelative/.package-lock.json"
    $registry.active[$Id]=$Revision
} else { $registry.active.Remove($Id) }
Write-Json (Resolve-WorkspacePath $stage $registryRelative) $registry
$generated=Join-Path $stage 'generated'
$result=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $stage '.sdd/scripts/sync-adapters.ps1'),'-RepositoryRoot',$stage,'-OutputRoot',$generated) $stage
if ($result.exitCode -ne 0) { throw ($result.stdout+$result.stderr) }
$desired=Read-Utf8 (Join-Path $generated '.sdd/generated-files.json') | ConvertFrom-Json -AsHashtable
$changes=[Collections.Generic.List[object]]::new()
function Add-Change([string]$Path,$After) {
    $before=File-State (Resolve-WorkspacePath $root $Path)
    if ($before -cne $After) { $changes.Add(@{path=$Path;before=$before;after=$After}) }
}
# Preflight every native path, including paths being retired, before changing shared state.
foreach ($path in @(@($old.files.Keys)+@($desired.files.Keys) | Sort-Object -Unique)) {
    $absolute=Resolve-WorkspacePath $root $path
    $before=File-State $absolute
    if ($old.files.Contains($path)) {
        if ($null -eq $before -or (Get-GeneratedDigest $absolute $old $path) -cne $old.files[$path]) { throw "Independent generated edit blocks extension change: $path" }
    } elseif ($null -ne $before) { throw "Unowned native path collision: $path" }
    $after=if($desired.files.Contains($path)){File-State (Resolve-WorkspacePath $generated $path)}else{$null}
    Add-Change $path $after
}
foreach ($path in $newPackageFiles) {
    $after=File-State (Resolve-WorkspacePath $stage $path)
    $before=File-State (Resolve-WorkspacePath $root $path)
    if ($null -ne $before -and $before -cne $after) { throw "Immutable package collision: $path" }
    Add-Change $path $after
}
Add-Change $registryRelative (File-State (Resolve-WorkspacePath $stage $registryRelative))
Add-Change '.sdd/generated-files.json' (File-State (Join-Path $generated '.sdd/generated-files.json'))
Write-Output "[extensions] $Action $Id; revision=$Revision; changed files=$($changes.Count)."
Write-Output "  previous revision=$beforeRevision; staged review=$stage"
foreach ($change in $changes) { Write-Output "  $(if($null -eq $change.after){'remove'}else{'write'}) $($change.path)" }
if (-not $Apply) { Write-Output 'Preview only. Add -Apply to publish this change.'; exit 0 }
if (-not $changes.Count) { Write-Output '[extensions] no changes.'; exit 0 }
[IO.Directory]::CreateDirectory($history) | Out-Null
$lockPath=Resolve-WorkspacePath $root '.sdd/extensions/.mutation-lock'
$lock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    if ((File-State $registryPath) -cne $registryBefore -or (File-State $manifestPath) -cne $manifestBefore) { throw 'Registry changed during preparation; retry.' }
    foreach ($change in $changes) {
        if ((File-State (Resolve-WorkspacePath $root $change.path)) -cne $change.before) { throw "File changed during preparation: $($change.path)" }
    }
    $transactionId=[Guid]::NewGuid().ToString('N')
    $recordPath=Resolve-WorkspacePath $root ".sdd/extensions/history/$transactionId.json"
    $record=@{schemaVersion=1;id=$Id;action=$Action;beforeRevision=$beforeRevision;afterRevision=if($Action -eq 'Remove'){$null}else{$Revision};state='pending';createdUtc=[DateTime]::UtcNow.ToString('o');changes=@($changes.ToArray())}
    Write-Json $recordPath $record
    try {
        foreach ($change in $changes) { Set-FileState $change.path $change.after }
        $record.state='complete'; Write-Json $recordPath $record
    } catch {
        $failure=$_
        try { Restore-Transaction $record; $record.state='recovered' } catch { $record.state='recovery-required' }
        Write-Json $recordPath $record
        throw $failure
    }
    Write-Output "[extensions] published; transaction=$transactionId. Retained packages support version rollback."
} finally { $lock.Dispose() }
