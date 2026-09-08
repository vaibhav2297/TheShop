# Decode Claude hook JSON here; keep the canonical linter independent of hook schemas.
[CmdletBinding()]
param([string[]]$Path)
$root = $PSScriptRoot
while (-not (Test-Path (Join-Path $root '.sdd/catalog.json'))) {
    $parent = Split-Path $root -Parent
    if (-not $parent -or $parent -eq $root) { throw 'Cannot locate the SDD repository root.' }
    $root = $parent
}
$checker = Join-Path $root '.sdd/scripts/check-design-rules.ps1'
if ($Path) { & $checker -Path $Path; exit $LASTEXITCODE }
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
$file = $payload.tool_input.file_path
if (-not $file) { $file = $payload.tool_response.filePath }
if (-not $file -or -not (Test-Path -LiteralPath $file)) { exit 0 }
$output = & $checker -Path $file
if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine(($output -join "`n")); exit 2 }
exit 0
