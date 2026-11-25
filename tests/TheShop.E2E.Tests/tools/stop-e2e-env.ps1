#Requires -Version 7
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path
Push-Location $repoRoot
try {
    supabase stop
    Remove-Item -Force -ErrorAction SilentlyContinue "$repoRoot/tests/TheShop.E2E.Tests/.e2e-env"
}
finally { Pop-Location }
