<#
.SYNOPSIS
    Updates and repackages the "the-vape-shop" Claude skill from the repo's source .md files.

.DESCRIPTION
    This script treats the repo's ARCHITECTURE.md and DESIGN.md as the source of truth.
    It copies them into the skill's references folder, then runs package_skill.py
    to produce a fresh .skill file ready to install in Claude Code.

    Run from the root of the vape shop repo (where ARCHITECTURE.md and DESIGN.md live).

.PARAMETER SkillSourcePath
    Path to the skill source folder (containing SKILL.md and references/).
    Defaults to "$HOME\skills\the-vape-shop".

.PARAMETER OutputPath
    Where to write the packaged .skill file.
    Defaults to "$HOME\Downloads".

.PARAMETER PackageScriptPath
    Path to the skill-creator's package_skill.py script.
    Defaults to "$HOME\skills\skill-creator".

.EXAMPLE
    .\update-skill.ps1
    Uses default paths.

.EXAMPLE
    .\update-skill.ps1 -SkillSourcePath "D:\skills\the-vape-shop" -OutputPath "D:\dist"
    Uses custom paths.
#>

[CmdletBinding()]
param(
    [string]$SkillSourcePath = (Join-Path $HOME "skills\the-vape-shop"),
    [string]$OutputPath = (Join-Path $HOME "Downloads"),
    [string]$PackageScriptPath = (Join-Path $HOME "skills\skill-creator")
)

$ErrorActionPreference = "Stop"

# ANSI color helpers (PowerShell 7+; falls back gracefully on PS5)
function Write-Step    { param($Msg) Write-Host "→ $Msg" -ForegroundColor Cyan }
function Write-Success { param($Msg) Write-Host "✓ $Msg" -ForegroundColor Green }
function Write-Warn    { param($Msg) Write-Host "⚠ $Msg" -ForegroundColor Yellow }
function Write-Fail    { param($Msg) Write-Host "✗ $Msg" -ForegroundColor Red }

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor DarkGray
Write-Host " The Vape Shop — Skill Update Script" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor DarkGray
Write-Host ""

# ───────────────────────────────────────────────────────
# Step 1: Verify we're in the repo root
# ───────────────────────────────────────────────────────
Write-Step "Verifying repo source files..."

$RepoArchPath   = Join-Path (Get-Location) "ARCHITECTURE.md"
$RepoDesignPath = Join-Path (Get-Location) "DESIGN.md"

if (-not (Test-Path $RepoArchPath)) {
    Write-Fail "ARCHITECTURE.md not found in current directory: $(Get-Location)"
    Write-Host "  Run this script from the root of the vape shop repo." -ForegroundColor DarkGray
    exit 1
}

if (-not (Test-Path $RepoDesignPath)) {
    Write-Fail "DESIGN.md not found in current directory: $(Get-Location)"
    Write-Host "  Run this script from the root of the vape shop repo." -ForegroundColor DarkGray
    exit 1
}

Write-Success "Found ARCHITECTURE.md and DESIGN.md in repo root"

# ───────────────────────────────────────────────────────
# Step 2: Verify skill source folder exists
# ───────────────────────────────────────────────────────
Write-Step "Verifying skill source folder..."

$SkillReferencesPath = Join-Path $SkillSourcePath "references"
$SkillMdPath         = Join-Path $SkillSourcePath "SKILL.md"

if (-not (Test-Path $SkillSourcePath)) {
    Write-Fail "Skill source folder not found: $SkillSourcePath"
    Write-Host "  Either create it (place SKILL.md and references/ inside)" -ForegroundColor DarkGray
    Write-Host "  or pass -SkillSourcePath to point to where it lives." -ForegroundColor DarkGray
    exit 1
}

if (-not (Test-Path $SkillMdPath)) {
    Write-Fail "SKILL.md not found in skill source folder: $SkillMdPath"
    exit 1
}

if (-not (Test-Path $SkillReferencesPath)) {
    Write-Step "Creating references folder..."
    New-Item -ItemType Directory -Path $SkillReferencesPath -Force | Out-Null
}

Write-Success "Skill source folder ready: $SkillSourcePath"

# ───────────────────────────────────────────────────────
# Step 3: Copy .md files into the skill
# ───────────────────────────────────────────────────────
Write-Step "Copying repo .md files into skill references..."

$DestArchPath   = Join-Path $SkillReferencesPath "ARCHITECTURE.md"
$DestDesignPath = Join-Path $SkillReferencesPath "DESIGN.md"

Copy-Item -Path $RepoArchPath   -Destination $DestArchPath   -Force
Copy-Item -Path $RepoDesignPath -Destination $DestDesignPath -Force

# Verify copies match (paranoia check — guards against partial writes)
$srcArchHash   = (Get-FileHash $RepoArchPath   -Algorithm SHA256).Hash
$dstArchHash   = (Get-FileHash $DestArchPath   -Algorithm SHA256).Hash
$srcDesignHash = (Get-FileHash $RepoDesignPath -Algorithm SHA256).Hash
$dstDesignHash = (Get-FileHash $DestDesignPath -Algorithm SHA256).Hash

if ($srcArchHash -ne $dstArchHash) {
    Write-Fail "ARCHITECTURE.md copy verification failed (hash mismatch)"
    exit 1
}
if ($srcDesignHash -ne $dstDesignHash) {
    Write-Fail "DESIGN.md copy verification failed (hash mismatch)"
    exit 1
}

Write-Success "Copied ARCHITECTURE.md ($([math]::Round((Get-Item $RepoArchPath).Length/1KB, 1)) KB)"
Write-Success "Copied DESIGN.md      ($([math]::Round((Get-Item $RepoDesignPath).Length/1KB, 1)) KB)"

# ───────────────────────────────────────────────────────
# Step 4: Remind about SKILL.md drift (best effort)
# ───────────────────────────────────────────────────────
Write-Step "Checking SKILL.md for potential drift..."

$skillMdContent = Get-Content $SkillMdPath -Raw
$archContent    = Get-Content $RepoArchPath -Raw
$designContent  = Get-Content $RepoDesignPath -Raw

# Light heuristic — if the SKILL.md mentions specific rule numbers or
# phrasing that should match the source, this is just a hint.
$warnings = @()

if ($archContent -notmatch "Clean Architecture" -and $skillMdContent -match "Clean Architecture") {
    $warnings += "SKILL.md mentions 'Clean Architecture' but ARCHITECTURE.md may have changed terminology"
}
if ($designContent -notmatch "Shop" -and $skillMdContent -match "Shop") {
    $warnings += "SKILL.md mentions 'Shop' prefix but DESIGN.md may have changed naming"
}

if ($warnings.Count -gt 0) {
    Write-Warn "Potential drift detected — SKILL.md summary may need manual update:"
    foreach ($w in $warnings) { Write-Host "  • $w" -ForegroundColor DarkYellow }
    Write-Host ""
    Write-Host "  Review SKILL.md at: $SkillMdPath" -ForegroundColor DarkGray
    Write-Host "  The 12-rule summary in SKILL.md must stay aligned with the references." -ForegroundColor DarkGray
    Write-Host ""
} else {
    Write-Success "No obvious drift between SKILL.md and references"
}

# ───────────────────────────────────────────────────────
# Step 5: Verify Python and package_skill.py are available
# ───────────────────────────────────────────────────────
Write-Step "Locating package_skill.py..."

$packageScriptFull = Join-Path $PackageScriptPath "scripts\package_skill.py"

if (-not (Test-Path $packageScriptFull)) {
    Write-Fail "package_skill.py not found at: $packageScriptFull"
    Write-Host "  Pass -PackageScriptPath to point at your skill-creator folder." -ForegroundColor DarkGray
    exit 1
}

# Check Python is on PATH
$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonCmd) {
    $pythonCmd = Get-Command python3 -ErrorAction SilentlyContinue
}
if (-not $pythonCmd) {
    Write-Fail "Python is not installed or not on PATH"
    Write-Host "  Install Python 3.8+ from python.org and ensure it's in PATH." -ForegroundColor DarkGray
    exit 1
}

Write-Success "Found Python: $($pythonCmd.Source)"

# ───────────────────────────────────────────────────────
# Step 6: Ensure output directory exists
# ───────────────────────────────────────────────────────
if (-not (Test-Path $OutputPath)) {
    Write-Step "Creating output directory: $OutputPath"
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# ───────────────────────────────────────────────────────
# Step 7: Run package_skill.py
# ───────────────────────────────────────────────────────
Write-Step "Running package_skill.py..."
Write-Host ""

# Move into the skill-creator dir so the relative module imports resolve
Push-Location $PackageScriptPath
try {
    & $pythonCmd.Source -m scripts.package_skill $SkillSourcePath $OutputPath
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

Write-Host ""

if ($exitCode -ne 0) {
    Write-Fail "package_skill.py exited with code $exitCode"
    Write-Host "  Check the validation errors above and fix the skill source." -ForegroundColor DarkGray
    exit $exitCode
}

# ───────────────────────────────────────────────────────
# Step 8: Confirm output file exists
# ───────────────────────────────────────────────────────
$skillFile = Join-Path $OutputPath "the-vape-shop.skill"

if (-not (Test-Path $skillFile)) {
    Write-Fail "Expected output file not found: $skillFile"
    Write-Host "  package_skill.py reported success but the file is missing." -ForegroundColor DarkGray
    exit 1
}

$skillSize = [math]::Round((Get-Item $skillFile).Length / 1KB, 1)

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor DarkGray
Write-Success "Skill repackaged successfully"
Write-Host ""
Write-Host "  Output: $skillFile" -ForegroundColor White
Write-Host "  Size:   $skillSize KB" -ForegroundColor White
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor DarkGray
Write-Host ""

# ───────────────────────────────────────────────────────
# Step 9: Reinstall instructions
# ───────────────────────────────────────────────────────
Write-Host "Next step — reinstall the skill in Claude Code:" -ForegroundColor White
Write-Host ""
Write-Host "  claude" -ForegroundColor Cyan
Write-Host "  /skill install `"$skillFile`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "Then run /compact in your active session, or restart it," -ForegroundColor DarkGray
Write-Host "to make sure Claude Code picks up the updated rules." -ForegroundColor DarkGray
Write-Host ""
