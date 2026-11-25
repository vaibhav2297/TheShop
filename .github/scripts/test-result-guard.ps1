#Requires -Version 7
$ErrorActionPreference = 'Stop'
$temporaryReport = New-TemporaryFile
$guard = Join-Path $PSScriptRoot 'assert-test-results.ps1'

try {
    $cases = @(
        @{ Name = 'passing'; Accept = $true; Total = 2; Executed = 2; Passed = 2; Failed = 0; Outcome = 'Completed' },
        @{ Name = 'empty'; Accept = $false; Total = 0; Executed = 0; Passed = 0; Failed = 0; Outcome = 'Completed' },
        @{ Name = 'all skipped'; Accept = $false; Total = 2; Executed = 0; Passed = 0; Failed = 0; Outcome = 'Completed' },
        @{ Name = 'partially skipped'; Accept = $false; Total = 2; Executed = 1; Passed = 1; Failed = 0; Outcome = 'Completed' },
        @{ Name = 'failed'; Accept = $false; Total = 2; Executed = 2; Passed = 1; Failed = 1; Outcome = 'Failed' },
        @{ Name = 'aborted after passing tests'; Accept = $false; Total = 2; Executed = 2; Passed = 2; Failed = 0; Outcome = 'Aborted' }
    )
    foreach ($case in $cases) {
        $skipped = $case.Total - $case.Executed
        $xml = @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="$($case.Outcome)">
    <Counters total="$($case.Total)" executed="$($case.Executed)" passed="$($case.Passed)" failed="$($case.Failed)" error="0" notExecuted="$skipped" />
  </ResultSummary>
</TestRun>
"@
        Set-Content -LiteralPath $temporaryReport.FullName -Value $xml
        $accepted = $true
        try { & $guard -Path $temporaryReport.FullName }
        catch { $accepted = $false }
        if ($accepted -ne $case.Accept) { throw "Unexpected guard result: $($case.Name)" }
    }

    foreach ($invalid in @('<TestRun/>', 'not XML')) {
        Set-Content -LiteralPath $temporaryReport.FullName -Value $invalid
        $accepted = $true
        try { & $guard -Path $temporaryReport.FullName }
        catch { $accepted = $false }
        if ($accepted) { throw 'Guard accepted an invalid report.' }
    }

    $accepted = $true
    try { & $guard -Path ($temporaryReport.FullName + '.missing') }
    catch { $accepted = $false }
    if ($accepted) { throw 'Guard accepted a missing report.' }
    Write-Host 'All nine result-guard cases passed.'
}
finally {
    Remove-Item -LiteralPath $temporaryReport.FullName -Force
}
