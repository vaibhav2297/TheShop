#Requires -Version 7
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$feature = 'manual-visual-' + [guid]::NewGuid().ToString('N')
$featureRoot = Join-Path $root ".specs/$feature"
$checks = 0

function Assert-Gate([string]$Mode, [int]$ExpectedExit) {
    $output = & pwsh -NoProfile -File "$PSScriptRoot/check-sdd-gates.ps1" $Mode -Feature $feature 2>&1
    if ($LASTEXITCODE -ne $ExpectedExit) { throw "Unexpected $Mode result: $output" }
    $script:checks++
}

try {
    New-Item -ItemType Directory -Path $featureRoot | Out-Null
    @'
## 6. Acceptance Criteria
**AC-1:** Editor saves valid content.
'@ | Set-Content -LiteralPath "$featureRoot/spec.md"
    $plan = @'
## 1. Objective
Manual visual acceptance.
## 2. Tech Stack
Existing stack.
## 3. Architecture
Existing layers.
## 4. Data Model
Unchanged.
## 5. Design Decisions
Figma: https://www.figma.com/design/fixture/Manual?node-id=1-2
## 6. Functional Flow
Open editor and save.
## 7. Development Plan
- [ ] **TASK-001** — Build editor.
## 8. Acceptance Criteria
| AC-1 | TASK-001 |
## 9. Validation
Existing rules.
## 10. Schema
Unchanged.
## 11. Open Questions
None.
**Status:** Resolved
**Spec:** spec.md
**Created:** 2026-11-01
'@
    $status = @'
**Last updated:** 2026-11-01
| Stage | State | Gate |
|---|---|---|
| 1. Spec | Confirmed | pass |
| 2. Plan | Resolved | pass |
| 3. Implement | Done | pass |
| 4. Test | Passing | pass |
| 5. Verify | Verified | pass |
'@
    Set-Content -LiteralPath "$featureRoot/plan.md" -Value $plan
    Set-Content -LiteralPath "$featureRoot/status.md" -Value $status

    # Figma remains design input without requiring scope, targets or captures.
    Assert-Gate plan 0
    Assert-Gate ship-ready 0

    # Legacy target tables with missing evidence must not reactivate visual gates.
    $legacy = $plan.Replace('## 8. Acceptance Criteria', @'
**Visual scope:** required
### Visual targets
| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| editor | /editor | 1440x900 | loaded | https://www.figma.com/design/fixture/Manual?node-id=1-2 |
## 8. Acceptance Criteria
'@)
    Set-Content -LiteralPath "$featureRoot/plan.md" -Value $legacy
    Assert-Gate plan 0
    Assert-Gate ship-ready 0

    # Ordinary non-Figma plans and existing failure checks retain their behavior.
    $plain = $plan.Replace('Figma: https://www.figma.com/design/fixture/Manual?node-id=1-2', 'Use existing components.')
    Set-Content -LiteralPath "$featureRoot/plan.md" -Value $plain
    Assert-Gate plan 0
    Assert-Gate ship-ready 0
    Set-Content -LiteralPath "$featureRoot/plan.md" -Value ($plan.Replace('| AC-1 | TASK-001 |', '| AC-1 | TASK-999 |'))
    Assert-Gate plan 1
    Set-Content -LiteralPath "$featureRoot/status.md" -Value ($status.Replace('| 5. Verify | Verified | pass |', '| 5. Verify | Pending | failure |'))
    Assert-Gate ship-ready 1
    Write-Output "Manual visual flow: $checks checks passed."
} finally {
    $resolved = [IO.Path]::GetFullPath($featureRoot)
    $expectedParent = [IO.Path]::GetFullPath((Join-Path $root '.specs'))
    if ([IO.Path]::GetDirectoryName($resolved) -eq $expectedParent -and
        [IO.Path]::GetFileName($resolved) -eq $feature -and
        $feature -match '^manual-visual-[a-f0-9]{32}$' -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
