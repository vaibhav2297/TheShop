# Shared visual evidence contract. Dot-source from capture and SDD gates.
function Get-VisualField([string]$Text, [string]$Name) {
    $match = [regex]::Match($Text, '(?m)^\*\*' + [regex]::Escape($Name) + ':\*\*\s*([^\r\n]+)\s*$')
    if ($match.Success) { $match.Groups[1].Value.Trim() } else { '' }
}

function Get-VisualTargets([string]$Plan) {
    $scope = Get-VisualField $Plan 'Visual scope'
    if ($scope -notin @('required', 'none')) { throw 'plan requires **Visual scope:** required|none; upgrade legacy plans before execution.' }
    $section = [regex]::Match($Plan, '(?ms)^### Visual targets\s*\r?\n(.*?)(?=^#{1,3} |\z)').Groups[1].Value
    $targets = @()
    foreach ($line in ($section -split '\r?\n')) {
        if ($line -notmatch '^\|') { continue }
        $cells = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
        if ($cells[0] -eq 'Surface' -or $cells[0] -match '^[-:]+$') { continue }
        if ($cells.Count -ne 5 -or $cells[0] -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or
            $cells[1] -notmatch '^/(?!/)' -or $cells[2] -notmatch '^[1-9][0-9]*x[1-9][0-9]*$' -or
            !$cells[3] -or !$cells[4] -or $cells[4] -match '[{}]') {
            throw 'invalid visual target; require Surface | Route | Viewport (WIDTHxHEIGHT) | State | Reference.'
        }
        $targets += [pscustomobject]@{ Surface=$cells[0]; Route=$cells[1]; Viewport=$cells[2]; State=$cells[3]; Reference=$cells[4] }
    }
    if ($scope -eq 'none') {
        if (!(Get-VisualField $Plan 'Visual exclusion') -or $targets.Count) { throw 'Visual scope none requires a Visual exclusion reason and no target rows.' }
    } elseif (!$targets.Count) { throw 'UI plan requires at least one row under ### Visual targets.' }
    if (@($targets.Surface | Select-Object -Unique).Count -ne $targets.Count) { throw 'duplicate visual surface ID.' }
    return $targets
}

function Get-VisualSourceHash([string]$Root, [string]$Feature) {
    function Get-SourceFiles([string]$Directory) {
        foreach ($item in Get-ChildItem -LiteralPath $Directory -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($item.PSIsContainer) {
                if ($item.Name -notin @('bin', 'obj', 'node_modules', '.git')) { Get-SourceFiles $item.FullName }
            } else { $item }
        }
    }
    $files = @(Get-SourceFiles (Join-Path $Root 'src'))
    $files += @(Get-SourceFiles (Join-Path $Root '.sdd/tools/VisualCapture'))
    foreach ($relative in @(".specs/$Feature/spec.md", ".specs/$Feature/plan.md", '.sdd/scripts/capture-ui.ps1', '.sdd/scripts/visual-evidence.ps1',
        'tests/TheShop.E2E.Tests/Fixtures/ShopBrowser.cs', 'tests/TheShop.E2E.Tests/Fixtures/E2EEnvironment.cs')) {
        $files += Get-Item -LiteralPath (Join-Path $Root $relative)
    }
    $lines = foreach ($file in ($files | Sort-Object FullName)) {
        [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/') + ':' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($lines -join "`n")
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

function Test-VisualEvidence([string]$Root, [string]$Feature, [switch]$PlanOnly) {
    try {
        $plan = Get-Content -LiteralPath (Join-Path $Root ".specs/$Feature/plan.md") -Raw
        $targets = @(Get-VisualTargets $plan)
        if ($PlanOnly -or !$targets.Count) { return }
        $sourceHash = Get-VisualSourceHash $Root $Feature
        foreach ($target in $targets) {
            $directory = Join-Path $Root ".specs/$Feature/evidence/visual/$($target.Surface)"
            $capturePath = Join-Path $directory 'capture.md'
            $reviewPath = Join-Path $directory 'review.md'
            if (!(Test-Path -LiteralPath $capturePath) -or !(Test-Path -LiteralPath $reviewPath)) {
                "$($target.Surface): capture.md and review.md required."; continue
            }
            $capture = Get-Content -LiteralPath $capturePath -Raw
            $review = Get-Content -LiteralPath $reviewPath -Raw
            foreach ($field in @('Surface', 'Route', 'Viewport', 'State', 'Reference')) {
                if ((Get-VisualField $capture $field) -cne $target.$field) { "$($target.Surface): capture $field does not match plan." }
            }
            if (!(Get-VisualField $capture 'Reference revision')) { "$($target.Surface): reference revision/date missing." }
            if ((Get-VisualField $capture 'Source SHA256') -cne $sourceHash) { "$($target.Surface): source/spec/plan changed; recapture and review." }
            foreach ($name in @('reference.png', 'actual.png', 'overlay.png', 'difference.png', 'browser.md')) {
                $path = Join-Path $directory $name
                if (!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -eq 0) { "$($target.Surface): $name missing or empty."; continue }
                if ((Get-VisualField $capture "$name SHA256") -cne (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash) { "$($target.Surface): $name changed since capture." }
            }
            if ((Get-VisualField $review 'Capture SHA256') -cne (Get-FileHash -LiteralPath $capturePath -Algorithm SHA256).Hash) { "$($target.Surface): review does not match capture." }
            if ((Get-VisualField $review 'Verdict') -cne 'PASS') { "$($target.Surface): visual review is not PASS." }
            foreach ($field in @('Reviewer', 'Findings', 'Checks')) {
                $value = Get-VisualField $review $field
                if (!$value -or $value -match '(?i)TODO|UNREVIEWED|\{.*\}') { "$($target.Surface): review $field missing or unfinished." }
            }
        }
    } catch { $_.Exception.Message }
}
