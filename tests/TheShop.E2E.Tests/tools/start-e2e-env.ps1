#Requires -Version 7
# Starts the local Supabase stack, resets its database to migrations + seed, and exports
# the keys the E2E fixtures read. Existing local database data is replaced.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path

Push-Location $repoRoot
try {
    supabase start                       # no-op if already running
    supabase db reset --local            # re-applies supabase/migrations/* + supabase/seed.sql
    $environmentLines = supabase status -o env
    $environmentLines | Out-File -Encoding utf8 "$repoRoot/tests/TheShop.E2E.Tests/.e2e-env"
    Write-Host "E2E environment ready. Keys written to tests/TheShop.E2E.Tests/.e2e-env"
}
finally { Pop-Location }
