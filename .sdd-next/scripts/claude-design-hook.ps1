[CmdletBinding()]
param(
    [string[]]$Path,
    [Parameter(ValueFromPipeline = $true)]
    [string]$InputObject
)

begin {
    $pipedInput = [System.Collections.Generic.List[string]]::new()
}

process {
    if ($PSBoundParameters.ContainsKey('InputObject')) { $pipedInput.Add($InputObject) }
}

end {

$checker = Join-Path $PSScriptRoot 'check-design-rules.ps1'

if ($Path) {
    & $checker -Path $Path
    exit $LASTEXITCODE
}

$raw = if ($pipedInput.Count -gt 0) { $pipedInput -join "`n" } else { [Console]::In.ReadToEnd() }
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try {
    $payload = $raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
    exit 0
}

$file = $payload.tool_input.file_path
if (-not $file) { $file = $payload.tool_response.filePath }
if (-not $file -or -not (Test-Path -LiteralPath $file -PathType Leaf)) { exit 0 }

$output = & $checker -Path $file
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine(($output -join "`n"))
    exit 2
}

exit 0
}
