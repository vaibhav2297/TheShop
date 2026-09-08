# Offline, reversible candidates only. Does not activate a proxy or compress live instructions.
[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$work=Resolve-WorkspacePath $root ('.sdd/.test-work/optimization-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
function Trial([string]$Name,[string]$Original) {
    # Run-length encoding preserves line order, delimiters, counts, and exact text.
    $runs=[Collections.Generic.List[object]]::new()
    foreach ($match in [regex]::Matches($Original,'[^\r\n]*(?:\r\n|\r|\n)|[^\r\n]+$')) {
        $line=$match.Value
        if ($runs.Count -and $runs[$runs.Count-1].text -ceq $line) { $runs[$runs.Count-1].count++ }
        else { $runs.Add(@{text=$line;count=1}) }
    }
    $candidate=$runs | ConvertTo-Json -Depth 8 -Compress
    if ($runs.Count -eq 0) { $candidate='[]' }
    $expanded=[Text.StringBuilder]::new()
    foreach ($run in @($candidate | ConvertFrom-Json)) { for($i=0;$i -lt $run.count;$i++){[void]$expanded.Append($run.text)} }
    $recovered=$expanded.ToString()
    $same=$Original -ceq $recovered
    $before=[Text.Encoding]::UTF8.GetByteCount($Original)
    $after=[Text.Encoding]::UTF8.GetByteCount($candidate)
    Write-Utf8 (Join-Path $work "$Name-original.txt") $Original
    Write-Utf8 (Join-Path $work "$Name-candidate.json") $candidate
    # Raw bytes additionally preserve CRLF if Write-Utf8 normalized display copy.
    [IO.File]::WriteAllBytes((Join-Path $work "$Name-original.bin"),[Text.Encoding]::UTF8.GetBytes($Original))
    [IO.File]::WriteAllBytes((Join-Path $work "$Name-restored.bin"),[Text.Encoding]::UTF8.GetBytes($recovered))
    return @{name=$Name;originalBytes=$before;candidateBytes=$after;roundTripExact=$same;originalHash=(Get-FileHash (Join-Path $work "$Name-original.bin")).Hash;restoredHash=(Get-FileHash (Join-Path $work "$Name-restored.bin")).Hash;byteReductionPercent=if($before){[Math]::Round(100*(1-$after/$before),2)}else{0};adopted=$false;reason='Byte result only. No tokenizer or model-quality proof; candidate stays offline.'}
}
$log=(('PASS duplicate fixture line' + "`r`n")*100)+"FAIL AC-9: Expected 12 tests; discovered 9. Feature is NOT READY.`r`n"+('PASS final line'+"`r`n")*20
$results=@((Trial 'tool-output' $log),(Trial 'memory' ([IO.File]::ReadAllText((Join-Path $root '.sdd/contracts/execution.md')))),(Trial 'edge-cases' "Line`r`nLine`r`n`r`nno trailing newline"),(Trial 'empty' ''))
$proxy=Invoke-Captured 'node' @((Join-Path $root '.sdd/evals/proxy-roundtrip.cjs')) $work 60
Write-Json (Join-Path $work 'proxy-process.json') $proxy
$proxyResult=if($proxy.exitCode -eq 0){$proxy.stdout|ConvertFrom-Json -AsHashtable}else{@{passed=$false;reason=$proxy.stderr}}
$passed=@($results|Where-Object {-not $_.roundTripExact -or $_.originalHash -cne $_.restoredHash}).Count -eq 0 -and $proxyResult.passed
$report=@{schemaVersion=1;checkedUtc=[DateTime]::UtcNow.ToString('o');passed=$passed;trials=$results;proxy=$proxyResult;artifacts=$work;adopted=@();rollback='No runtime setting changed. Originals and restored byte copies retained in fixture.';limitation='Synthetic repeated tool output, actual shared execution memory, local HTTP passthrough. No Caveman Cloud, real model proxy routing, streaming, credentials, token-saving, or production quality claims.'}
Write-Json (Join-Path $root '.sdd/reports/optimization-validation.json') $report
Write-Output "Optimization roundtrip checks passed=$passed; adopted=none; artifacts=$work"
if(-not $passed){exit 1}
