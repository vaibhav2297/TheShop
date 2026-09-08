# Native-client checks. Discovery is read-only; live cases use isolated workspaces under .sdd.
[CmdletBinding()]
param(
    [ValidateSet('discover','codex','claude')][string]$Runtime='discover',
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [string]$Executable,
    [string]$FixtureRoot,
    [ValidateRange(30,600)][int]$TimeoutSeconds=300,
    [ValidateSet('Draft','Confirmed')][string]$ExpectedState
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
function Find-Runtime([string]$Name) {
    $candidates=@()
    $command=Get-Command "$Name.exe" -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $profile=[Environment]::GetFolderPath('UserProfile')
    $candidates += (Join-Path $profile ".local/bin/$Name.exe")
    $candidates += (Join-Path $profile "AppData/Roaming/npm/$Name.ps1")
    $extensionRoot=Join-Path $profile '.vscode/extensions'
    $pattern=if($Name -eq 'claude'){'anthropic.claude-code*'}else{'openai.chatgpt*'}
    if (Test-Path $extensionRoot) {
        foreach ($extension in (Get-ChildItem $extensionRoot -Directory -Filter $pattern -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            $candidates += @(Get-ChildItem $extension.FullName -Recurse -File -Filter "$Name.exe" -ErrorAction SilentlyContinue | ForEach-Object FullName)
        }
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $candidate) {
            try {
                $version = if ($candidate.EndsWith('.ps1')) {
                    Invoke-Captured (Get-Process -Id $PID).Path @('-NoProfile','-File',$candidate,'--version') $root 30
                } else { Invoke-Captured $candidate @('--version') $root 30 }
                if ($version.exitCode -eq 0) { return @{ available=$true; executable=$candidate; version=$version.stdout.Trim() } }
            } catch { continue }
        }
    }
    return @{ available=$false; executable=$null; version=$null; reason='Native executable was not found in PATH or known local/VS Code installation locations.' }
}
if ($Runtime -eq 'discover') {
    $discovery=[ordered]@{ checkedUtc=[DateTime]::UtcNow.ToString('o'); codex=(Find-Runtime 'codex'); claude=(Find-Runtime 'claude') }
    Write-Json (Join-Path $root '.sdd/reports/runtime-discovery.json') $discovery
    $discovery | ConvertTo-Json -Depth 5
    exit 0
}
if (-not $FixtureRoot) { throw 'Live checks require an explicitly prepared -FixtureRoot under .sdd/.test-work.' }
$fixture=[IO.Path]::GetFullPath($FixtureRoot)
$allowed=[IO.Path]::GetFullPath((Join-Path $root '.sdd/.test-work')).TrimEnd('/','\') + [IO.Path]::DirectorySeparatorChar
if (-not $fixture.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Live test fixture must stay under .sdd/.test-work.' }
if (-not $Executable) {
    $found=Find-Runtime $Runtime
    if (-not $found.available) { throw $found.reason }
    $Executable=$found.executable
}
$promptPath=Join-Path $fixture "$Runtime-prompt.txt"
if (-not (Test-Path $promptPath)) { throw "Missing reviewed test prompt: $promptPath" }
$prompt=Read-Utf8 $promptPath
$outputPath=Join-Path $fixture "$Runtime-result.txt"
if ($Runtime -eq 'codex') {
    $arguments=@('exec','--skip-git-repo-check','--ephemeral','--sandbox','workspace-write','--json','--output-last-message',$outputPath,'-C',$fixture,$prompt)
} else {
    $arguments=@('-p',$prompt,'--output-format','stream-json','--verbose','--permission-mode','default','--allowedTools','Skill,Read,Glob,Grep,Write,Edit,Bash(pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 *),Bash(pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 *)')
}
if ($Executable.EndsWith('.ps1')) {
    $arguments=@('-NoProfile','-File',$Executable) + $arguments
    $Executable=(Get-Process -Id $PID).Path
}
$result=Invoke-Captured $Executable $arguments $fixture $TimeoutSeconds
Write-Utf8 (Join-Path $fixture "$Runtime-events.txt") $result.stdout
Write-Utf8 (Join-Path $fixture "$Runtime-errors.txt") $result.stderr
$gates=@()
foreach ($mode in @('spec','status')) {
    $gate=Invoke-Captured (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $fixture '.sdd/scripts/check-sdd-gates.ps1'),$mode,'-Feature','sdd-portability-probe') $fixture
    $gates += @{ mode=$mode; result=$gate }
}
$specPath=Join-Path $fixture '.specs/sdd-portability-probe/spec.md'
$spec=if(Test-Path $specPath){Read-Utf8 $specPath}else{''}
if (-not $ExpectedState) { $ExpectedState=if($Runtime -eq 'claude'){'Confirmed'}else{'Draft'} }
$artifactStateMatches=$spec -match ('\*\*Status:\*\* ' + $ExpectedState)
$passed=$result.exitCode -eq 0 -and $artifactStateMatches -and @($gates | Where-Object { $_.result.exitCode -ne 0 }).Count -eq 0
Write-Json (Join-Path $root ".sdd/reports/$Runtime-live.json") @{ checkedUtc=[DateTime]::UtcNow.ToString('o'); runtime=$Runtime; processExitCode=$result.exitCode; artifactStateMatches=$artifactStateMatches; passed=$passed; gates=$gates; fixture=[IO.Path]::GetRelativePath($root,$fixture).Replace('\','/'); stdoutPath="$Runtime-events.txt"; stderrPath="$Runtime-errors.txt" }
Write-Output "$Runtime native process exit: $($result.exitCode); artifact verification passed: $passed. Results are in the isolated fixture."
if (-not $passed) { exit 1 }
