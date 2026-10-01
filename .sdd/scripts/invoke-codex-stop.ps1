# Keep Codex Stop output valid JSON without nesting PowerShell command strings.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('design', 'format')]
    [string]$Check
)

$ErrorActionPreference = 'Stop'
$scriptName = if ($Check -eq 'design') { 'check-design-rules.ps1' } else { 'format-on-stop.ps1' }
$scriptPath = Join-Path $PSScriptRoot $scriptName
[string[]]$scriptArguments = if ($Check -eq 'design') { @('-Changed') } else { @() }

try {
    # A child process isolates the scripts' exit statements. Close its stdin so
    # the design check's legacy hook mode cannot wait for input on an empty diff.
    $output = '' | & pwsh -NoProfile -ExecutionPolicy Bypass -File $scriptPath @scriptArguments 2>&1 | Out-String
    $hookExit = $LASTEXITCODE
    if ($hookExit -ne 0) {
        [Console]::Error.Write($output)
        exit $hookExit
    }

    [Console]::Out.Write('{"continue":true}')
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
