#Requires -Version 7
# Loads the 002_product-details fixture into the LOCAL Supabase stack only: uploads the exported
# Figma artwork to the product-images bucket and replaces fixture-owned rows
# (ids 22222222-2222-4222-8222-*). Never resets the database and never touches other rows.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../../..").Path

$status = @{}
supabase status -o env --workdir $root | ForEach-Object {
    if ($_ -match '^([A-Z_]+)="?(.*?)"?$') { $status[$Matches[1]] = $Matches[2] }
}
$apiUrl = $status['API_URL']
$serviceKey = $status['SERVICE_ROLE_KEY']
if (-not $apiUrl -or $apiUrl -notmatch '^http://(127\.0\.0\.1|localhost)[:/]') {
    throw 'Local Supabase API URL not found. Run supabase start first.'
}
if (-not $serviceKey) { throw 'Local service-role key not found in supabase status.' }

foreach ($asset in Get-ChildItem "$PSScriptRoot/assets" -Filter '*.png') {
    $key = "fixtures/002_product-details/$($asset.Name)"
    Invoke-RestMethod -Method Post -Uri "$apiUrl/storage/v1/object/product-images/$key" `
        -Headers @{ Authorization = "Bearer $serviceKey"; apikey = $serviceKey; 'x-upsert' = 'true' } `
        -ContentType 'image/png' -InFile $asset.FullName | Out-Null
    Write-Output "Uploaded $key"
}

$container = docker ps --format '{{.Names}}' | Where-Object { $_ -like 'supabase_db_*' } | Select-Object -First 1
if (-not $container) { throw 'Local Supabase database container not running.' }
Get-Content -Raw "$PSScriptRoot/product-details-fixture.sql" |
    docker exec -i $container psql -U postgres -v ON_ERROR_STOP=1 -q
if ($LASTEXITCODE -ne 0) { throw 'Fixture SQL failed.' }
Write-Output 'Product-details fixture loaded.'
