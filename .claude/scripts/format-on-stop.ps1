# format-on-stop.ps1
# Stop hook for Claude Code on The Shop project.
# Runs `dotnet format` on the solution when the current diff includes any
# .cs or .razor files. No-ops otherwise so non-code turns are fast.

$ErrorActionPreference = 'SilentlyContinue'

# Locate the solution root (this script lives at .claude/scripts/ inside the repo).
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $repoRoot

# Collect changed files: unstaged + staged. Added / Copied / Modified only.
$changed = @()
$changed += & git diff --name-only --diff-filter=ACM 2>$null
$changed += & git diff --staged --name-only --diff-filter=ACM 2>$null

$relevant = $changed | Where-Object { $_ -match '\.(cs|razor)$' }

if ($relevant) {
    Write-Host "[format-on-stop] $($relevant.Count) C# / Razor file(s) changed — running dotnet format..."
    & dotnet format TheShop.slnx --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[format-on-stop] dotnet format exited with code $LASTEXITCODE (non-fatal - files may still need attention)."
    }
} else {
    Write-Host "[format-on-stop] No .cs / .razor changes in diff - skipping format."
}

exit 0
