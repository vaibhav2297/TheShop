# Browser visual loop

Canonical UI handoff for Plan, Resolve, Execute, E2E, Verify, Ship. No new workflow stage.
Figma remains design authority. Do not create or require `design-contract.json` or copy node trees into maintained design files.
Keep links, target states, viewport sizes, decisions in plan. Keep screenshots and review evidence under feature.
Legacy plans: add visual scope and target table before execution; do not grandfather UI through missing evidence.

## Plan and Resolve

Use `**Visual scope:** required` for any rendered UI change, including styling, assets and shared components.
Use `**Visual scope:** none` plus `**Visual exclusion:** {concrete backend-only reason}` only when rendered UI is unaffected.
Record one row per screen/viewport/state needing proof. Include affected shared component callers and designed mobile/desktop variants.

```markdown
**Visual scope:** required

### Visual targets

| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| catalogue-desktop | /catalogue | 1440x900 | seeded products, filters closed | https://www.figma.com/design/FILE/NAME?node-id=123-456 |
| catalogue-mobile | /catalogue | 390x844 | same products, filters closed | https://www.figma.com/design/FILE/NAME?node-id=123-457 |
```

Surface IDs: lowercase letters/digits/hyphens, unique. Routes: local absolute paths. Viewports: CSS pixels, `WIDTHxHEIGHT`.
Reference: exact Figma frame URL, or path to explicitly accepted screenshot when Figma is unavailable/skipped.
No reference: unresolved design gap, not permission to invent appearance. Record proposed reference and obtain decision through Resolve.
For widths/states lacking designs, Resolve records expected responsive behavior. Do not claim Figma parity for inferred designs.
Resolve MudBlazor, typography and token conflicts once. Keep accepted decisions in plan; do not ask repeatedly during execution.

## Execute: inspect, render, compare, correct

1. Re-fetch exact Figma nodes at Web phase start. Use available MCP tools for structure/measurements, variants, assets and screenshot.
   Inspect screenshot with image tool. Split oversized responses by section. Tool names vary by Figma provider.
   Record revision or capture date. If design changed since Resolve, reconcile affected decisions before coding.
   Export reference PNG at 1x. Keep exact assets locally; never use whole screenshot as implementation.
2. Implement section using project components. Match layout, typography, spacing, imagery and states.
3. Build current Web source. Start app using capture helper, or reuse an app built from current source.
   An already running host must be rebuilt/reloaded after source edits; hashes alone cannot prove its served build.
4. Capture each planned target using command below. Use deterministic local data and existing test persona/storage state.
   Add browser actions for dialogs, hover, validation or other planned state. Do not use production credentials or production data.
   Wait for feature-specific readiness, not `body` or HTTP 200. Include a final `--wait-for` after asynchronous state changes.
5. **Open and inspect all four images:** reference, actual, overlay, difference. Read `browser.md` geometry/computed styles.
   Compare layout first, then fonts/wrapping, spacing, colors, icons/images/crop, overflow and state.
   Agent must actually view images; file existence and pixel count cannot establish fidelity.
6. Correct responsible Razor/SCSS/theme code. Rebuild and repeat affected targets. After final edit, recapture all targets invalidated by source hash.
   Three consecutive passes without reducing differences: stop with concrete blockers and resume command. Keep Implement pending.
   Browser inspection is permitted in Execute; feature test authoring remains with Test/E2E.
7. Write `review.md` only after inspecting final capture. Run visual gate, then mark Web complete.

## Capture command

```powershell
pwsh -NoProfile -File .sdd/scripts/capture-ui.ps1 -Feature {feature} -Surface catalogue-desktop -Reference {exported-png} -ReferenceRevision '{revision-or-capture-date}' -Ready '[data-testid="catalogue-grid"]' -StartApp -LocalE2E
```

Helper runs headless Chromium, viewport from plan, stable screenshots, fonts/images readiness, animation suppression,
element geometry, 50% overlay and pixel difference. It emits **UNREVIEWED**, never PASS.
Existing E2E environment settings are reused through `ShopBrowser`; no database reset occurs.
`-LocalE2E` requires prepared `.e2e-env`. Prepare missing local dependencies explicitly; do not invoke reset scripts implicitly.
Omit `-LocalE2E` only for a local app with safe, deterministic configuration. Missing setup is a blocker, not reason to capture blank UI.
`-StartApp` starts `dotnet run` at localhost:5218 and stops only its own process tree in `finally`.
Occupied port fails; omit flag to intentionally reuse an existing current host. Browser/context always close; external hosts stay running.
Normal failures clean up automatically. If tool process is forcibly terminated, inspect owned app process and stop it explicitly.

Optional parameters:

- `-StorageState {path}`: existing local test persona state; never commit auth files.
- `-Crop '[data-testid="product-card"]'`: compare exact component bounds with matching reference export; no stretching.
- `-BaseUrl http://localhost:{port}`: use an existing local server; incompatible with `-StartApp` on other ports.
- `-BrowserArguments @('--click','[data-testid="open-dialog"]','--wait-for','[data-testid="dialog"]')`: ordered actions.
  Invoke from PowerShell with an array. Actions: `--click selector`, `--fill selector value`, `--hover selector`,
  `--press selector key`, `--wait-for selector`, `--scroll selector`. Keep sensitive values out of command history.

First setup:

```powershell
dotnet build .sdd/tools/VisualCapture/VisualCapture.csproj --nologo
pwsh .sdd/tools/VisualCapture/bin/Debug/net10.0/playwright.ps1 install chromium
```

Reference/capture dimensions must match. Default capture is viewport, not full scroll height.
Use matching crop or multiple targets for tall designs. Seed matching text/images; wait for data explicitly.
Fonts, browser version, OS, viewport, scale and data must remain stable. CSS background images and font fallback need agent inspection too.
Changed pixel count is diagnostic only. No universal percentage threshold: text rasterization differs between Figma and browser.
Exact image diff is useful for browser regression baselines; Figma fidelity additionally needs visual and geometry review.

## Review and gates

Artifacts: `.specs/{feature}/evidence/visual/{surface}/` contains `reference.png`, `actual.png`, `overlay.png`,
`difference.png`, `browser.md`, `capture.md`, agent-authored `review.md`.
Helper prints capture SHA256. Put that exact value in review after inspection:

```markdown
# Visual review

**Capture SHA256:** {hash printed by helper}
**Reviewer:** {agent or human that inspected images}
**Checks:** layout; typography/wrapping; spacing; colors; assets/crop; responsive behavior; target state
**Findings:** {specific comparison findings, measured corrections and any accepted rasterization differences}
**Verdict:** PASS
```

Unresolved material mismatch: use FAIL. Missing visual inspection: use UNREVIEWED.
Do not auto-fill PASS, replace reference with current output, hide changed regions or weaken checks to turn gate green.
Missing assets, wrong fonts, clipped text or wrong layout/state cannot be dismissed as rasterization noise.

```powershell
pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 visual -Feature {feature}
```

Gate checks target coverage, artifact integrity, current source/spec/plan fingerprint, and review bound to exact capture.
It validates evidence integrity, **not the truth of visual judgment**. Review remains agent/human responsibility.
Source fingerprint includes uncommitted changes; later commit or tracker-only edit does not invalidate images.
Reference changes require fresh MCP inspection, export, capture and review. Offline gate cannot detect remote Figma changes.

## E2E, Verify and Ship

Keep behavioral checks and visual checks independent. E2E cannot mark Verify complete with failed visual evidence.
Verify runs visual gate even when reusing fresh behavioral evidence. Inspect cited comparisons; stale evidence returns to Execute.
Ship runs visual gate through ship-ready. No missing/stale/failed visual proof may be waived as ordinary ledger debt.
Backend-only scope skips capture with explicit reason.

After fidelity passes, E2E can add browser screenshot regression checks for high-risk shared surfaces using approved captures.
Keep accepted browser baselines separate from Figma references. Do not auto-update baselines after regression failures.
Plan any comparison-library/harness additions explicitly; .NET Playwright screenshots alone are not visual assertions.
