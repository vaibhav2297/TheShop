# format-on-stop.ps1
# Stop hook for Claude Code on The Shop project.
# Runs `dotnet format` on the solution when the current diff includes any
# .cs or .razor files. No-ops otherwise so non-code turns are fast.
# Also refreshes the graphify knowledge graph (AST-only, no API cost) when
# any file changed, so agents always query an up-to-date graph next turn.

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

# --- graphify refresh (non-fatal) -------------------------------------------
# Keep the knowledge graph current so agents can query it instead of grepping.
# Gated on $relevant (.cs / .razor), not $changed: a full `graphify update` costs
# ~38s even when nothing moved, so doc-only turns (.md / .json / .specs) skip it.
# For doc/paper/image changes, refresh the graph manually via /graphify --update.
$graphExists = Test-Path (Join-Path $repoRoot 'graphify-out/graph.json')
$graphifyCmd = Get-Command graphify -ErrorAction SilentlyContinue
if ($relevant -and $graphExists -and $graphifyCmd) {
    Write-Host "[format-on-stop] Code changes detected - running graphify update (AST-only)..."
    & graphify update .
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[format-on-stop] graphify update exited with code $LASTEXITCODE (non-fatal)."
    }
} elseif ($relevant -and $graphExists) {
    Write-Host "[format-on-stop] graphify not found on PATH - skipping graph refresh."
}

exit 0
