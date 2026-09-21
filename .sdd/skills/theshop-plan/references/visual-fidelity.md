# Visual fidelity contract

Shared Figma rules. Existing stages only. Artifacts: `.specs/{feature}/`. No new ledger row.

## Spec / Clarify

Name screens, desktop/mobile viewports, locale, fixtures, required states. Add visual ACs alongside behavior ACs.
Clarify responsive behavior. Desktop frame never defines mobile. Label inferred states.

## Plan / Resolve

Create `design-contract.json` from [example](../templates/design-contract.example.json). Replace examples; calibrate tolerances.

- Every feature declares `mode`: `figma` or `backend`. Backend: concrete reason, no UI surfaces.
- Existing features adopt on next Plan/Verify/Ship. Missing evidence fails; no silent grandfathering.
- Missing/skipped Figma: blocking Section 11 item. Waivers never prove fidelity.
- Fetch `get_design_context` and `get_screenshot` for exact nodes. Truncated: metadata, then smaller nodes.
- MCP unavailable: equivalent exported measurements/assets/screenshots allowed. Record provenance. URL alone fails.
- Pin node URL, revision/export timestamp, screenshot/context SHA-256. Store assets through normal implementation. Never ship temporary MCP URLs.
- Context: geometry, typography (family/weight/size/line-height/letter spacing), spacing, colors, radii, borders, shadows, crop, interactions, responsive behavior.
- Map measured values to MudBlazor, Shop tokens/icons, SCSS, component paths. Reuse equivalent values only. Resolve unsupported mappings before Execute.
- One surface per route/viewport/locale/state. Map to spec ACs. PNG dimensions equal viewport times device scale. No browser chrome or corrective resizing.
- Include whole-frame and critical regions. Define pixel thresholds, difference ratios, rationale. Calibrate against faithful render. Never loosen thresholds/references to force green.
- `environment`: exact browser/version, OS, device scale, color scheme, reduced motion, timezone, loaded fonts, fixture identity.
- `checks`: expected geometry, typography, assets, state/responsive results. Pixel comparison alone cannot prove these.
- `sourcePaths`: extra input files outside automatic fingerprint roots. Tool includes `src/`, `tests/`, `supabase/`, root inputs, compiled web CSS. Git ignores exclude build output/secrets/browser artifacts.

Run `python .sdd/scripts/visual-fidelity.py plan --feature {feature}` before resolving.
Incomplete contract blocks UI coding. Resolve may edit contract/context alongside plan. Contract changes invalidate visual evidence.

## Execute: automatic correction loop

Load contract, images, context at Web phase. Implement one surface. Build. Launch existing app/harness.
Use available browser automation. Database resets require authorized E2E flow.

1. Before rendering, run `python .sdd/scripts/visual-fidelity.py fingerprint --feature {feature}`. Capture against that source. Source changes: rebuild/recapture.
2. Pin environment/fixtures. Wait for app readiness, `document.fonts.ready`, image decoding, settled layout. Disable animations. No fixed sleeps.
3. Capture viewport PNGs: `visual/actual/{id}.png`.
4. Browser driver writes `visual/capture.json`: `sourceFingerprint`, actual `environment`, `images` mapping every surface ID to PNG SHA-256. Environment must match contract.
5. Inspect actual/Figma images. Write `visual/review.json`: same fingerprint/images; `surfaces` maps IDs to `geometry`, `typography`, `assets`, `states`. Each: `passed` boolean, concrete `observed` measurements/results against contract checks. Never invent observations.
6. Run `python .sdd/scripts/visual-fidelity.py align --feature {feature}`. Outputs: overlays, differences, `visual/alignment.json`. References unchanged.
7. Fix layout, typography, spacing, assets, details. Rebuild; repeat capture/review/align. Maximum three correction rounds per surface.

Still failing/missing tools/fonts/access: report differences and resume point. Never mark Implement Done.
Alignment is implementation inspection; allowed in Execute. Formal tests remain Test/E2E.
Record evidence/commands in Implement row. Stop only app processes started here.

## Test / E2E: regression protection

Test maps visual ACs to `e2e`. Never substitute bUnit/manual proof.
E2E owns browser journeys, captures, visual evidence, initial baselines. Never change contract/context/references to force green.

After alignment passes: `python .sdd/scripts/visual-fidelity.py baseline --feature {feature}`.
Locks aligned captures under `visual/baseline/`. Refuses overwrite.
Redesign: new contract, explicit baseline replacement authorization, old baseline preserved in Git. Never auto-update failures.

Add feature journeys using existing .NET Playwright harness. Reproduce every surface; write capture/review evidence.
Use `Page.ScreenshotAsync`: viewport capture, animations disabled. Hash PNGs with SHA-256. Use fingerprint command above.
Node Playwright screenshot assertions are not assumed available in .NET.

Fresh captures: `python .sdd/scripts/visual-fidelity.py regression --feature {feature}`.
Outputs `visual/regression.json` and differences against locked browser baselines.
Source/contract/captures changed: rerun alignment/review too.

## Verify / Ship

Run `python .sdd/scripts/visual-fidelity.py verify --feature {feature}` alongside behavioral gates.
Recomputes comparisons; validates source/contract/image hashes, region results, review, baseline provenance.
Status/commit alone never proves fidelity. Status-only commits preserve evidence. Dirty relevant inputs invalidate it.

Verify never edits source/tests. Failures return through Execute, Test/E2E, Verify.
Behavioral E2E cannot bypass visual gate. Manual visual ACs block completion.
Ship requires visual gate even without tracker. Generic `proceed` never converts visual failure into pass.

Gate proves evidence integrity and measured differences. Browser provenance/semantic observations remain declared evidence.
Never claim tool proved browser ran. No blanket cross-browser pixel guarantee. Preserve tolerances/deviations.

## Tooling

Python 3.10+. Images require Pillow:
`python -m pip install -r .sdd/scripts/visual-requirements.txt` in approved environment.
Plan/backend gates use standard library. Tool never launches app or accesses network.
