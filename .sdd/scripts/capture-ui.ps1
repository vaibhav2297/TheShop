#Requires -Version 7
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^(?:[0-9]+_)?[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Feature,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string]$Surface,
    [Parameter(Mandatory)][string]$Reference,
    [Parameter(Mandatory)][string]$ReferenceRevision,
    [Parameter(Mandatory)][string]$Ready,
    [string]$BaseUrl = 'http://localhost:5218',
    [string]$StorageState,
    [string]$Crop,
    [string[]]$BrowserArguments = @(),
    [switch]$StartApp,
    [switch]$LocalE2E
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
. "$PSScriptRoot/visual-evidence.ps1"
$plan = Get-Content -LiteralPath "$root/.specs/$Feature/plan.md" -Raw
$target = @(Get-VisualTargets $plan | Where-Object Surface -CEQ $Surface)
if ($target.Count -ne 1) { throw "Surface $Surface must occur once in plan Visual targets." }
$target = $target[0]
$referencePath = (Resolve-Path -LiteralPath $Reference).Path
if (!(Get-Item -LiteralPath $referencePath).Length) { throw 'Reference image is empty.' }
if ([string]::IsNullOrWhiteSpace($ReferenceRevision)) { throw 'Reference revision/date required.' }
for ($i = 0; $i -lt $BrowserArguments.Count; $i++) {
    $action = $BrowserArguments[$i]
    if ($action -notin @('--click', '--fill', '--hover', '--press', '--wait-for', '--scroll')) { throw "Unsupported browser action $action." }
    $i += $(if ($action -in @('--fill', '--press')) { 2 } else { 1 })
    if ($i -ge $BrowserArguments.Count) { throw "Missing argument for $action." }
}
$output = "$root/.specs/$Feature/evidence/visual/$Surface"
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Invalidate previous evidence before attempting a new capture, including failed attempts.
Set-Content -LiteralPath "$output/capture.md" -Value '**Verdict:** INCOMPLETE' -Encoding utf8
$before = Get-VisualSourceHash $root $Feature
$dimensions = $target.Viewport.Split('x')
$arguments = @('run', '--project', "$root/.sdd/tools/VisualCapture", '--', '--url', ($BaseUrl.TrimEnd('/') + $target.Route), '--output', $output,
    '--width', $dimensions[0], '--height', $dimensions[1], '--ready', $Ready, '--reference', $referencePath)
if ($StartApp) { $arguments += '--start-app' }
if ($LocalE2E) { $arguments += '--local-e2e' }
if ($StorageState) { $arguments += @('--storage-state', (Resolve-Path -LiteralPath $StorageState).Path) }
if ($Crop) { $arguments += @('--crop', $Crop) }
$arguments += $BrowserArguments
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Browser capture failed. Evidence remains incomplete.' }
$after = Get-VisualSourceHash $root $Feature
if ($before -cne $after) { throw 'Source changed during capture. Rebuild and recapture.' }
$lines = @('# Visual capture', '')
foreach ($field in @('Surface', 'Route', 'Viewport', 'State', 'Reference')) { $lines += "**${field}:** $($target.$field)" }
$lines += "**Reference revision:** $ReferenceRevision", "**Source SHA256:** $after"
foreach ($name in @('reference.png', 'actual.png', 'overlay.png', 'difference.png', 'browser.md')) {
    $lines += "**$name SHA256:** $((Get-FileHash -LiteralPath "$output/$name" -Algorithm SHA256).Hash)"
}
$lines += '**Verdict:** UNREVIEWED'
Set-Content -LiteralPath "$output/capture.md" -Value $lines -Encoding utf8
Write-Output "Capture SHA256: $((Get-FileHash -LiteralPath "$output/capture.md" -Algorithm SHA256).Hash)"
Write-Output 'Open reference.png, actual.png, overlay.png and difference.png. Record independent review.md; capture does not assert fidelity.'
