---
name: theshop-e2e
description: Optionally create, run, repair, and report feature-scoped Playwright journeys in one session.
argument-hint: <feature-name>
disable-model-invocation: true
---

# E2E Feature

Optional browser-proof helper. It may satisfy the Verify ledger row, but is not a mandatory workflow stage.

Figma UI requires browser regression proof before Verify. Helper remains outside stage sequence.
Read [visual fidelity — Test / E2E and Verify / Ship](../theshop-plan/references/visual-fidelity.md).

Own one loop:

`classify → write → gate → build → start → run → repair → report`

No sub-agents.

## Authority

May edit:

- feature journeys under `tests/TheShop.E2E.Tests/Journeys/`
- feature page objects under `tests/TheShop.E2E.Tests/Pages/`
- `.specs/{feature}/e2e-manifest.json`
- `.specs/{feature}/e2e-report.md`
- Verify row in `.specs/{feature}/status.md`
- `.specs/{feature}/visual/` captures, reviews, reports and initially aligned baselines

Never edit references/context/contract to force green. Never auto-overwrite baselines.

May inspect `src/` for routes and locator seams.

Never edit:

- `src/`
- E2E `Fixtures/`, `Auth/`, or `tools/`
- unit/component tests
- spec, plan, project/package files, or dependencies

Never weaken/delete/skip assertions, add blind retries, or use fixed sleeps to force green.

## Input

Require a safe feature folder name. Require:

- `spec.md`: oracle and ACs
- `plan.md`: routes/Web scope
- `test-manifest.json`: lower-level evidence
- `status.md`: Test state

Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature {feature}
```

Red manifest: halt.

Backend-only features write a skipped `e2e-report.md`, mark Verify `Skipped`, and stop.

## Classify every AC

Exactly one bucket, in spec order:

- `e2e`: real navigation, auth, persistence, RLS/RPC/storage, permissions, cross-surface effects
- `unit`: pure rule already mapped to the same AC in `test-manifest.json`
- `manual`: genuinely human-only; include reason

Borderline → `e2e`. Manual is narrow.

Visual ACs belong to `e2e`. Capture every design-contract surface using the existing harness.
After fresh capture/review, run `align`, create `baseline` only if absent, then `regression`
and `verify` with `.sdd/scripts/visual-fidelity.py --feature {feature}` (mode before flag).
Missing browser/failing pixels block. Never substitute manual pass.

Write `e2e-manifest.json` with feature, trait, date, journey files/FQNs/test counts, and every AC's bucket/evidence/reason.

Rules:

- every AC exactly once
- each E2E method named `AC{n}_{Behavior}`
- each method claimed by its AC
- journey count equals actual methods
- unit evidence matches the same AC in unit manifest

## Write journeys

Class traits:

```csharp
[Trait("Category", "E2E")]
[Trait("Feature", "{feature}")]
```

Use `[Fact]` and `public async Task AC{n}_{Behavior}()`. No `[Theory]`.

Follow existing harness:

- anonymous: `E2ETestBase`
- signed-in: `AuthenticatedE2ETestBase` + existing persona
- fixtures launch app; never launch it manually
- `data-testid` first, accessible role/name second
- resource strings, never hardcoded localized UI text
- no MudBlazor internal CSS selectors
- unique generated DB rows
- Playwright auto-waits and explicit wait APIs; no `WaitForTimeout`
- route behavior belongs in a `ShopPage` page object
- XML comments link class to the spec

Missing `data-testid`: reference intended hook, do not edit `src/`, then halt after the static gate with exact hook and component.

## Static gates

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 e2e -Feature {feature}
dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo
```

One repair pass for manifest/journey/test compile errors.

Halt on:

- unresolved feature hook
- production compile error
- second static failure

Other-feature hook warnings are debt, not blockers.

## Environment and run

Starting the environment resets the local Supabase database. Run only within this skill's authorized local E2E flow:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter "Category=E2E&Feature={feature}" --logger "console;verbosity=normal" --nologo --no-build
```

Environment failure or skipped tests: halt; never count as pass.

Reconcile manifest count with discovered cases.

## Repair loop

Maximum three rounds. Re-run only failing methods.

Classify first:

- environment: repair environment only
- locator/timing: repair test/page object
- missing hook: halt
- product defect: record; do not change assertion
- unclear: treat as product defect

Use Playwright traces as evidence. After any test edit, rerun E2E gate and rebuild before verdict. Still red after three rounds: report.

## Teardown

Confirm fixture stopped app and port 5218 is free. Stop any orphan it created. Leave Supabase running; name `tools/stop-e2e-env.ps1` for explicit shutdown.

## Report and verdict

Overwrite `e2e-report.md`; final response must match it.

Include:

- date/commit
- bucket counts
- expected/discovered/passed/failed/skipped/repair rounds
- every AC with bucket, result, evidence
- findings with FQN, class, symptom, trace, hypothesis
- environment/teardown
- verdict and next step

Verdicts:

- `✅ VERIFIED`: static gates clean, all E2E ACs pass, no skips; unit ACs require Test row `Passing`
- `🔴 NOT VERIFIED`: failed/unclassified AC, missing hook, environment failure, or uncorroborated unit AC
- `⛔ HALTED`: static gate/build/environment stopped journey
- `⏭️ SKIPPED`: backend-only

Manual ACs stay `⚠️ Unverified`. Block VERIFIED until explicit confirmation through Verify.
Figma UI requires visual gate pass. Behavioral success cannot waive it.

Update Verify row accordingly; evidence links `e2e-report.md`; date today; next optional Document, otherwise Ship.

Before return, verify report AC order/counts, manifest counts, unit evidence, verdict, and tracker all agree. Inconsistency forces `🔴 NOT VERIFIED`.
