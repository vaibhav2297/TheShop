[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-ParserClean {
    param([string]$Path)
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null
    Assert-True ($errors.Count -eq 0) "PowerShell syntax error in ${Path}: $($errors.Message -join '; ')"
}

$canonical = Join-Path $root '.sdd/theshop-build/SKILL.md'
$codex = Join-Path $root '.agents/skills/theshop-build/SKILL.md'
$claude = Join-Path $root '.claude/skills/theshop-build/SKILL.md'
$hook = Join-Path $root '.sdd/scripts/claude-design-hook.ps1'
$checker = Join-Path $root '.sdd/scripts/check-design-rules.ps1'
$retiredDirectory = '.sdd' + '-next'

foreach ($path in @($canonical, $codex, $claude, $hook, $checker, (Join-Path $root 'AGENTS.md'), (Join-Path $root 'CLAUDE.md'))) {
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Missing required SDD file: $path"
}

foreach ($path in @($codex, $claude)) {
    $content = Get-Content -LiteralPath $path -Raw
    Assert-True ($content -match '\.sdd/theshop-build/SKILL\.md') "Native entry does not load canonical skill: $path"
}

$claudeSettings = Get-Content -LiteralPath (Join-Path $root '.claude/settings.json') -Raw | ConvertFrom-Json
$commands = @($claudeSettings.hooks.PostToolUse | ForEach-Object { $_.hooks } | ForEach-Object { $_.command })
Assert-True (($commands -match '\.sdd/scripts/claude-design-hook\.ps1').Count -gt 0) 'Claude settings do not register SDD design hook.'
Assert-True (($commands -match ([regex]::Escape($retiredDirectory) + '/')).Count -eq 0) 'Claude settings still register retired SDD Next hook.'

Assert-ParserClean $hook
Assert-ParserClean $checker
Assert-ParserClean $PSCommandPath

$fixture = Join-Path $root 'src/TheShop.Web/.sdd-design-fixture.razor'
try {
    [System.IO.File]::WriteAllText($fixture, '<MudText Typo="Typo.body1">Valid</MudText>')
    & $checker -Path $fixture *> $null
    Assert-True ($LASTEXITCODE -eq 0) 'Design checker rejected valid fixture.'

    [System.IO.File]::WriteAllText($fixture, '<p>Invalid</p>')
    & $checker -Path $fixture *> $null
    Assert-True ($LASTEXITCODE -ne 0) 'Design checker accepted invalid fixture.'

    $input = @{ tool_input = @{ file_path = $fixture } } | ConvertTo-Json -Compress
    $input | & $hook 2>$null
    Assert-True ($LASTEXITCODE -eq 2) 'Claude hook did not propagate checker failure.'

    '{bad json' | & $hook
    Assert-True ($LASTEXITCODE -eq 0) 'Claude hook did not safely ignore malformed input.'
}
finally {
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Force }
}

$activePaths = @(
    (Join-Path $root 'AGENTS.md'),
    (Join-Path $root 'CLAUDE.md'),
    (Join-Path $root '.agents/skills/theshop-build'),
    (Join-Path $root '.agents/skills/graphify-windows'),
    (Join-Path $root '.claude'),
    (Join-Path $root '.codex'),
    (Join-Path $root '.github/workflows')
)
$existingActivePaths = @($activePaths | Where-Object { Test-Path -LiteralPath $_ })
$legacyPattern = [regex]::Escape($retiredDirectory) + '|theshop-(clarify|constitution|document|e2e|execute|implement|plan|resolve|review|ship|spec|start|test|test-merged|verify)|sdd-cavecrew-reviewer'
$legacyMatches = Get-ChildItem -LiteralPath $existingActivePaths -Recurse -File | Select-String -Pattern $legacyPattern -AllMatches
if ($legacyMatches) { throw "Active legacy reference remains: $($legacyMatches[0].Path):$($legacyMatches[0].LineNumber)" }

Assert-True (Test-Path -LiteralPath (Join-Path $root '.sdd') -PathType Container) 'Canonical .sdd directory is missing.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $root $retiredDirectory))) 'Retired SDD directory remains.'
Write-Output 'SDD integration checks passed.'
