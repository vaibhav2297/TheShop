#Requires -Version 7
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
. "$PSScriptRoot/visual-evidence.ps1"
$feature = 'visual-proof-' + [guid]::NewGuid().ToString('N')
$featureRoot = Join-Path $root ".specs/$feature"
$work = Join-Path $root ".sdd/.test-work/$feature"
$server = $null
$checks = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw "FAIL: $Message" }
    $script:checks++
}
function Get-Issues { @(Test-VisualEvidence $root $feature) }
function Write-Review {
    $hash = (Get-FileHash -LiteralPath "$featureRoot/evidence/visual/dialog/capture.md").Hash
    @("**Capture SHA256:** $hash", '**Reviewer:** automated fixture contract test',
      '**Checks:** fixture layout and modal state', '**Findings:** test evidence only; not a product fidelity claim', '**Verdict:** PASS') |
        Set-Content -LiteralPath "$featureRoot/evidence/visual/dialog/review.md"
}
try {
    New-Item -ItemType Directory -Path $featureRoot,$work -Force | Out-Null
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $server = Start-Job -ArgumentList $port -ScriptBlock {
        param($port)
        $http = [Net.HttpListener]::new()
        $http.Prefixes.Add("http://localhost:$port/")
        $http.Start()
        try {
            while ($http.IsListening) {
                $pending = $http.GetContextAsync()
                while (!$pending.IsCompleted) { Start-Sleep -Milliseconds 100 }
                $context = $pending.GetAwaiter().GetResult()
                $color = if ($context.Request.QueryString['variant'] -eq 'changed') { 'blue' } else { 'red' }
                $html = "<!doctype html><html><head><style>body{margin:0;background:white;font-family:Arial}#fixture{padding:20px}#dialog{width:100px;height:60px;background:$color}button{width:80px;height:32px}</style></head><body><main id='fixture' data-testid='fixture'><button id='open' onclick='document.getElementById(`"dialog`").hidden=false'>Open</button><div id='dialog' data-testid='dialog' hidden>Dialog</div></main></body></html>"
                $bytes = [Text.Encoding]::UTF8.GetBytes($html)
                $context.Response.ContentType = 'text/html'
                $context.Response.OutputStream.Write($bytes,0,$bytes.Length)
                $context.Response.Close()
            }
        } finally { $http.Close() }
    }
    $url = "http://localhost:$port"
    $ready = $false
    for ($i=0; $i -lt 40; $i++) {
        try { $null = Invoke-WebRequest "$url/" -TimeoutSec 1; $ready = $true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    Assert-True $ready 'local fixture starts'
    & dotnet build "$root/.sdd/tools/VisualCapture/VisualCapture.csproj" --nologo
    if ($LASTEXITCODE) { throw 'Capture helper build failed.' }
    $dll = "$root/.sdd/tools/VisualCapture/bin/Debug/net10.0/VisualCapture.dll"
    & dotnet $dll --url "$url/" --output "$work/reference" --width 400 --height 300 --ready '#fixture' --click '#open'
    Assert-True ($LASTEXITCODE -eq 0) 'reference fixture capture succeeds'
    $reference = "$work/reference/actual.png"
    @"
# Fixture plan
**Visual scope:** required
### Visual targets
| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| dialog | / | 400x300 | open dialog | https://www.figma.com/design/fixture/Smoke?node-id=1-2 |
"@ | Set-Content -LiteralPath "$featureRoot/plan.md"
    Set-Content -LiteralPath "$featureRoot/spec.md" -Value '# Visual fixture spec'
    Assert-True ((Get-Issues).Count -gt 0) 'missing captures fail gate'
    & "$PSScriptRoot/capture-ui.ps1" -Feature $feature -Surface dialog -Reference $reference -ReferenceRevision 'fixture-v1' -Ready '#fixture' -BaseUrl $url -BrowserArguments @('--click','#open','--wait-for','#dialog')
    $evidence = "$featureRoot/evidence/visual/dialog"
    Assert-True ((Get-Content "$evidence/browser.md" -Raw) -match 'Changed pixels:\*\* 0/') 'identical fixture produces zero changed pixels'
    Assert-True ((Get-Issues).Count -gt 0) 'capture alone cannot pass visual gate'
    Write-Review
    Assert-True ((Get-Issues).Count -eq 0) 'review bound to current capture passes integrity gate'
    & pwsh -NoProfile -File "$PSScriptRoot/check-sdd-gates.ps1" visual -Feature $feature
    Assert-True ($LASTEXITCODE -eq 0) 'visual gate dispatch accepts complete evidence'

    Add-Content -LiteralPath "$featureRoot/spec.md" -Value 'Changed acceptance condition'
    Assert-True (((Get-Issues) -join ';') -match 'source/spec/plan changed') 'spec edit invalidates capture'
    Set-Content -LiteralPath "$featureRoot/spec.md" -Value '# Visual fixture spec'
    $oldBytes = [IO.File]::ReadAllBytes("$evidence/actual.png")
    [IO.File]::WriteAllBytes("$evidence/actual.png", [byte[]](1,2,3))
    Assert-True (((Get-Issues) -join ';') -match 'actual.png changed') 'image tampering invalidates capture'
    [IO.File]::WriteAllBytes("$evidence/actual.png", $oldBytes)
    Add-Content -LiteralPath "$evidence/capture.md" -Value 'New capture'
    Assert-True (((Get-Issues) -join ';') -match 'review does not match capture') 'old review cannot approve new capture'

    & dotnet $dll --url "$url/?variant=changed" --output "$work/changed" --width 400 --height 300 --ready '#fixture' --click '#open' --reference $reference
    Assert-True ($LASTEXITCODE -eq 0) 'changed fixture generates comparison artifacts'
    Assert-True ((Get-Content "$work/changed/browser.md" -Raw) -match 'Changed pixels:\*\* [1-9]') 'visible color regression appears in difference'
    & dotnet $dll --url "$url/" --output "$work/mismatch" --width 401 --height 300 --ready '#fixture' --reference $reference
    Assert-True ($LASTEXITCODE -ne 0) 'dimension mismatch fails instead of stretching reference'
    & dotnet $dll --url 'https://example.com/' --output "$work/remote" --width 400 --height 300 --ready '#fixture'
    Assert-True ($LASTEXITCODE -ne 0) 'nonlocal capture rejected'
    Assert-True (@(Get-VisualTargets '# UI plan without Figma').Count -eq 0) 'UI without Figma uses normal flow without scope or evidence'
    Assert-True (@(Get-VisualTargets '**Visual scope:** none').Count -eq 0) 'no Figma requires no exclusion reason'
    Assert-True (@(Get-VisualTargets 'Reference: reference.png').Count -eq 0) 'local screenshot alone does not trigger Figma flow'
    Assert-True (@(Get-VisualTargets 'https://www.figma.com.example.org/design/fixture/Smoke').Count -eq 0) 'unrelated host does not trigger Figma flow'
    $rejected = $false
    try { Get-VisualTargets "**Visual scope:** none`nhttps://www.figma.com/design/fixture/Smoke" } catch { $rejected = $true }
    Assert-True $rejected 'provided Figma URL cannot be disabled with scope none'
    $rejected = $false
    try { Get-VisualTargets 'https://figma.com/file/fixture/Smoke' } catch { $rejected = $true }
    Assert-True $rejected 'legacy Figma plan still requires visual targets'
    $savedPlan = Get-Content -LiteralPath "$featureRoot/plan.md" -Raw
    Set-Content -LiteralPath "$featureRoot/plan.md" -Value '# Normal UI plan without Figma'
    Assert-True ((Get-Issues).Count -eq 0) 'no-Figma gate ignores old visual artifacts'
    & pwsh -NoProfile -File "$PSScriptRoot/check-sdd-gates.ps1" visual -Feature $feature
    Assert-True ($LASTEXITCODE -eq 0) 'visual gate dispatch does not block normal UI flow'
    Set-Content -LiteralPath "$featureRoot/plan.md" -Value $savedPlan
    $badReference = "$work/not-image.png"
    Set-Content -LiteralPath $badReference -Value 'not an image'
    $rejected = $false
    try {
        & "$PSScriptRoot/capture-ui.ps1" -Feature $feature -Surface dialog -Reference $badReference -ReferenceRevision fixture-v2 -Ready '#fixture' -BaseUrl $url
    } catch { $rejected = $true }
    Assert-True $rejected 'invalid reference fails capture'
    Assert-True ((Get-VisualField (Get-Content "$evidence/capture.md" -Raw) 'Verdict') -eq 'INCOMPLETE') 'failed recapture invalidates previous success'
    Assert-True ((Get-Issues).Count -gt 0) 'failed recapture cannot reuse old review'
    Assert-True ((Invoke-WebRequest "$url/" -TimeoutSec 2).StatusCode -eq 200) 'capture never stops external server'
    Write-Output "Visual flow: $checks checks passed. Diagnostic images: $work"
} finally {
    if ($server) { Stop-Job $server; Remove-Job $server }
    $resolved = [IO.Path]::GetFullPath($featureRoot)
    $expected = [IO.Path]::GetFullPath((Join-Path $root ".specs/$feature"))
    if ($resolved -eq $expected -and $feature -match '^visual-proof-[a-f0-9]{32}$' -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
