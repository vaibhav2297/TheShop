[CmdletBinding()]
param(
    [string]$CodexSkills=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.agents/skills'),
    [string]$ClaudeSkills=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude/skills'),
    [string]$OutputPath
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$rows=@(foreach ($name in @('lean-build','investigate-first','surgical-patch','safe-refactor','migration','verify-and-stop')) {
    $codex=Join-Path $CodexSkills "$name/SKILL.md"
    $claude=Join-Path $ClaudeSkills "$name/SKILL.md"
    $available=(Test-Path -LiteralPath $codex -PathType Leaf) -and (Test-Path -LiteralPath $claude -PathType Leaf)
    $same=$available -and ((Get-FileHash -LiteralPath $codex).Hash -ceq (Get-FileHash -LiteralPath $claude).Hash)
    [ordered]@{ skill=$name; codex=$codex; claude=$claude; available=$available; identicalBytes=$same }
})
$report=[ordered]@{ schemaVersion=1; checkedUtc=[DateTime]::UtcNow.ToString('o'); checks=$rows; passed=@($rows | Where-Object { -not $_.available -or -not $_.identicalBytes }).Count -eq 0; limitation='Filesystem discovery only; does not prove native selection or agent registration.' }
if ($OutputPath) { Write-Json $OutputPath $report }
$report | ConvertTo-Json -Depth 8
if (-not $report.passed) { exit 1 }
