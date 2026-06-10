# check-design-rules.ps1 - deterministic linter for the mechanically-checkable
# subset of the theshop.constitution rules. Rule numbers in the output refer to
# .claude/skills/theshop.constitution/SKILL.md.
#
# Modes:
#   Hook mode (no args)   PostToolUse hook on Edit|Write. Reads the hook JSON from
#                         stdin, lints the edited file. Violations -> stderr, exit 2
#                         (Claude Code feeds stderr back to the model immediately).
#   Path mode (-Path ...) Lints the given files/directories. Used by /theshop.review
#                         or manually: pwsh .claude/scripts/check-design-rules.ps1 -Path src
#                         Violations -> stdout, exit 1.
#
# Scope: *.razor / *.cs under src/ only.
# Escape hatch: a line containing "design-rules: ignore" is skipped (use sparingly,
# with a justification comment).

[CmdletBinding()]
param(
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Get-FileViolations {
    param([string]$FilePath)

    $abs = (Resolve-Path -LiteralPath $FilePath).Path
    if (-not $abs.StartsWith($repoRoot)) { return @() }
    $rel = $abs.Substring($repoRoot.Length).TrimStart('\', '/') -replace '\\', '/'

    if ($rel -notmatch '^src/') { return @() }
    if ($rel -notmatch '\.(razor|cs)$') { return @() }

    $content = Get-Content -Raw -LiteralPath $abs -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($content)) { return @() }

    $isRazor  = $rel.EndsWith('.razor')
    $isWeb    = $rel.StartsWith('src/TheShop.Web/')
    $isDomain = $rel.StartsWith('src/TheShop.Domain/')
    $isApp    = $rel.StartsWith('src/TheShop.Application/')

    # Line-based checks. Rx is matched per line; Rule/Msg cite the constitution.
    $checks = @()
    if ($isRazor) {
        $checks += @{ Rx = '^\s*@page\b'; Rule = 20; Msg = '@page directive in markup - declare [Route(Routes.X)] on the .razor.cs partial instead' }
        $checks += @{ Rx = '<(span|p|h[1-6])\b'; Rule = 16; Msg = 'native HTML text element - use <MudText Typo="...">' }
        $checks += @{ Rx = '<style\b'; Rule = 28; Msg = '<style> block in .razor - move to SCSS under src/TheShop.Web/Styles/' }
        $checks += @{ Rx = '(?<!&)#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b'; Rule = 15; Msg = 'hardcoded hex value - use Color="Color.X" or the most specific mud-* class' }
        $checks += @{ Rx = 'Href\s*=\s*"/'; Rule = 21; Msg = 'hardcoded route in Href - use Routes.X' }
    }
    if ($isWeb) {
        $checks += @{ Rx = 'Icons\.Material'; Rule = 19; Msg = 'Material icon referenced - all icons come from ShopIcons' }
        $checks += @{ Rx = 'Localizer\[\s*"'; Rule = 11; Msg = 'string-literal resource key - use the typed Strings.{Key} accessor (Localizer[] is for runtime keys only)' }
        $checks += @{ Rx = 'NavigateTo\(\s*\$?"'; Rule = 21; Msg = 'hardcoded route in NavigateTo - use Routes.X or a Routes helper method' }
        # The busy infrastructure itself (BusyFor, ShopLoadingOverlay, BusyState) legitimately
        # holds an internal _isBusy - Rule 22 bans it everywhere else.
        if ($rel -notmatch '/(BusyFor|ShopLoadingOverlay|BusyState)\.(razor|razor\.cs|cs)$') {
            $checks += @{ Rx = '\b_isBusy\b'; Rule = 22; Msg = '_isBusy field is banned - drive work via BusyState.RunAsync(BusyKeys.X, ...)' }
        }
    }
    if ($isDomain) {
        $checks += @{ Rx = '^\s*using\s+(Supabase|Stripe|Resend|MudBlazor|Newtonsoft|System\.Text\.Json)'; Rule = 2; Msg = 'external SDK / serialization import in Domain - Domain is pure C#' }
    }
    if ($isApp) {
        $checks += @{ Rx = '^\s*using\s+(Supabase|Stripe|Resend|MudBlazor)'; Rule = 3; Msg = 'external SDK import in Application - SDKs live only in Infrastructure behind an Application interface' }
        $checks += @{ Rx = '\bResult(?:<[^>]+>)?\.Fail\(\s*\$?"'; Rule = 12; Msg = 'string-literal error key - return nameof(Strings.{Key})' }
    }

    $violations = [System.Collections.Generic.List[object]]::new()
    $lines = $content -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match 'design-rules:\s*ignore') { continue }
        $trim = $line.TrimStart()
        if ($trim.StartsWith('//') -or $trim.StartsWith('@*')) { continue }

        foreach ($c in $checks) {
            if ($line -match $c.Rx) {
                $violations.Add([pscustomobject]@{ Line = $i + 1; Rule = $c.Rule; Msg = $c.Msg })
            }
        }

        # Rule 27 (heuristic): ternary or interpolation inside a Class/Style attribute.
        if ($isRazor -and $line -match '(Class|Style)\s*=\s*"@\(') {
            if (($line -match '\s\?\s' -and $line -match '\s:\s') -or $line -match '(Class|Style)\s*=\s*"@\(\$"') {
                $violations.Add([pscustomobject]@{ Line = $i + 1; Rule = 27; Msg = 'class/style string built inline (heuristic) - compose with CssBuilder / StyleBuilder' })
            }
        }
    }

    # Rule 17 (tag may span lines): MudTextField using Label=.
    if ($isRazor) {
        foreach ($m in [regex]::Matches($content, '<MudTextField[^>]*?\bLabel\s*=')) {
            $lineNo = $content.Substring(0, $m.Index).Split("`n").Count
            $violations.Add([pscustomobject]@{ Line = $lineNo; Rule = 17; Msg = 'MudTextField uses Label - use Placeholder (sibling MudText Typo.caption if a visible label is required)' })
        }
    }

    foreach ($v in $violations) {
        $v | Add-Member -NotePropertyName File -NotePropertyValue $rel
    }
    return @($violations | Sort-Object Line)
}

function Format-Report {
    param($Violations)
    $sb = [System.Text.StringBuilder]::new()
    foreach ($g in ($Violations | Group-Object File)) {
        [void]$sb.AppendLine("[design-rules] $($g.Name) - $($g.Count) violation(s) of theshop.constitution:")
        foreach ($v in $g.Group) {
            [void]$sb.AppendLine(("  L{0,-5} Rule {1}: {2}" -f $v.Line, $v.Rule, $v.Msg))
        }
    }
    [void]$sb.Append('Fix every violation above now (edit the file again). Rules live in .claude/skills/theshop.constitution/SKILL.md; load the matching reference from its routing table if you need the pattern.')
    return $sb.ToString()
}

if ($Path) {
    # ---- Path mode ----
    $targets = foreach ($p in $Path) {
        if (Test-Path -LiteralPath $p -PathType Container) {
            Get-ChildItem -Path $p -Recurse -File -Include *.razor, *.cs | ForEach-Object FullName
        }
        elseif (Test-Path -LiteralPath $p) {
            (Resolve-Path -LiteralPath $p).Path
        }
    }
    $all = @($targets | ForEach-Object { Get-FileViolations $_ })
    if ($all.Count -gt 0) {
        Write-Output (Format-Report $all)
        exit 1
    }
    Write-Output '[design-rules] clean - no violations.'
    exit 0
}

# ---- Hook mode ----
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$file = $payload.tool_input.file_path
if (-not $file) { $file = $payload.tool_response.filePath }
if (-not $file -or -not (Test-Path -LiteralPath $file)) { exit 0 }

$violations = @(Get-FileViolations $file)
if ($violations.Count -eq 0) { exit 0 }

[Console]::Error.WriteLine((Format-Report $violations))
exit 2
