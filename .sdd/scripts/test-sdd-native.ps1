# Bounded native pilots. No production edits, connectors, dependencies, or global configuration changes.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('claude','codex')][string]$Runtime,
    [Parameter(Mandatory)][ValidateSet('routing','single','layered','test-merged','test-separate')][string]$Case,
    [ValidateRange(1,5)][int]$Repeat=1,
    [ValidateRange(30,600)][int]$TimeoutSeconds=240,
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [string]$Executable
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$runId="$Runtime-$Case-$Repeat-"+[Guid]::NewGuid().ToString('N')
$fixture=Resolve-WorkspacePath ([IO.Path]::GetTempPath()) ("TheShop-strategy-$runId")
$logs=Resolve-WorkspacePath $root ".sdd/.test-work/strategy/$runId"
[IO.Directory]::CreateDirectory($fixture) | Out-Null
[IO.Directory]::CreateDirectory($logs) | Out-Null
$pwsh=(Get-Process -Id $PID).Path
if (-not $Executable) { $Executable=Join-Path ([Environment]::GetFolderPath('UserProfile')) "AppData/Roaming/npm/$Runtime.ps1" }
if (-not (Test-Path -LiteralPath $Executable)) { throw "Runtime unavailable: $Executable" }
$calls=[Collections.Generic.List[object]]::new()
function Native([string]$Name,[string]$Task,[string[]]$Owned) {
    $prompt="Authorized isolated evaluation. Work only in $fixture. Own only: $($Owned -join ', '). Other contexts may work here; preserve their edits. No packages, network, git, production files, external services, or subagents. Default Caveman full; preserve exact literals. $Task"
    Write-Utf8 (Join-Path $logs "$Name-prompt.md") $prompt
    $before=@{}
    foreach ($file in @(Get-ChildItem -LiteralPath $fixture -File -Recurse)) { $before[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash }
    $arguments=if($Runtime -eq 'codex'){@('exec','--skip-git-repo-check','--ephemeral','--sandbox','workspace-write','--json','--output-last-message',(Join-Path $logs "$Name-final.txt"),'-C',$fixture,$prompt)}else{@('-p',$prompt,'--output-format','stream-json','--verbose','--permission-mode','default','--allowedTools','Read,Glob,Grep,Write,Edit,Bash(node *)')}
    $launcher=$Executable
    if ($Executable.EndsWith('.ps1')) { $arguments=@('-NoProfile','-File',$Executable)+$arguments; $launcher=$pwsh }
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $result=Invoke-Captured $launcher $arguments $fixture $TimeoutSeconds
    $timer.Stop()
    Write-Utf8 (Join-Path $logs "$Name-events.jsonl") $result.stdout
    Write-Utf8 (Join-Path $logs "$Name-errors.txt") $result.stderr
    $usage=@(); $models=@()
    foreach ($line in $result.stdout -split "`n") {
        try {
            $event=$line | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            if ($event.ContainsKey('usage') -and $event.usage) { $usage+=@{type=$event.type;usage=$event.usage} }
            if ($event.ContainsKey('modelUsage')) { $models+=@($event.modelUsage.Keys) }
            if ($event.ContainsKey('message') -and $event.message -is [Collections.IDictionary] -and $event.message.Contains('model')) { $models+=$event.message.model }
        } catch {}
    }
    $violations=@()
    foreach ($path in @($before.Keys)+@(Get-ChildItem -LiteralPath $fixture -File -Recurse | ForEach-Object FullName) | Select-Object -Unique) {
        $relative=[IO.Path]::GetRelativePath($fixture,$path).Replace('\','/')
        $after=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{$null}
        if ($before[$path] -cne $after -and $relative -notin $Owned) { $violations+=$relative }
    }
    $call=@{name=$Name;exitCode=$result.exitCode;elapsedSeconds=$timer.Elapsed.TotalSeconds;usage=$usage;models=@($models|Select-Object -Unique);scopeViolations=$violations}
    $calls.Add($call)
    return $result.exitCode -eq 0 -and $violations.Count -eq 0
}
$spec=@'
Synthetic Node.js CommonJS four-boundary fixture; no packages. This measures orchestration overhead, not The Shop production workflow fidelity.
Domain.js exports add(items,label). items is array of strings. Trim label first. Reject empty with error 'empty'; trimmed length >40 with 'length'; case-insensitive duplicate with 'duplicate'; sixth addition with 'capacity'. Check in that order. Success {ok:true,items:[...items,trimmed]}; rejection {ok:false,items:[...items],error}. Never mutate input.
Infrastructure.js exports createRepository(): private in-memory array; read() and write(items) clone arrays, preventing alias mutation.
Application.js exports createChecklist(repo): add(label) reads repository, calls Domain.add, writes only on success, returns result; list() reads repository.
Web.js exports render(result): JSON.stringify(result), exact result preserved. This is a serialization boundary, not production UI.
Use require('./Domain.js') from Application.js. All files at fixture root. No additional APIs or features.
'@
Write-Utf8 (Join-Path $fixture 'spec.md') $spec
$oracle=@'
const assert=require('node:assert/strict');
const {add}=require('./Domain.js');
const {createRepository}=require('./Infrastructure.js');
const {createChecklist}=require('./Application.js');
const {render}=require('./Web.js');
const initial=['One']; const success=add(initial,' Two ');
assert.deepEqual(success,{ok:true,items:['One','Two']});assert.deepEqual(initial,['One']);
for(const [value,error] of [[' ','empty'],['x'.repeat(41),'length'],[' one ','duplicate']]) assert.deepEqual(add(initial,value),{ok:false,items:['One'],error});
assert.equal(add([], ' '+ 'x'.repeat(40)+' ').ok,true);
assert.deepEqual(add(['a','b','c','d','e'],'f'),{ok:false,items:['a','b','c','d','e'],error:'capacity'});
assert.equal(add(['a','b','c','d','e'],' a ').error,'duplicate');
const repo=createRepository();const values=['base'];repo.write(values);values.push('bad');assert.deepEqual(repo.read(),['base']);const copy=repo.read();copy.push('bad');assert.deepEqual(repo.read(),['base']);
const service=createChecklist(repo);assert.equal(service.add(' next ').ok,true);assert.equal(service.add('NEXT').ok,false);assert.deepEqual(service.list(),['base','next']);
assert.equal(render(success),JSON.stringify(success));console.log('Oracle assertions passed');
'@
$checks=[ordered]@{}
if ($Case -eq 'routing') {
    foreach ($name in @('task-routing','execution','evidence','communication')) { Write-Utf8 (Join-Path $fixture "$name.md") (Read-Utf8 (Join-Path $root ".sdd/contracts/$name.md")) }
    $checks.native=Native 'routing' ((Read-Utf8 (Join-Path $root '.sdd/evals/routing.md'))+' Contracts copied at fixture root.') @('answer.json')
    try {
        $answer=Read-Utf8 (Join-Path $fixture 'answer.json') | ConvertFrom-Json -AsHashtable
        foreach ($pair in @(@('feature','lean-build'),@('diagnosis','investigate-first'),@('patch','surgical-patch'),@('refactor','safe-refactor'),@('migration','migration'),@('verification','verify-and-stop'),@('review','theshop-review'))) { $checks[$pair[0]]=$answer[$pair[0]] -ceq $pair[1] }
        foreach ($field in @('missingHelper','missingDelegation','explicitOnly','staleHandoff','requirementGap')) { $checks[$field]=$answer[$field] -ceq $false }
    } catch { $checks.answer=$false }
} elseif ($Case -in @('single','layered')) {
    if ($Case -eq 'single') { $checks.native=Native 'single' 'Read spec.md; implement all four files. Verify with local Node checks if useful. Stop after complete.' @('Domain.js','Application.js','Infrastructure.js','Web.js') }
    else {
        # Fresh isolated native contexts model handoff boundaries; explicit API fixed in spec.
        foreach ($file in @('Domain.js','Application.js','Infrastructure.js','Web.js')) {
            $checks[$file]=Native $file "Read full spec.md and existing upstream files if needed. Implement only $file against exact contracts. Return produced API. No test files." @($file)
            if (-not $checks[$file]) { break }
        }
    }
    Write-Utf8 (Join-Path $fixture 'oracle.js') $oracle
    $proof=Invoke-Captured 'node' @('oracle.js') $fixture 60
    Write-Json (Join-Path $logs 'oracle.json') $proof
    $checks.oracle=$proof.exitCode -eq 0
} else {
    # Deliberately faulty implementation: duplicate check is case-sensitive.
    Write-Utf8 (Join-Path $fixture 'Domain.js') "exports.add=(items,label)=>{label=label.trim();const error=!label?'empty':label.length>40?'length':items.includes(label)?'duplicate':items.length>=5?'capacity':null;return error?{ok:false,items:[...items],error}:{ok:true,items:[...items,label]};};"
    Write-Utf8 (Join-Path $fixture 'Infrastructure.js') 'exports.createRepository=()=>{let items=[];return {read:()=>[...items],write:value=>{items=[...value]}}};'
    Write-Utf8 (Join-Path $fixture 'Application.js') "exports.createChecklist=repo=>({add:label=>{const result=require('./Domain.js').add(repo.read(),label);if(result.ok)repo.write(result.items);return result;},list:()=>repo.read()});"
    Write-Utf8 (Join-Path $fixture 'Web.js') 'exports.render=result=>JSON.stringify(result);'
    $task='Read spec.md. Write tests.js using node:assert/strict to test requirements across four boundaries. Do not edit production files. '
    if ($Case -eq 'test-merged') { $checks.native=Native 'merged' ($task+'Run node tests.js. Write result.json with exitCode and defect description. Preserve failing assertions; do not weaken tests.') @('tests.js','result.json') }
    else {
        $checks.writer=Native 'writer' ($task+'Write tests only; separate runner will execute. Stop after tests exist.') @('tests.js')
        if ($checks.writer) { $checks.runner=Native 'runner' 'Run node tests.js; no edits to tests or source. Write only result.json with actual exitCode and defect description.' @('result.json') }
    }
    $proof=Invoke-Captured 'node' @('tests.js') $fixture 60
    Write-Json (Join-Path $logs 'tests.json') $proof
    $checks.detectedSeededDefect=$proof.exitCode -ne 0
    try { $answer=Read-Utf8 (Join-Path $fixture 'result.json') | ConvertFrom-Json -AsHashtable; $checks.honestReport=[int]$answer.exitCode -ne 0 } catch { $checks.honestReport=$false }
    # Counterfactual: same generated tests must pass corrected behavior, excluding trivial always-fail tests.
    $path=Join-Path $fixture 'Domain.js'
    Write-Utf8 $path ((Read-Utf8 $path).Replace('items.includes(label)','items.some(item=>item.toLowerCase()===label.toLowerCase())'))
    $fixed=Invoke-Captured 'node' @('tests.js') $fixture 60
    Write-Json (Join-Path $logs 'corrected-tests.json') $fixed
    $checks.correctedBehaviorPasses=$fixed.exitCode -eq 0
}
$failed=@($checks.Keys | Where-Object { -not $checks[$_] })
$artifacts=Join-Path $logs 'artifacts'
[IO.Directory]::CreateDirectory($artifacts) | Out-Null
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $artifacts
$report=[ordered]@{schemaVersion=1;checkedUtc=[DateTime]::UtcNow.ToString('o');runtime=$Runtime;case=$Case;repeat=$Repeat;fixture=$fixture;logs=$logs;passed=$failed.Count -eq 0;checks=$checks;calls=$calls;elapsedSeconds=($calls|Measure-Object elapsedSeconds -Sum).Sum;retries=0;humanCorrections=0;limitation='Bounded synthetic CommonJS pilot. Layered contexts sequential, not full SDD agents or parallel Infrastructure/Web. Native reported usage retained raw; compare within runtime only. No production default justified by this sample.'}
Write-Json (Join-Path $root ".sdd/reports/native-$Runtime-$Case-$Repeat.json") $report
Write-Output "$Runtime/${Case}: passed=$($report.passed); failed=$($failed -join ', '); logs=$logs"
if ($failed.Count) { exit 1 }
