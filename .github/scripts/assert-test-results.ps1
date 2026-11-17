#Requires -Version 7
param(
    [Parameter(Mandatory)]
    [string] $Path
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Test result file missing: $Path"
}

[xml] $report = Get-Content -LiteralPath $Path -Raw
$summary = $report.TestRun.ResultSummary
$counts = $summary.Counters
if ($null -eq $counts) {
    throw "Test result counters missing: $Path"
}

$total = [int] $counts.total
$executed = [int] $counts.executed
$passed = [int] $counts.passed
if ($total -le 0 -or $executed -ne $total -or $passed -ne $total -or
    [int] $counts.failed -ne 0 -or [int] $counts.error -ne 0 -or
    [int] $counts.notExecuted -ne 0 -or $summary.outcome -notin @('Completed', 'Passed')) {
    throw "Tests did not all execute and pass: total=$total, executed=$executed, passed=$passed, outcome=$($summary.outcome). See $Path."
}

Write-Host "Verified $passed passing tests: $Path"
