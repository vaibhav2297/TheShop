[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$rows=@(foreach($file in Get-ChildItem (Join-Path $root '.sdd/reports') -Filter 'native-*.json' -File) {
    $report=Read-Utf8 $file.FullName | ConvertFrom-Json -AsHashtable
    if (-not $report.Contains('calls') -or -not $report.Contains('case')) { continue }
    $inputTokens=0L; $cacheRead=0L; $cacheWrite=0L; $outputTokens=0L; $hasUsage=$false
    foreach($call in $report.calls) {
        foreach($entry in $call.usage) {
            # Only terminal usage: avoid counting intermediate message usage twice.
            if($entry.type -notin @('result','turn.completed')){continue}
            $usage=$entry.usage; $hasUsage=$true
            if($usage.Contains('input_tokens')){$inputTokens+=$usage.input_tokens}
            if($usage.Contains('cache_read_input_tokens')){$cacheRead+=$usage.cache_read_input_tokens}
            if($usage.Contains('cached_input_tokens')){$cacheRead+=$usage.cached_input_tokens}
            if($usage.Contains('cache_creation_input_tokens')){$cacheWrite+=$usage.cache_creation_input_tokens}
            if($usage.Contains('output_tokens')){$outputTokens+=$usage.output_tokens}
        }
    }
    $accepted=$report.passed; $correction=$null
    $environmentErrors=@()
    foreach($log in @(Get-ChildItem -LiteralPath $report.logs -Filter '*-events.jsonl' -File)) {
        foreach($line in [IO.File]::ReadLines($log.FullName)) {
            try {
                $event=$line|ConvertFrom-Json -AsHashtable -ErrorAction Stop
                if($event.type -eq 'error' -and $event.Contains('message') -and $event.message -match 'usage limit|at capacity') {$environmentErrors+=$event.message}
            } catch {}
        }
    }
    if($report.case -in @('test-merged','test-separate')) {
        $proofPath=Join-Path $report.logs 'tests.json';$fixedPath=Join-Path $report.logs 'corrected-tests.json'
        if((Test-Path $proofPath) -and (Test-Path $fixedPath)) {
            $bad=Read-Utf8 $proofPath|ConvertFrom-Json -AsHashtable;$good=Read-Utf8 $fixedPath|ConvertFrom-Json -AsHashtable
            $otherFailures=@($report.checks.Keys | Where-Object {$_ -ne 'detectedSeededDefect' -and -not $report.checks[$_]})
            $accepted=$bad.exitCode -ne 0 -and $good.exitCode -eq 0 -and $otherFailures.Count -eq 0
            if($accepted -and -not $report.passed){$correction='Original evaluator required literal AssertionError. Runner caught assertions and printed failures instead. Paired failure/pass proves seeded defect detection. Original report retained.'}
        }
    }
    @{runtime=$report.runtime;case=$report.case;repeat=$report.repeat;accepted=$accepted;environmentBlocked=$environmentErrors.Count -gt 0;environmentErrors=@($environmentErrors|Select-Object -Unique);originalPassed=$report.passed;assessmentCorrection=$correction;seconds=[Math]::Round($report.elapsedSeconds,2);calls=$report.calls.Count;usageAvailable=$hasUsage;inputTokens=$inputTokens;cacheReadTokens=$cacheRead;cacheWriteTokens=$cacheWrite;outputTokens=$outputTokens;report=$file.Name}
})
$output=@{schemaVersion=1;checkedUtc=[DateTime]::UtcNow.ToString('o');samples=$rows;decision='Retain existing explicit production workflows. Synthetic pilot insufficient to promote defaults.';usageCaveat='Claude input excludes separately reported caches; Codex input may include cached tokens. Fields retained separately, never summed across runtimes as comparable spend.';optimization='No candidate adopted; see optimization-validation.json.'}
Write-Json (Join-Path $root '.sdd/reports/execution-evaluation.json') $output
foreach($row in $rows) { Write-Output "$($row.runtime)/$($row.case)/$($row.repeat): accepted=$($row.accepted); seconds=$($row.seconds); outputTokens=$($row.outputTokens); usageAvailable=$($row.usageAvailable)" }
