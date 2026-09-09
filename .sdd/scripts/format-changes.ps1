# Format changed C#/Razor files before final verification; usable by either runtime.
[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
Push-Location $root
try {
    $changed = @(& git diff --name-only --diff-filter=ACM)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect unstaged changes.' }
    $changed += @(& git diff --staged --name-only --diff-filter=ACM)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect staged changes.' }
    $changed += @(& git ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect untracked files.' }
    $changed = @($changed | Sort-Object -Unique)
    $relevant = @($changed | Where-Object { $_ -match '\.(cs|razor)$' -and $_ -notmatch '^\.sdd/' })
    if ($relevant.Count) {
        & dotnet format TheShop.slnx --no-restore --verbosity quiet --include @relevant
        if ($LASTEXITCODE -ne 0) { throw "dotnet format failed: $LASTEXITCODE" }
        Write-Output "[sdd-format] formatted $($relevant.Count) changed C#/Razor files; rerun affected verification."
    } else { Write-Output '[sdd-format] no C#/Razor changes.' }
    if ($changed.Count -and (Test-Path 'graphify-out/graph.json') -and (Get-Command graphify -ErrorAction SilentlyContinue)) {
        & graphify update .
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Graph refresh failed; graph evidence may be stale.' }
    }
} finally { Pop-Location }
