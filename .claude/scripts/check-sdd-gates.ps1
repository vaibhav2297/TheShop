# check-sdd-gates.ps1 - deterministic verification gates for the SDD pipeline
# artifacts. Companion to check-design-rules.ps1 (which lints *code*); this one
# validates the pipeline's *artifacts* and *process invariants* so each step can
# prove its output meets the contract instead of asserting it.
#
# Modes:
#   spec      -Feature x              spec.md template conformance (incl. Scope/Actors
#                                     sub-sections, Business Rules RULE ids, Given/When/
#                                     Then ACs), FR/AC id sequence, footer <-> appendix
#                                     consistency
#   plan      -Feature x              plan.md template conformance (incl. Section 7
#                                     TASK id sequence), AC coverage vs the spec with
#                                     AC -> TASK mapping, Section 11 <-> footer consistency
#   manifest  -Feature x              test-manifest.json: count arithmetic, AC ids vs
#                                     spec, listed files exist, feature trait stamped
#   compile   -Feature x              dotnet-builds every test project the manifest
#                                     lists (transitively building the layers they
#                                     reference); emits each compiler error tagged
#                                     [tests]/[src] by the failing file's location
#   e2e       -Feature x              e2e-manifest.json: every spec AC classified exactly
#                                     once as e2e|unit|manual, e2e evidence resolves to a
#                                     real AC{n}_ method in a listed journey, unit evidence
#                                     resolves through test-manifest.json, manual carries a
#                                     reason; plus the data-testid resolution check - fatal
#                                     for this feature's journeys/pages and the shared
#                                     harness, a warning for other features' files
#   scope     -Phase p -Files f,...   newly changed files confined to the layer the
#                                     sub-agent owns (domain|application|infra|web|infra+web)
#   snapshot  -Snapshot dir           save every currently-changed file aside (baseline
#                                     for doc-only); always exits 0
#   doc-only  -Snapshot dir           diff vs the snapshot contains ONLY XML doc-comment
#                                     ("///") line changes in .cs files
#   status    -Feature x              status.md rows agree with the spec/plan footers
#                                     (three-location state drift detector)
#   ship-ready -Feature x             every status.md ledger row is in a ship-ready
#                                     terminal state, with no recorded gate failure
#                                     or carried waiver (the pre-ship readiness scan)
#
# Output: violations -> stdout, exit 1. Clean -> one line, exit 0.
# Used by the /theshop.* commands as entry/exit gates; can also be run manually.

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('spec', 'plan', 'manifest', 'compile', 'e2e', 'scope', 'snapshot', 'doc-only', 'status', 'ship-ready')]
    [string]$Mode,

    [string]$Feature,

    [ValidateSet('domain', 'application', 'infra', 'web', 'infra+web')]
    [string]$Phase,

    [string[]]$Files,

    [string]$Snapshot
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Emoji built from code points so matching never depends on this file's encoding.
$PIN  = [char]::ConvertFromUtf32(0x1F4CC)   # pushpin   (assumption)
$QQ   = [string][char]0x2753                # question  (open question)
$WARN = [string][char]0x26A0                # warning   (risk)

$violations = [System.Collections.Generic.List[string]]::new()
function Fail([string]$Msg) { $script:violations.Add($Msg) }

# Findings that are real but belong to a different feature than the one under test.
# Reported, never fatal - one feature's pending work must not block another's gate.
$warnings = [System.Collections.Generic.List[string]]::new()
function Warn([string]$Msg) { $script:warnings.Add($Msg) }

function Write-Warnings {
    if ($script:warnings.Count -eq 0) { return }
    Write-Output "[sdd-gates] $($script:warnings.Count) warning(s) - outside this feature's scope, not blocking:"
    foreach ($w in $script:warnings) { Write-Output "  ! $w" }
}

function Complete-Run([string]$Label) {
    Write-Warnings
    if ($script:violations.Count -gt 0) {
        Write-Output "[sdd-gates] $Label - $($script:violations.Count) violation(s):"
        foreach ($v in $script:violations) { Write-Output "  - $v" }
        Write-Output 'A failed gate means the producing step did not meet the pipeline contract. Fix the artifact (or re-run the producing step), then re-run this gate. Do not report the step as done while this gate fails.'
        exit 1
    }
    Write-Output "[sdd-gates] $Label - clean."
    exit 0
}

function Read-Doc([string]$RelPath) {
    $p = Join-Path $repoRoot $RelPath
    if (-not (Test-Path -LiteralPath $p)) { return $null }
    Get-Content -Raw -LiteralPath $p
}

function Get-NumberedSection([string]$Content, [int]$Number) {
    $m = [regex]::Match($Content, "(?ms)^## $Number\. [^\r\n]*\r?\n(.*?)(?=^## |\z)")
    if ($m.Success) { $m.Groups[1].Value } else { $null }
}

function Get-HeadingTitle([string]$Content, [int]$Number) {
    $m = [regex]::Match($Content, "(?m)^## $Number\. (.+?)\s*$")
    if ($m.Success) { $m.Groups[1].Value } else { $null }
}

function Get-SpecAcIds([string]$SpecContent) {
    $s6 = Get-NumberedSection $SpecContent 6
    if (-not $s6) { return @() }
    @([regex]::Matches($s6, '\*\*AC-(\d+):\*\*') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
}

function Test-IdSequence([int[]]$Ids, [string]$Prefix, [string]$Where) {
    if ($Ids.Count -eq 0) { Fail "$Where contains no **$Prefix-n:** items"; return }
    $expected = @(1..$Ids.Count)
    if (Compare-Object $Ids $expected) {
        Fail "$Where $Prefix ids are not sequential 1..$($Ids.Count) (found: $($Ids -join ', '))"
    }
}

function Get-ChangedPaths {
    $out = git -C $repoRoot status --porcelain=v1 --untracked-files=all
    foreach ($line in @($out)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $p = $line.Substring(3)
        if ($p -match '\s->\s') { $p = ($p -split '\s->\s')[-1] }
        $p.Trim().Trim('"')
    }
}

# ---------------------------------------------------------------- spec gate --
function Test-SpecGate([string]$F) {
    $c = Read-Doc ".specs/$F/spec.md"
    if (-not $c) { Fail ".specs/$F/spec.md not found"; return }

    $expected = @('Problem Statement', 'Functional Requirements', 'Functional Behaviors',
                  'Constraints', 'Edge Cases & Error Handling', 'Acceptance Criteria')
    for ($i = 1; $i -le 6; $i++) {
        $t = Get-HeadingTitle $c $i
        if (-not $t) { Fail "missing numbered section '## $i. $($expected[$i-1])'" }
        elseif ($t -ne $expected[$i - 1]) { Fail "section $i title is '$t' - template requires '$($expected[$i-1])'" }
    }
    $extra = @([regex]::Matches($c, '(?m)^## (\d+)\.') | ForEach-Object { [int]$_.Groups[1].Value } | Where-Object { $_ -gt 6 })
    if ($extra.Count -gt 0) { Fail "unexpected numbered section(s) beyond 6: $($extra -join ', ') - the spec template has exactly six" }

    $s1 = Get-NumberedSection $c 1
    if ($s1) {
        if ($s1 -notmatch '\*\*Solution \(one line\):\*\*') { Fail "Section 1 missing '**Solution (one line):**'" }
        if ($s1 -notmatch '(?m)^### Scope\s*$') { Fail "Section 1 missing the '### Scope' sub-section" }
        if ($s1 -notmatch '\*\*In scope:\*\*')  { Fail "Section 1 missing '**In scope:**' block" }
        if ($s1 -notmatch '\*\*Out of scope:\*\*') { Fail "Section 1 missing '**Out of scope:**' block" }
        if ($s1 -notmatch '(?m)^### Actors & Access\s*$') { Fail "Section 1 missing the '### Actors & Access' sub-section" }
    }

    $s2 = Get-NumberedSection $c 2
    if ($s2) {
        $frIds = @([regex]::Matches($s2, '\*\*FR-(\d+):\*\*') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        Test-IdSequence $frIds 'FR' 'Section 2'
    }

    $s3 = Get-NumberedSection $c 3
    if ($s3 -and $s3 -notmatch '(?m)^### Behavior') { Fail "Section 3 has no '### Behavior n:' subsections" }

    $s4 = Get-NumberedSection $c 4
    if ($s4) {
        if ($s4 -notmatch '(?m)^### Business Rules\s*$') { Fail "Section 4 missing the '### Business Rules' sub-section" }
        $ruleIds = @([regex]::Matches($s4, '\*\*RULE-(\d+)\*\*') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        if ($ruleIds.Count -gt 0) { Test-IdSequence $ruleIds 'RULE' 'Section 4' }
    }

    $s5 = Get-NumberedSection $c 5
    if ($s5 -and $s5 -notmatch '\*\*Edge case:\*\*') { Fail "Section 5 has no '**Edge case:** ... -> **User experience:** ...' items" }

    $s6 = Get-NumberedSection $c 6
    if ($s6) {
        $acIds = Get-SpecAcIds $c
        Test-IdSequence $acIds 'AC' 'Section 6'
        foreach ($m in [regex]::Matches($s6, '(?m)^.*\*\*AC-\d+:\*\*.*$')) {
            if ($m.Value -notmatch '(?i)\bgiven\b.*\bwhen\b.*\bthen\b') {
                $snip = $m.Value.Trim(); if ($snip.Length -gt 80) { $snip = $snip.Substring(0, 80) + '...' }
                Fail "AC not phrased 'Given ..., when ..., then ...': $snip"
            }
        }
    }

    # Footer
    $specStatus = $null; $declaredN = $null
    $st = [regex]::Match($c, '(?m)^\*\*Status:\*\*\s*(.+)$')
    if (-not $st.Success) { Fail "missing '**Status:**' footer line" }
    else {
        $val = $st.Groups[1].Value.Trim()
        if ($val -match '^Confirmed\b') { $specStatus = 'Confirmed' }
        elseif ($val -match '^Draft\s*[' + [char]0x2014 + [char]0x2013 + '-]+\s*(\d+)\s+open assumption') {
            $specStatus = 'Draft'; $declaredN = [int]$Matches[1]
        }
        else { Fail "footer Status is '$val' - must be 'Draft $([char]0x2014) N open assumption(s)' or 'Confirmed'" }
    }
    if ($c -notmatch '\*\*Created:\*\*\s*\d{4}-\d{2}-\d{2}') { Fail "footer missing '**Created:** YYYY-MM-DD'" }

    # Appendix <-> footer consistency
    $app = [regex]::Match($c, '(?ms)^## Assumptions & Open Questions\s*\r?\n(.*?)(?=^---|\z)')
    if (-not $app.Success) { Fail "missing '## Assumptions & Open Questions' appendix" }
    else {
        $openCount = ([regex]::Matches($app.Groups[1].Value, "(?m)^\s*-\s*\*\*\s*(?:$PIN|$QQ)")).Count
        if ($specStatus -eq 'Confirmed' -and $openCount -gt 0) { Fail "Status is Confirmed but the appendix still lists $openCount open item(s)" }
        if ($specStatus -eq 'Draft' -and $null -ne $declaredN -and $declaredN -ne $openCount) { Fail "footer declares $declaredN open assumption(s) but the appendix lists $openCount" }
    }
}

# ---------------------------------------------------------------- plan gate --
function Test-PlanGate([string]$F) {
    $c = Read-Doc ".specs/$F/plan.md"
    if (-not $c) { Fail ".specs/$F/plan.md not found"; return }

    $keywords = @('Objective', 'Tech Stack', 'Architecture', 'Data Model', 'Design Decisions',
                  'Functional Flow', 'Development Plan', 'Acceptance Criteria', 'Validation',
                  'Schema', 'Open Questions')
    for ($i = 1; $i -le 11; $i++) {
        $t = Get-HeadingTitle $c $i
        if (-not $t) { Fail "missing numbered section '## $i.' (expected a '$($keywords[$i-1])' section)" }
        elseif ($t -notlike "*$($keywords[$i-1])*") { Fail "section $i title '$t' does not match the template's '$($keywords[$i-1])' section" }
    }

    # Section 7: agent execution plan - TASK ids defined as checklist items must be
    # unique and sequential from 001 (continuous across steps, never reset per layer).
    $s7 = Get-NumberedSection $c 7
    $definedTasks = @()
    if ($s7) {
        $taskIds = @([regex]::Matches($s7, '\*\*TASK-(\d{3})\*\*') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        Test-IdSequence $taskIds 'TASK' 'Section 7'
        $definedTasks = @($taskIds | ForEach-Object { 'TASK-{0:d3}' -f $_ })
    }

    # AC coverage: every AC in the spec must be mapped in Section 8, to at least one
    # TASK id, and every TASK id Section 8 references must be defined in Section 7.
    $spec = Read-Doc ".specs/$F/spec.md"
    if (-not $spec) { Fail "companion spec .specs/$F/spec.md not found - cannot verify AC coverage" }
    else {
        $acIds = Get-SpecAcIds $spec
        $s8 = Get-NumberedSection $c 8
        if ($acIds.Count -eq 0) { Fail 'spec Section 6 has no **AC-n:** items to map' }
        elseif ($s8) {
            foreach ($id in $acIds) {
                $rows = @(($s8 -split "`n") | Where-Object { $_ -match "\bAC-$id\b" })
                if ($rows.Count -eq 0) { Fail "AC-$id from the spec is not mapped in plan Section 8" }
                elseif (-not @($rows | Where-Object { $_ -match 'TASK-\d{3}' })) { Fail "AC-$id is mapped in plan Section 8 but names no TASK id" }
            }
            foreach ($m in [regex]::Matches($s8, '\bTASK-(\d{3})\b')) {
                $tid = "TASK-$($m.Groups[1].Value)"
                if ($definedTasks -notcontains $tid) { Fail "plan Section 8 references $tid, which is not defined in Section 7" }
            }
        }
    }

    # Footer
    $planStatus = $null
    $st = [regex]::Match($c, '(?m)^\*\*Status:\*\*\s*(Draft|Resolved)\b')
    if (-not $st.Success) { Fail "footer must start '**Status:** Draft' or '**Status:** Resolved'" }
    else { $planStatus = $st.Groups[1].Value }
    if ($c -notmatch '\*\*Spec:\*\*') { Fail "footer missing the '**Spec:**' back-reference" }
    if ($c -notmatch '\*\*Created:\*\*\s*\d{4}-\d{2}-\d{2}') { Fail "footer missing '**Created:** YYYY-MM-DD'" }

    # Resolved <-> Section 11 consistency
    $s11 = Get-NumberedSection $c 11
    if ($planStatus -eq 'Resolved' -and $s11) {
        if ($s11 -match $QQ)  { Fail "Status is Resolved but Section 11 still contains an open question ($QQ)" }
        if ($s11 -match $PIN) { Fail "Status is Resolved but Section 11 still contains an unratified assumption ($PIN)" }
        foreach ($line in ($s11 -split "`n")) {
            if ($line -match $WARN -and $line -notmatch 'Accepted') {
                Fail "Status is Resolved but a Section 11 risk has no disposition (mitigate-and-remove, or mark Accepted): $($line.Trim())"
            }
        }
    }
}

# ------------------------------------------------------------ manifest gate --
function Test-ManifestGate([string]$F) {
    $raw = Read-Doc ".specs/$F/test-manifest.json"
    if (-not $raw) { Fail ".specs/$F/test-manifest.json not found"; return }
    try { $m = $raw | ConvertFrom-Json } catch { Fail "test-manifest.json is not valid JSON: $($_.Exception.Message)"; return }

    if ($m.feature -ne $F) { Fail "manifest 'feature' is '$($m.feature)' - expected '$F'" }
    if ($m.trait -ne $F)   { Fail "manifest 'trait' is '$($m.trait)' - must equal the feature name (the runner filters on it)" }

    if (-not $m.classes -or @($m.classes).Count -eq 0) { Fail "manifest lists no test classes" }
    else {
        $sum = (@($m.classes) | Measure-Object -Property tests -Sum).Sum
        if ($sum -ne $m.totalTests) { Fail "totalTests is $($m.totalTests) but the per-class counts sum to $sum" }

        $traitRx = '\[Trait\(\s*"Feature"\s*,\s*"' + [regex]::Escape($F) + '"\s*\)\]'
        foreach ($cls in @($m.classes)) {
            $fp = Join-Path $repoRoot $cls.file
            if (-not (Test-Path -LiteralPath $fp)) { Fail "listed test file missing on disk: $($cls.file)"; continue }
            $fc = Get-Content -Raw -LiteralPath $fp
            if ($fc -notmatch $traitRx) { Fail "$($cls.file) has no [Trait(""Feature"", ""$F"")] stamp - the runner's trait filter will not find its tests" }
        }
    }

    if (-not $m.acceptanceCriteria) { Fail "manifest has no 'acceptanceCriteria' array - the runner loses its definition-of-done oracle" }
    else {
        $spec = Read-Doc ".specs/$F/spec.md"
        if (-not $spec) { Fail "spec.md not found - cannot cross-check acceptanceCriteria ids" }
        else {
            $specIds = @(Get-SpecAcIds $spec | ForEach-Object { "AC-$_" })
            $mfIds   = @($m.acceptanceCriteria | ForEach-Object { $_.id })
            foreach ($id in $specIds) { if ($id -notin $mfIds) { Fail "spec $id is missing from the manifest's acceptanceCriteria (record it with tests: [] if uncovered)" } }
            foreach ($id in $mfIds)   { if ($id -notin $specIds) { Fail "manifest lists $id which does not exist in the spec" } }
        }
        $fqns = @(@($m.classes) | ForEach-Object { $_.fqn })
        foreach ($ac in @($m.acceptanceCriteria)) {
            foreach ($t in @($ac.tests)) {
                $owned = $false
                foreach ($fq in $fqns) { if ($t.StartsWith("$fq.")) { $owned = $true; break } }
                if (-not $owned) { Fail "$($ac.id) maps to test '$t' which is not under any class fqn listed in the manifest" }
            }
        }
    }
}

# -------------------------------------------------------------- compile gate --
# Builds (never runs) the test projects the manifest lists. Each compiler error
# is tagged by the failing file's location so the caller can route it: [tests]
# errors are the test writer's to fix; [src] errors mean production code is
# broken and no amount of test editing will help.
function Test-CompileGate([string]$F) {
    $raw = Read-Doc ".specs/$F/test-manifest.json"
    if (-not $raw) { Fail ".specs/$F/test-manifest.json not found - run the manifest gate first"; return }
    try { $m = $raw | ConvertFrom-Json } catch { Fail "test-manifest.json is not valid JSON: $($_.Exception.Message)"; return }

    $projects = @(@($m.classes) | ForEach-Object {
        $norm = ("$($_.file)" -replace '\\', '/')
        if ($norm -match '^(tests/[^/]+)/') { $Matches[1] }
    } | Sort-Object -Unique)
    if ($projects.Count -eq 0) { Fail 'manifest lists no files under tests/ - nothing to compile'; return }

    $errRx = '^\s*(?<file>.+?)\((?<line>\d+),\d+\):\s*error\s+(?<code>\w+):\s*(?<msg>.*?)(\s*\[[^\[\]]*\])?\s*$'
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($proj in $projects) {
        $projPath = Join-Path $repoRoot $proj
        if (-not (Test-Path -LiteralPath $projPath)) { Fail "test project directory missing on disk: $proj"; continue }
        $out = & dotnet build $projPath --nologo 2>&1 | ForEach-Object { "$_" }
        if ($LASTEXITCODE -eq 0) { continue }

        $matched = $false
        foreach ($line in $out) {
            $em = [regex]::Match($line, $errRx)
            if (-not $em.Success) { continue }
            $matched = $true
            $rel = $em.Groups['file'].Value
            if ($rel.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                $rel = $rel.Substring($repoRoot.Length)
            }
            $rel = ($rel -replace '\\', '/').TrimStart('/')
            if (-not $seen.Add("$rel($($em.Groups['line'].Value)):$($em.Groups['code'].Value)")) { continue }
            $tag = if ($rel -like 'tests/*') { 'tests' } elseif ($rel -like 'src/*') { 'src' } else { 'other' }
            Fail "[$tag] $rel($($em.Groups['line'].Value)) - error $($em.Groups['code'].Value): $($em.Groups['msg'].Value)"
        }
        if (-not $matched) { Fail "dotnet build $proj failed (exit $LASTEXITCODE) with no parseable compiler errors - run it manually for detail" }
    }
}

# ------------------------------------------------------------------ e2e gate --
# The E2E counterpart of the manifest gate, and the pipeline's defence against a
# thin browser suite. The unit manifest forces every AC to be *listed*; this one
# forces every AC to be *classified* - proven in the browser, proven below it, or
# consciously handed to a human with a written reason. A journey that quietly
# covers 11 of 32 ACs stops being indistinguishable from one that covers them all.
function Test-E2eGate([string]$F) {
    $raw = Read-Doc ".specs/$F/e2e-manifest.json"
    if (-not $raw) { Fail ".specs/$F/e2e-manifest.json not found - /theshop.e2e writes it before any journey runs"; return }
    try { $m = $raw | ConvertFrom-Json } catch { Fail "e2e-manifest.json is not valid JSON: $($_.Exception.Message)"; return }

    if ($m.feature -ne $F) { Fail "e2e-manifest 'feature' is '$($m.feature)' - expected '$F'" }
    if ($m.trait -ne $F)   { Fail "e2e-manifest 'trait' is '$($m.trait)' - must equal the feature name (the runner filters on it)" }

    # -- journeys: on disk, stamped with both traits, and honest about their test count.
    $methodRx   = '(?m)^\s*public\s+(?:async\s+)?Task\s+(AC(\d+)_\w+)\s*\('
    $catTraitRx = '\[Trait\(\s*"Category"\s*,\s*"E2E"\s*\)\]'
    $ftTraitRx  = '\[Trait\(\s*"Feature"\s*,\s*"' + [regex]::Escape($F) + '"\s*\)\]'

    $journeyFqns    = @(@($m.journeys) | ForEach-Object { $_.fqn })
    $journeyMethods = @{}   # "Fqn.Method" -> claimed by an AC entry yet?

    foreach ($j in @($m.journeys)) {
        $fp = Join-Path $repoRoot $j.file
        if (-not (Test-Path -LiteralPath $fp)) { Fail "listed journey file missing on disk: $($j.file)"; continue }
        $fc = Get-Content -Raw -LiteralPath $fp
        if ($fc -notmatch $catTraitRx) { Fail "$($j.file) has no [Trait(""Category"", ""E2E"")] stamp - /theshop.e2e's filter will not find its tests" }
        if ($fc -notmatch $ftTraitRx)  { Fail "$($j.file) has no [Trait(""Feature"", ""$F"")] stamp - /theshop.e2e's filter will not find its tests" }

        $found = @([regex]::Matches($fc, $methodRx) | ForEach-Object { $_.Groups[1].Value })
        if ($j.tests -ne $found.Count) {
            Fail "$($j.file) declares tests: $($j.tests) but defines $($found.Count) AC-prefixed test method(s)"
        }
        foreach ($name in $found) { $journeyMethods["$($j.fqn).$name"] = $false }
    }

    # -- classification: every spec AC, exactly once, in exactly one bucket.
    if (-not $m.acceptanceCriteria) { Fail "e2e-manifest has no 'acceptanceCriteria' array - nothing classifies the spec's ACs"; return }

    $spec = Read-Doc ".specs/$F/spec.md"
    if (-not $spec) { Fail "spec.md not found - cannot cross-check acceptanceCriteria ids" }
    else {
        $specIds = @(Get-SpecAcIds $spec | ForEach-Object { "AC-$_" })
        $mfIds   = @($m.acceptanceCriteria | ForEach-Object { $_.id })
        foreach ($id in $specIds) { if ($id -notin $mfIds) { Fail "spec $id is not classified in the e2e-manifest (every AC needs coverage e2e|unit|manual)" } }
        foreach ($id in $mfIds)   { if ($id -notin $specIds) { Fail "e2e-manifest classifies $id which does not exist in the spec" } }
        foreach ($g in @($mfIds | Group-Object | Where-Object Count -gt 1)) { Fail "$($g.Name) is classified $($g.Count) times - each AC belongs in exactly one bucket" }
    }

    # Unit-covered ACs are only credible if the unit manifest really maps that AC to that test.
    $unitMap = @{}
    $unitRaw = Read-Doc ".specs/$F/test-manifest.json"
    if ($unitRaw) {
        try {
            $um = $unitRaw | ConvertFrom-Json
            foreach ($ac in @($um.acceptanceCriteria)) { $unitMap[$ac.id] = @($ac.tests) }
        } catch { Fail "test-manifest.json is not valid JSON - cannot corroborate unit-covered ACs" }
    }

    foreach ($ac in @($m.acceptanceCriteria)) {
        switch -Regex ("$($ac.coverage)") {
            '^e2e$' {
                if (-not $ac.evidence) { Fail "$($ac.id) is coverage 'e2e' with no evidence - name the journey test that proves it"; break }
                $owner = @($journeyFqns | Where-Object { "$($ac.evidence)".StartsWith("$_.") }) | Select-Object -First 1
                if (-not $owner) { Fail "$($ac.id) maps to '$($ac.evidence)' which is not under any journey fqn listed in the e2e-manifest"; break }
                if (-not $journeyMethods.ContainsKey("$($ac.evidence)")) { Fail "$($ac.id) maps to '$($ac.evidence)' but no such AC-prefixed test method exists in $owner"; break }
                $journeyMethods["$($ac.evidence)"] = $true
                $num = "$($ac.id)".Substring(3)
                $method = "$($ac.evidence)".Substring($owner.Length + 1)
                if ($method -notmatch "^AC$num`_") { Fail "$($ac.id) maps to '$method' - an E2E test must be named AC$num`_ so its result maps back to this AC unambiguously" }
            }
            '^unit$' {
                if (-not $ac.evidence) { Fail "$($ac.id) is coverage 'unit' with no evidence - name the test that covers it below the browser"; break }
                if (-not $unitRaw) { Fail "$($ac.id) is coverage 'unit' but .specs/$F/test-manifest.json does not exist to corroborate it"; break }
                if ("$($ac.evidence)" -notin @($unitMap["$($ac.id)"])) {
                    Fail "$($ac.id) claims unit coverage by '$($ac.evidence)' but test-manifest.json does not map $($ac.id) to that test"
                }
            }
            '^manual$' {
                $reason = "$($ac.reason)".Trim()
                if ($reason.Length -lt 15) { Fail "$($ac.id) is coverage 'manual' with no substantive reason - state why it cannot be machine-proven" }
            }
            default { Fail "$($ac.id) has coverage '$($ac.coverage)' - must be one of e2e, unit, manual" }
        }
    }

    # -- no orphan journey tests: a written AC{n}_ test that no AC claims is a stale
    #    or misnamed test whose result would silently map to nothing.
    foreach ($k in $journeyMethods.Keys) {
        if (-not $journeyMethods[$k]) { Fail "$k is an AC-prefixed journey test that no acceptanceCriteria entry claims - map it or rename it" }
    }

    # -- data-testid resolution: every literal hook a journey reaches for must exist in the
    #    Web layer. This is the single largest cause of E2E failure, and it is knowable
    #    without starting a browser.
    #
    #    The scan is repo-wide, but the VERDICT is feature-scoped. /theshop.e2e deliberately
    #    leaves a journey on disk that reaches for a hook src/ does not define yet, then halts
    #    so the Web layer adds it. A repo-wide failure would let that one parked journey block
    #    every other feature's gate. So: this feature's own files fail, everyone else's warn.
    $e2eRoot = Join-Path $repoRoot 'tests/TheShop.E2E.Tests'
    if (Test-Path -LiteralPath $e2eRoot) {
        # NB: loop variables here must not be named $f - PowerShell variable names are
        # case-insensitive, so $f would clobber this function's $F feature parameter.
        $defined = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($webFile in @(Get-ChildItem -Path (Join-Path $repoRoot 'src/TheShop.Web') -Recurse -Include *.razor, *.cs -File -ErrorAction SilentlyContinue)) {
            $c = Get-Content -Raw -LiteralPath $webFile.FullName
            if (-not $c) { continue }   # -Raw yields $null for an empty file
            # Plain HTML attribute and MudBlazor UserAttributes dictionary form alike.
            foreach ($mm in [regex]::Matches($c, 'data-testid"?\]?\s*=\s*"([^"]+)"')) { [void]$defined.Add($mm.Groups[1].Value) }
        }

        # Only per-feature test code can be warned about. Everything else under the E2E
        # project - Auth/, Fixtures/, the shared sign-in flow - is harness that /theshop.e2e
        # is forbidden to edit and that every feature depends on, so it always fails hard.
        #
        # Out of scope = a Journeys/ file this manifest does not list, or a Pages/ file none
        # of those journeys names. Page objects hold most locators, so a page object this
        # feature actually drives has to be in scope or the check would prove nothing.
        $ownJourneys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $journeyText = ''
        foreach ($j in @($m.journeys)) {
            $jp = Join-Path $repoRoot $j.file
            if (-not (Test-Path -LiteralPath $jp)) { continue }
            [void]$ownJourneys.Add((Resolve-Path -LiteralPath $jp).Path)
            $journeyText += (Get-Content -Raw -LiteralPath $jp)
        }

        foreach ($e2eFile in @(Get-ChildItem -Path $e2eRoot -Recurse -Include *.cs -File -ErrorAction SilentlyContinue |
                               Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
            $c = Get-Content -Raw -LiteralPath $e2eFile.FullName
            if (-not $c) { continue }

            $mine = $true
            if ($e2eFile.FullName -match '[\\/]Journeys[\\/]') {
                $mine = $ownJourneys.Contains($e2eFile.FullName)
            } elseif ($e2eFile.FullName -match '[\\/]Pages[\\/]') {
                # Class name == file name by convention here; a journey that mentions the
                # identifier is the journey that drives that page.
                $mine = [bool]($journeyText -match ('\b' + [regex]::Escape($e2eFile.BaseName) + '\b'))
            }
            foreach ($mm in [regex]::Matches($c, 'GetByTestId\(\s*"([^"]+)"\s*\)')) {
                $id = $mm.Groups[1].Value
                if (-not $defined.Contains($id)) {
                    $rel = $e2eFile.FullName.Substring($repoRoot.Length).TrimStart('\', '/') -replace '\\', '/'
                    if ($mine) {
                        Fail "$rel reaches for data-testid '$id' which no file under src/TheShop.Web defines - the locator will not resolve at run time"
                    } else {
                        Warn "$rel reaches for data-testid '$id' which no file under src/TheShop.Web defines - not this feature's journey, but it will fail when its own gate runs"
                    }
                }
            }
        }
    }
}

# --------------------------------------------------------------- scope gate --
function Test-ScopeGate {
    if (-not $Phase) { throw 'scope mode requires -Phase' }
    $allowed = @{
        'domain'      = @('src/TheShop.Domain/')
        'application' = @('src/TheShop.Application/',
                          'src/TheShop.Web/Resources/Strings.resx',
                          'src/TheShop.Web/Resources/Strings.fr.resx')
        'infra'       = @('src/TheShop.Infrastructure/')
        'web'         = @('src/TheShop.Web/')
        'infra+web'   = @('src/TheShop.Infrastructure/', 'src/TheShop.Web/')
    }[$Phase]

    foreach ($f in @($Files)) {
        if ([string]::IsNullOrWhiteSpace($f)) { continue }
        $norm = ($f -replace '\\', '/').Trim()
        $ok = $false
        foreach ($a in $allowed) { if ($norm -eq $a -or $norm.StartsWith($a)) { $ok = $true; break } }
        if (-not $ok) { Fail "'$norm' is outside the $Phase agent's allowed scope (allowed: $($allowed -join ', '))" }
    }
}

# ------------------------------------------------------- snapshot / doc-only --
function Invoke-SnapshotMode {
    if (-not $Snapshot) { throw 'snapshot mode requires -Snapshot <dir>' }
    New-Item -ItemType Directory -Force -Path $Snapshot | Out-Null
    $paths = @(Get-ChangedPaths)
    $saved = 0
    foreach ($p in $paths) {
        $src = Join-Path $repoRoot $p
        if (-not (Test-Path -LiteralPath $src -PathType Leaf)) { continue }
        $dst = Join-Path $Snapshot $p
        New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
        $saved++
    }
    Set-Content -Path (Join-Path $Snapshot '_files.txt') -Value ($paths -join "`n")
    Write-Output "[sdd-gates] snapshot - $saved changed file(s) saved to $Snapshot"
    exit 0
}

function Test-DocOnlyGate {
    if (-not $Snapshot -or -not (Test-Path -LiteralPath $Snapshot)) {
        throw 'doc-only mode requires -Snapshot <dir> (created with snapshot mode BEFORE the documenter ran)'
    }
    foreach ($p in @(Get-ChangedPaths)) {
        $abs  = Join-Path $repoRoot $p
        $base = Join-Path $Snapshot $p

        if (-not (Test-Path -LiteralPath $abs -PathType Leaf)) {
            if (Test-Path -LiteralPath $base) { Fail "$p - file deleted; the documenter must not delete files" }
            continue
        }

        $diffLines = $null
        if (Test-Path -LiteralPath $base) {
            # Changed before the documenter too: only the delta vs the snapshot is the documenter's.
            $diffLines = git diff --no-index --unified=0 -- $base $abs 2>$null
        }
        else {
            # Clean before the documenter ran: its whole working-tree diff is the documenter's doing.
            git -C $repoRoot ls-files --error-unmatch -- $p *> $null
            if ($LASTEXITCODE -ne 0) { Fail "$p - new file created during the documentation pass; the documenter must not create files"; continue }
            $diffLines = git -C $repoRoot diff --unified=0 HEAD -- $p 2>$null
        }

        if (-not $diffLines) { continue }
        if ($p -notmatch '\.cs$') { Fail "$p - non-C# file changed during the documentation pass"; continue }

        $bad = 0
        foreach ($line in @($diffLines)) {
            if ($line -match '^(diff |index |@@ |\+\+\+ |--- |new file|deleted file|similarity)') { continue }
            if ($line -match '^[+-]') {
                $body = $line.Substring(1).Trim()
                if ($body -ne '' -and -not $body.StartsWith('///')) {
                    $bad++
                    if ($bad -le 3) { Fail "$p - non-doc-comment change: $($line.Trim())" }
                }
            }
        }
        if ($bad -gt 3) { Fail "$p - ...and $($bad - 3) more non-doc-comment changed line(s)" }
    }
}

# -------------------------------------------------------------- status gate --
function Test-StatusGate([string]$F) {
    $doc = Read-Doc ".specs/$F/status.md"
    if (-not $doc) { Fail ".specs/$F/status.md not found"; return }
    if ($doc -notmatch '\*\*Last updated:\*\*') { Fail "status.md missing '**Last updated:**' line" }

    function Get-RowState([string]$D, [string]$RowRx) {
        $m = [regex]::Match($D, "(?m)^\|\s*$RowRx\s*\|\s*([^|]+)\|")
        if ($m.Success) { $m.Groups[1].Value.Trim() } else { $null }
    }

    $specRow = Get-RowState $doc '1\.\s*Spec'
    $planRow = Get-RowState $doc '2\.\s*Plan'
    if (-not $specRow) { Fail "status.md has no '| 1. Spec |' row" }
    if (-not $planRow) { Fail "status.md has no '| 2. Plan |' row" }

    $spec = Read-Doc ".specs/$F/spec.md"
    if ($spec -and $specRow) {
        $m = [regex]::Match($spec, '(?m)^\*\*Status:\*\*\s*(Confirmed|Draft)\b')
        if ($m.Success -and $specRow -notmatch [regex]::Escape($m.Groups[1].Value)) {
            Fail "spec footer says '$($m.Groups[1].Value)' but status.md Spec row says '$specRow'"
        }
    }
    $plan = Read-Doc ".specs/$F/plan.md"
    if ($plan -and $planRow) {
        $m = [regex]::Match($plan, '(?m)^\*\*Status:\*\*\s*(Resolved|Draft)\b')
        if ($m.Success -and $planRow -notmatch [regex]::Escape($m.Groups[1].Value)) {
            Fail "plan footer says '$($m.Groups[1].Value)' but status.md Plan row says '$planRow'"
        }
    }
}

# --------------------------------------------------------- ship-ready gate --
# Scans the feature's status.md ledger and reports every stage that is NOT in a
# ship-ready terminal state, plus any recorded gate failure (red circle) or carried
# waiver. /theshop.ship runs this as a warn-gate before committing: exit 0 means the
# whole pipeline is green and safe to land on dev; exit 1 lists what is still open so
# the command can surface it and let the user ship anyway with a recorded waiver.
function Test-ShipReadyGate([string]$F) {
    $doc = Read-Doc ".specs/$F/status.md"
    if (-not $doc) { Fail ".specs/$F/status.md not found - the feature has no SDD ledger to verify"; return }
    if ($doc -notmatch '\*\*Last updated:\*\*') { Fail "status.md missing '**Last updated:**' line" }

    $RED = [char]::ConvertFromUtf32(0x1F534)   # large red circle - the gate-fail marker

    # Each ledger row, paired with the State value(s) that count as ship-ready.
    $stages = @(
        @{ Rx = '1\.\s*Spec';      Name = '1. Spec';      Ok = @('Confirmed') }
        @{ Rx = '2\.\s*Plan';      Name = '2. Plan';      Ok = @('Resolved') }
        @{ Rx = '3\.\s*Implement'; Name = '3. Implement'; Ok = @('Done') }
        @{ Rx = '4\.\s*Test';      Name = '4. Test';      Ok = @('Passing') }
        @{ Rx = '5\.\s*Verify';    Name = '5. Verify';    Ok = @('Verified', 'Skipped') }
        @{ Rx = '6\.\s*Review';    Name = '6. Review';    Ok = @('Approved') }
        @{ Rx = '7\.\s*Document';  Name = '7. Document';  Ok = @('Done') }
    )

    foreach ($s in $stages) {
        $m = [regex]::Match($doc, "(?m)^\|\s*$($s.Rx)\s*\|\s*([^|]*?)\s*\|\s*([^|]*?)\s*\|")
        if (-not $m.Success) { Fail "status.md has no '| $($s.Name) |' row"; continue }
        $state = $m.Groups[1].Value.Trim()
        $gate = $m.Groups[2].Value.Trim()
        $stateOk = @($s.Ok | Where-Object { $state -ieq $_ }).Count -gt 0

        if (-not $stateOk) {
            Fail "$($s.Name) - state '$state' is not ship-ready (expected: $($s.Ok -join '/')); gate: $gate"
        }
        elseif ($gate -match [regex]::Escape($RED)) {
            Fail "$($s.Name) - reached '$state' but a $RED gate failure is recorded: $gate"
        }
        elseif ($gate -match '(?i)waived') {
            Fail "$($s.Name) - passed with a carried waiver: $gate"
        }
    }
}

# ------------------------------------------------------------------ dispatch --
switch ($Mode) {
    'spec'     { if (-not $Feature) { throw 'spec mode requires -Feature' };     Test-SpecGate $Feature }
    'plan'     { if (-not $Feature) { throw 'plan mode requires -Feature' };     Test-PlanGate $Feature }
    'manifest' { if (-not $Feature) { throw 'manifest mode requires -Feature' }; Test-ManifestGate $Feature }
    'compile'  { if (-not $Feature) { throw 'compile mode requires -Feature' };  Test-CompileGate $Feature }
    'e2e'      { if (-not $Feature) { throw 'e2e mode requires -Feature' };      Test-E2eGate $Feature }
    'status'   { if (-not $Feature) { throw 'status mode requires -Feature' };   Test-StatusGate $Feature }
    'ship-ready' { if (-not $Feature) { throw 'ship-ready mode requires -Feature' }; Test-ShipReadyGate $Feature }
    'scope'    { Test-ScopeGate }
    'snapshot' { Invoke-SnapshotMode }
    'doc-only' { Test-DocOnlyGate }
}

$label = $Mode + $(if ($Feature) { ":$Feature" } elseif ($Phase) { ":$Phase" } else { '' })
Complete-Run $label
