# Additive manifest validation; legacy entries default to unit proof.
function Get-TestProofIssues($Manifest, $E2eManifest = $null) {
    $seen = @{}
    foreach ($ac in @($Manifest.acceptanceCriteria)) {
        if ($seen.ContainsKey([string]$ac.id)) { "Duplicate acceptance criterion: $($ac.id)" }
        $seen[[string]$ac.id] = $true
        $proof = if ($ac.PSObject.Properties['proof']) { [string]$ac.proof } else { 'unit' }
        if ($proof -cnotin @('unit','e2e','manual')) { "Invalid proof classification for $($ac.id): $proof"; continue }
        if ($proof -eq 'unit') { continue }
        if (-not $ac.PSObject.Properties['reason'] -or $ac.reason -isnot [string] -or [string]::IsNullOrWhiteSpace($ac.reason)) {
            "Deferred $($ac.id) needs a reason."
        }
        if ($null -ne $E2eManifest) {
            $matches = @($E2eManifest.acceptanceCriteria | Where-Object { $_.id -ceq $ac.id })
            $allowed = if ($proof -eq 'e2e') { @('e2e') } else { @('e2e','manual') }
            if ($matches.Count -ne 1 -or $matches[0].coverage -cnotin $allowed) {
                "Deferred $($ac.id) lacks matching browser or human proof classification."
            }
        }
    }
}
