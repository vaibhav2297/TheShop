# Shared helpers for the SDD adapter tools. Requires PowerShell 7.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$Path) {
    return [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).Replace("`r`n", "`n")
}

function Write-Utf8([string]$Path, [string]$Text) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))) | Out-Null
    [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

function Write-Json([string]$Path, $Value) {
    Write-Utf8 $Path (($Value | ConvertTo-Json -Depth 100) + "`n")
}

function Get-Digest([string]$Text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant()
}

function Read-Definition([string]$Path) {
    $text = Read-Utf8 $Path
    $match = [regex]::Match($text, '\A---\n(?<header>.*?)\n---\n(?<body>.*)\z', 'Singleline')
    if (-not $match.Success) { throw "Missing frontmatter: $Path" }
    $fields = [ordered]@{}
    foreach ($line in ($match.Groups['header'].Value -split "`n")) {
        if ($line -match '^(?<key>[a-zA-Z][\w-]*):\s*(?<value>.*)$') {
            $fields[$Matches.key] = $Matches.value
        } elseif ($line.Trim()) { throw "Unsupported multiline frontmatter in $Path" }
    }
    return @{ Fields = $fields; Body = $match.Groups['body'].Value.TrimStart("`n") }
}

function Resolve-WorkspacePath([string]$Root, [string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or $Relative -match '(^|[/\\])\.\.([/\\]|$)') {
        throw "Expected a workspace-relative path: $Relative"
    }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('/','\')
    $path = [IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if (-not $path.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes workspace: $Relative"
    }
    # Generated paths must not traverse a symlink or junction into another workspace.
    $part = $path
    while ($part -and $part.Length -gt $rootPath.Length) {
        if (Test-Path -LiteralPath $part) {
            $item = Get-Item -LiteralPath $part -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point in generated path: $part" }
        }
        $part = [IO.Path]::GetDirectoryName($part)
    }
    return $path
}

function Invoke-Captured([string]$Program, [string[]]$Arguments, [string]$WorkingDirectory, [int]$TimeoutSeconds=600, [string]$InputText) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Program
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.RedirectStandardInput = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if ($InputText) { $process.StandardInput.Write($InputText) }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        return [ordered]@{ exitCode=124; stdout=$stdout.Result; stderr="Timed out after $TimeoutSeconds seconds.`n" + $stderr.Result }
    }
    return [ordered]@{ exitCode = $process.ExitCode; stdout = $stdout.Result.Replace("`r`n","`n"); stderr = $stderr.Result.Replace("`r`n","`n") }
}
