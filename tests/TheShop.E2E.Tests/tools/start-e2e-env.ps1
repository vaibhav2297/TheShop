#Requires -Version 7
# Starts the local Supabase stack, applies all migrations + seed, and exports the keys the
# E2E fixtures read. Idempotent — safe to re-run.
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path

Push-Location $repoRoot
try {
    supabase start                       # no-op if already running
    supabase db reset                    # re-applies supabase/migrations/* + supabase/seed.sql
    supabase status -o env | Out-File -Encoding utf8 "$repoRoot/tests/TheShop.E2E.Tests/.e2e-env"
    Write-Host "E2E environment ready. Keys written to tests/TheShop.E2E.Tests/.e2e-env"
}
finally { Pop-Location }
