# check-sdd-gates.ps1 - deterministic verification gates for the SDD pipeline
# artifacts. Companion to check-design-rules.ps1 (which lints *code*); this one
# validates the pipeline's *artifacts* and *process invariants* so each step can
# prove its output meets the contract instead of asserting it.
#
# Modes:
#   spec      -Feature x              spec.md template conformance, FR/AC id sequence,
#                                     footer <-> appendix consistency
#   plan      -Feature x              plan.md template conformance, AC coverage vs the
#                                     spec, Section 11 <-> footer consistency
#   manifest  -Feature x              test-manifest.json: count arithmetic, AC ids vs
#                                     spec, listed files exist, feature trait stamped
#   compile   -Feature x              dotnet-builds every test project the manifest
#                                     lists (transitively building the layers they
#                                     reference); emits each compiler error tagged
#                                     [tests]/[src] by the failing file's location
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
    [ValidateSet('spec', 'plan', 'manifest', 'compile', 'scope', 'snapshot', 'doc-only', 'status', 'ship-ready')]
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

function Complete-Run([string]$Label) {
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
        if ($s1 -notmatch '\*\*In scope:\*\*')  { Fail "Section 1 missing '**In scope:**' block" }
        if ($s1 -notmatch '\*\*Out of scope:\*\*') { Fail "Section 1 missing '**Out of scope:**' block" }
    }

    $s2 = Get-NumberedSection $c 2
    if ($s2) {
        $frIds = @([regex]::Matches($s2, '\*\*FR-(\d+):\*\*') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        Test-IdSequence $frIds 'FR' 'Section 2'
    }

    $s3 = Get-NumberedSection $c 3
    if ($s3 -and $s3 -notmatch '(?m)^### Behavior') { Fail "Section 3 has no '### Behavior n:' subsections" }

    $s5 = Get-NumberedSection $c 5
    if ($s5 -and $s5 -notmatch '\*\*Edge case:\*\*') { Fail "Section 5 has no '**Edge case:** ... -> **User experience:** ...' items" }

    $s6 = Get-NumberedSection $c 6
    if ($s6) {
        $acIds = Get-SpecAcIds $c
        Test-IdSequence $acIds 'AC' 'Section 6'
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

    # AC coverage: every AC in the spec must be mapped in Section 8.
    $spec = Read-Doc ".specs/$F/spec.md"
    if (-not $spec) { Fail "companion spec .specs/$F/spec.md not found - cannot verify AC coverage" }
    else {
        $acIds = Get-SpecAcIds $spec
        $s8 = Get-NumberedSection $c 8
        if ($acIds.Count -eq 0) { Fail 'spec Section 6 has no **AC-n:** items to map' }
        elseif ($s8) {
            foreach ($id in $acIds) {
                if ($s8 -notmatch "\bAC-$id\b") { Fail "AC-$id from the spec is not mapped in plan Section 8" }
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
    'status'   { if (-not $Feature) { throw 'status mode requires -Feature' };   Test-StatusGate $Feature }
    'ship-ready' { if (-not $Feature) { throw 'ship-ready mode requires -Feature' }; Test-ShipReadyGate $Feature }
    'scope'    { Test-ScopeGate }
    'snapshot' { Invoke-SnapshotMode }
    'doc-only' { Test-DocOnlyGate }
}

$label = $Mode + $(if ($Feature) { ":$Feature" } elseif ($Phase) { ":$Phase" } else { '' })
Complete-Run $label
