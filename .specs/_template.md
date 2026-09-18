# <Feature name>

Status: Draft

<!-- Copy to .specs/features/<feature-name>/spec.md.
Follow .specs/README.md. Replace placeholders and remove inapplicable rows/fields.
Use Draft, Building, Verified, Shipped. Record blockers without advancing status.
Keep this document short; do not add separate plans or reports by default. -->

## 1. Goal & Scope

**Outcome:** <Who benefits, and what can they accomplish?>

**Included:** <Behavior delivered by this feature.>

**Excluded:** <Relevant boundaries that prevent scope expansion.>

**Constraints / open questions, if needed:** <Only meaningful decisions; resolve blockers before building.>

**Design target — UI only:** <Exact Figma frame link(s); frame/node identifier and reference date/version when available.>

**Viewports and states — UI only:** <Map each frame to viewport dimensions and required states; include mobile frames when supplied.>

**Agreed design deviations — if any:** <Explicitly accepted differences and reason; otherwise none.>

## 2. Acceptance Criteria

- [ ] AC1: <Observable behavior and expected result.>
- [ ] AC2: <Relevant boundary, edge case, or failure behavior.>
- [ ] AC3 — UI only: <Named page/state matches the referenced frame at the specified viewport for layout, typography, spacing, colors, and assets, subject to agreed deviations.>

<!-- Usually 3–7 criteria; add only what the feature needs.
Use Given/When/Then when helpful. Check boxes only after verification.
Every criterion needs evidence, but not every criterion needs every test type. -->

## 3. Implementation Plan

**Approach:** <Smallest appropriate implementation grounded in inspected code.>

**Affected areas:** <Actual components, modules, or paths.>

- [ ] <Implementation step.>
- [ ] <Necessary tests; use a failing-test-first loop for important logic or reproducible bugs where useful.>
- [ ] <UI only: use existing tokens/components and design assets; compare rendered screenshots with Figma and correct discrepancies.>

<!-- Add compatibility, migration, or recovery decisions only when relevant.
Update agreed scope changes before implementing them. Do not weaken criteria
to accommodate an incorrect implementation. -->

## 4. Verification

| Criteria | Check | Exact command + working directory, or review procedure | Result / evidence |
| --- | --- | --- | --- |
| <AC IDs> | Unit | <Relevant logic / edge-case checks> | Not run |
| <AC IDs> | Playwright functional E2E | <User journey and actual command> | Not run |
| <Visual AC IDs> | Design fidelity | <Figma reference versus rendered screenshot; viewport/state> | Not run; link evidence and list discrepancies |
| <Visual AC IDs> | Playwright visual regression, where useful | <Screenshot assertion against approved browser baseline> | Not run; baseline approval pending or reference |
| <AC IDs> | Manual, if needed | <Steps and expected outcome> | Not run |
| Project checks | Required lint / type check / build / CI | <Applicable actual commands or CI run> | Not run |

**Developer visual review — UI only:** <Pending / approved; record explicitly accepted deviations.>

**Remaining failures, blockers, or unverified items:** <Specific issue and next action; otherwise none.>

**Release reference — after shipping:** <Confirmed release, deployment, or project-defined release reference; otherwise not shipped.>

<!-- Record actual results, not intended results. Distinguish passed, failed,
and blocked/not-run. Include relevant code state or run reference where useful.
Fix visual differences and rerun affected checks. Do not approve browser
baselines before the rendering has been reviewed against the intended design.
Keep screenshots/reports in existing project artifact locations and link them;
avoid committing large generated test outputs here. -->
