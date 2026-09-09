---
name: theshop-e2e
description: "Prove user-facing feature acceptance criteria through browser journeys; classify coverage, run, repair, and report in one context. Supersedes {{command:theshop-verify}}."
---

Read `.sdd/contracts/test-proof.md` before classification or reporting. It defines Passed, Deferred, Failed, and Not Covered, including the stage-specific `Ready for E2E — deferred proof remains` verdict. Deferred ACs stay outside passed counts; supporting tests still must pass.


# {{command:theshop-e2e}}

**Feature requested:** `{arguments}`

Own E2E proof in one context. Read sources, classify ACs, write journey, gate, start environment, run, repair, rerun, report.

Never delegate. Write and execute browser journeys in this context; compilation cannot prove locators, waits, dialog timing, or auth state.

This command supersedes `{{command:theshop-verify}}` and writes the same `5. Verify` ledger row, so `ship-ready` keeps working unchanged. If both commands exist, run this one.

## Scope and authority

You may:

- Read the feature artifacts, `src/` for locator seams and route names, existing journeys, page objects, fixtures, and repository guidance.
- Create or edit this feature's journey under `tests/TheShop.E2E.Tests/Journeys/` and its page objects under `tests/TheShop.E2E.Tests/Pages/`.
- Write `.specs/{arguments}/e2e-manifest.json` and `.specs/{arguments}/e2e-report.md`, and update the Verify row in `.specs/{arguments}/status.md`.
- Run the deterministic gates, the E2E project build, `tools/start-e2e-env.ps1`, filtered `dotnet test` runs, Playwright trace inspection, and `git rev-parse --short HEAD`.

You must not:

- Edit anything under `src/`. A missing UI hook is a **finding**, never a fix you make here.
- Edit `tests/TheShop.E2E.Tests/Fixtures/`, `Auth/`, or `tools/` — the shared harness is out of scope, and a feature journey that needs harness changes is a design problem to report.
- Edit unit/component tests, `test-manifest.json`, specs, plans, project files, or package references.
- Weaken, delete, skip, retry-loop, or loosen an assertion to obtain a green result, or add a bare `WaitForTimeout` to paper over a race.
- Derive expected behavior from the running app. The spec is the oracle; the app is the thing on trial.

## 1. Validate input and read the sources of truth

If `{arguments}` is empty, contains a path separator, or contains `..`, stop and ask for a feature folder name. Write nothing.

Read, in order:

| Artifact | Role here |
|---|---|
| `.specs/{arguments}/spec.md` | **The oracle.** §6 Acceptance Criteria are the pass/fail checklist; §3 Functional Behaviors are the click-paths. |
| `.specs/{arguments}/plan.md` | Routes, Web phase, Figma intent — tells you the surface and whether the feature is user-facing. |
| `.specs/{arguments}/test-manifest.json` | What is already proven below the browser. Required: `unit`-classified ACs are corroborated against it. |
| `.specs/{arguments}/status.md` | Upstream ledger. Warn (do not halt) if the Test row is not `Passing` — a red unit suite makes E2E failures ambiguous. Record the state: it constrains the verdict in step 10, because a `unit`-bucketed AC inherits its proof from that suite. |

Run the upstream gate before trusting the unit manifest:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature {arguments}
```

If it fails, halt with Template C — classification would be built on a manifest that does not describe reality.

Use `graphify query` first for codebase discovery when `graphify-out/graph.json` exists; otherwise targeted `Glob`/`Grep`/`Read`. Read the existing journeys and page objects for real conventions before writing anything.

## 2. Applicability

- **User-facing** if the plan has a Phase 4 — Web with tasks, **or** a Figma references block, **or** the feature shipped `.razor` files under `src/TheShop.Web/`.
- **Backend-only** otherwise.

Backend-only features **skip, they do not fail**: write Template B to `.specs/{arguments}/e2e-report.md`, set the Verify row to `Skipped` / `⏭️ backend-only`, emit the same text, and stop. Do not write an e2e-manifest.

## 3. Classify every acceptance criterion

Classify every AC **before writing tests**; never hide uncovered criteria.

Put every AC in the spec into exactly one bucket:

| Bucket | Use when | Requires |
|---|---|---|
| `e2e` | The AC's truth depends on real navigation, real auth, real persistence, or a cross-surface effect (admin action changing what a customer sees). | `evidence` = the FQN of an `AC{n}_` test you will write. |
| `unit` | The AC is already proven below the browser and the browser would only re-prove the same logic through a slower, flakier path. | `evidence` = a test FQN that `test-manifest.json` maps to **that same AC id**. |
| `manual` | Not machine-provable at all — assistive-technology behavior, visual/Figma fidelity, deliverability of a real email. | `reason` = a written sentence saying why. |

Bias rules, applied in this order:

1. Anything crossing a boundary the unit tests stub — RLS, RPC, storage, auth session, navigation — is `e2e`. Those are exactly the seams substitutes hide.
2. Permission and access-denied ACs are `e2e`. They are the ones where a component test can pass while the real route is wide open.
3. A pure validation rule, a clamp, a mapping, or a sort comparator is `unit`. Driving 30 of those through Chromium buys flake, not confidence.
4. When an AC is genuinely borderline, choose `e2e`. Over-covering costs seconds; under-covering costs a shipped defect.
5. `manual` is the narrow escape hatch, not the overflow bucket. If you reach for it more than a few times, re-read rule 4.

Write `.specs/{arguments}/e2e-manifest.json`:

```json
{
  "feature": "{arguments}",
  "trait": "{arguments}",
  "writtenAt": "YYYY-MM-DD",
  "journeys": [
    {
      "fqn": "TheShop.E2E.Tests.Journeys.FeatureJourneyTests",
      "file": "tests/TheShop.E2E.Tests/Journeys/FeatureJourneyTests.cs",
      "persona": "admin",
      "tests": 0
    }
  ],
  "acceptanceCriteria": [
    { "id": "AC-1", "coverage": "unit",   "evidence": "Namespace.ClassTests.Method_Name" },
    { "id": "AC-2", "coverage": "e2e",    "evidence": "TheShop.E2E.Tests.Journeys.FeatureJourneyTests.AC2_Behavior" },
    { "id": "AC-3", "coverage": "manual", "reason": "Screen-reader announcement order needs a human with assistive tech." }
  ]
}
```

Manifest rules: every spec AC appears exactly once, in spec order; `journeys[].tests` equals the number of `AC{n}_` methods actually in that file; an `e2e` evidence method must be named `AC{n}_` for its own id; every `AC{n}_` method you write must be claimed by an entry. The gate in step 5 enforces all of this.

`tests` is not knowable until step 4 has written the methods — the `0` above is a placeholder. **Backfill the real count before you run the step 5 gate**, or the gate will fail on a mismatch you created yourself.

## 4. Write the journey

Place it at `tests/TheShop.E2E.Tests/Journeys/{FeatureName}JourneyTests.cs`, stamped at class level:

```csharp
[Trait("Category", "E2E")]
[Trait("Feature", "{arguments}")]
```

Follow the conventions the existing journeys already demonstrate:

- Derive from `E2ETestBase` for anonymous journeys, `AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)` for signed-in ones. Personas: `AdminEmail`, `SupportEmail`, `CustomerEmail`. Never hand-roll a sign-in — `AuthStateFactory` caches storage state per run.
- **Do not launch the app.** `AppHostFixture` starts `dotnet run` once per E2E collection and polls until it serves. A manual launch will collide on port 5218.
- Name each test `AC{n}_{Behavior}` — the number is the mapping back to the spec, and the gate checks it.
- **Signature: `[Fact]` + `public async Task AC{n}_{Behavior}()`.** The gate matches `public [async] Task AC{n}_…`, so a `public void` test is invisible to it — it becomes a phantom that runs but is counted nowhere, and the mismatch surfaces as a confusing count error rather than a naming one. **No `[Theory]` in a journey:** the gate counts *methods* while step 7 reconciles *discovered cases*, so one theory with three rows makes those two numbers disagree permanently. If an AC genuinely needs several data shapes, write them as separate `AC{n}_` facts.
- Locate by `data-testid` first, then accessible role/name. Take visible text from `Strings.*` resource keys, never a hardcoded literal — CLAUDE.md rule 3 applies to test code that asserts on UI text, and a French run must not break the suite.
- Never depend on MudBlazor internal CSS classes.
- **Shared-database discipline:** every row a journey creates gets a unique generated name (`$"e2e-{thing}-{Guid.NewGuid():N}"`) so reruns never collide. Lean on the migration-seeded rows for "already in use" cases rather than building them.
- Prefer Playwright's auto-waiting assertions and `WaitForAsync`/`WaitForURLAsync` with explicit timeouts. A fixed sleep is a defect, not a fix.
- Add a page object under `Pages/` (deriving from `ShopPage`) whenever the feature adds a route; keep locators there and behavior in the journey.
- Add an XML doc comment on the class referencing `.specs/{arguments}/spec.md`, and on any test whose reason for existing at the E2E tier is not self-evident.

**If an AC needs a `data-testid` that `src/` does not define:** do not edit `src/`, and do not silently downgrade the AC to `manual` to dodge it. Write the test against the intended hook, let the step 5 gate name the missing hook, and halt with Template C listing the exact `data-testid` values the Web layer must add. That is a real, actionable finding — the whole point of catching it before a browser starts.

Keep halted journey on disk. Hook gate fails only this feature's journeys and referenced page objects; other features' pending hooks produce warnings. Web layer must add missing hook before this feature passes.

## 5. Static gates — everything knowable without a browser

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 e2e -Feature {arguments}
dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo
```

The `e2e` gate checks classification completeness, evidence resolution in both directions, orphan `AC{n}_` tests, and that every literal `GetByTestId("…")` resolves to a `data-testid` defined under `src/TheShop.Web`. That last check is **scoped to this feature** — its manifest journeys plus the page objects they reference fail the gate; unresolved hooks anywhere else are reported as warnings so another feature's pending work cannot block yours.

Routing:

- Manifest or journey violations: fix them here, re-run the gate once. If it still fails, halt with Template C.
- Unresolved `data-testid` (a failure, in this feature's files): halt with Template C naming each hook and the element that needs it. This is the missing-hook finding from step 4.
- Unresolved `data-testid` warnings in other features' files: do not fix them, do not halt. Carry them into the report's findings section as pre-existing debt.
- Compile errors in `tests/`: fix here, rebuild once.
- Compile errors in `src/`: halt with Template C — production is broken and no test edit helps.

Red static gate blocks environment startup.

## 6. Start the environment

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1
```

This runs `supabase start`, **`supabase db reset` (destructive to the local stack only — it re-applies migrations and seed)**, and writes `tests/TheShop.E2E.Tests/.e2e-env`. It is idempotent.

If it fails — Docker down, `supabase` CLI missing, migrations red — halt with Template C quoting the failure. Do not attempt to run tests without `.e2e-env`: every test would `Assert.Skip`, and a skipped E2E test is never a pass.

## 7. Run the journey

```powershell
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter "Category=E2E&Feature={arguments}" --logger "console;verbosity=normal" --nologo --no-build
```

`--no-build` is safe here and only here: step 5 just built this project and nothing has changed since. Drop it on every re-run in step 8, where you have just edited test code.

Expect the first run to be slow: the collection fixture launches the app and polls up to 120s, and each persona signs in through the real OTP UI once. The browser is **not** headless (`E2EEnvironment.Headless = false`), so a window opening is expected behavior, not a fault.

Reconcile discovered against `journeys[].tests`. Because journeys are `[Fact]`-only (step 4), discovered cases and declared methods are the same number — if they disagree, you have an unclaimed method, a `void` signature the gate could not see, or a `[Theory]` that should not exist. Any **skipped** test means the environment was not available — treat it as a halt, never a pass.

## 8. Repair — the loop, and its limit

Up to **three** repair rounds. Each round: classify every failure, fix only what you are permitted to fix, then re-run **only** the failing tests via `--filter "FullyQualifiedName~{Method}"`. Never re-run the whole suite to see if a flake goes away.

Classify each failure before touching anything:

| Class | Signal | Action |
|---|---|---|
| **Environment** | every test fails at boot; app never served; `.e2e-env` missing; auth state rejected | Repair the environment (re-run `start-e2e-env.ps1`, clear `.auth-states/`). Not a test defect and not a product defect. |
| **Locator / timing** | the app did the right thing — visible in the trace — but the test looked in the wrong place, or asserted before the UI settled | Fix the locator or the wait **in test code**. This is the only class you may fix by editing a test. |
| **Missing hook** | locator names a `data-testid` `src/` does not define | Halt. Report the hook. Never add it yourself. |
| **Product defect** | the app's observed behavior contradicts the spec sentence the AC states | **Record it as a failure and stop repairing that test.** Do not adjust the assertion to match what the app did. |

**Unclear locator versus product defect: classify as product defect.** Never adapt expected behavior to observed failure.

Use the Playwright trace saved on failure under `tests/TheShop.E2E.Tests/bin/**/playwright-traces/{TestDisplayName}.zip` as evidence for the classification — it carries screenshots and DOM snapshots, and it is what makes "the app did the right thing" a claim you can substantiate rather than assume.

After three rounds, stop and report whatever is still red. A test still failing after three rounds is a finding, not a puzzle to keep grinding.

**If you edited any test in this step, re-run the `e2e` gate once before you report:**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 e2e -Feature {arguments}
```

A repair can introduce exactly what step 5 existed to catch — a new `GetByTestId("…")` literal that resolves to nothing, or a renamed method that no longer matches its AC. Step 5 ran before those edits existed, so without this re-run they reach the verdict unchecked. If it now fails, the verdict is `🔴 NOT VERIFIED` with gate `🔴 e2e gate red after repair`, whatever the test results said.

If post-repair gate fails, fix or report failure. Never report VERIFIED over an invalidated gate.

## 9. Tear down

The collection fixture kills the app process tree on disposal. Confirm port 5218 is free and no orphaned `dotnet run` survives:

```powershell
Get-NetTCPConnection -LocalPort 5218 -State Listen -ErrorAction SilentlyContinue |
  ForEach-Object { Get-Process -Id $_.OwningProcess }
```

Any process this returns is an orphan the fixture failed to reap — stop it, and note it in the report's Environment line.

Leave the Supabase stack **running** — the next run's `db reset` cleans it anyway, and stopping it costs the user their local environment. Say so in the report, and mention `tools/stop-e2e-env.ps1` as the explicit way down.

## 10. Persist the report and the ledger

Read `references/report.md`; fill exact template with actual date, Git SHA or `(unknown)`, metrics, classifications, and evidence. Overwrite `.specs/{arguments}/e2e-report.md`; final response must match byte-for-byte. Apply verdict/ledger rules below.

Verdict rules:

- `✅ VERIFIED` — the e2e gate is clean (including the step 8 re-run), every `e2e` AC passed, no skips, no repair round ended red, and every `unit` AC is corroborated by a passing unit manifest. `manual` ACs remain ⚠️ Unverified and are listed, but they do **not** block this verdict when the user has been told what they are.
  - "Corroborated by a *passing* manifest" is a real constraint, and the `manifest` gate cannot supply it — that gate validates structure, never results. The only evidence that those tests pass is the `4. Test` row you read in step 1. **So: if any AC is bucketed `unit` and the Test row is not `Passing`, `✅ VERIFIED` is unavailable** — the verdict is `🔴 NOT VERIFIED` with gate `🔴 unit ACs uncorroborated ({Test row state})`, however green the browser run was. Those ACs show as ⚠️ Unverified in the AC table, not ✅ Passed. When every AC is `e2e` or `manual`, the Test row constrains nothing and stays a warning.
- `🔴 NOT VERIFIED` — any failed AC, any skipped test, any missing hook, any environment failure, or any AC left unclassified.
- `⛔ Halted` — a static gate, the build, or the environment stopped the run before the journey executed. Say the journey **did not run**; never say tests failed.

Then update `.specs/{arguments}/status.md` — the **`5. Verify`** row (that name is what `ship-ready` scans; keep it even though this command is not `{{command:theshop-verify}}`):

Rows carry **five** columns — `| Step | State | Gate | Evidence | Date |`. Match the shape of the rows already in the file and fill the date cell with today's date (`YYYY-MM-DD`); leaving it `—` is a drift the ship gate cannot catch but a human reader will.

| Outcome | State | Gate | Evidence | Date |
|---|---|---|---|---|
| VERIFIED | `Verified` | `✅ e2e gate + journey pass` | `{n}/{n} e2e ACs ✅ · {n} unit · {n} manual — see [e2e-report.md](./e2e-report.md)` | today |
| NOT VERIFIED | `Pending` | `🔴 {n} AC failed \| missing hooks \| env}` | one line + report link | today |
| Halted | `Pending` | `🔴 {e2e gate \| build \| environment}` | one line + report link | today |
| Backend-only | `Skipped` | `⏭️ backend-only` | `no Web surface to drive` | today |

Refresh `Last updated` and point `Next step` at `{{command:theshop-review}} {arguments}`. If `status.md` is missing, read `.sdd/skills/theshop-spec/references/status-tracker.md`, create the tracker, then apply the update.

## 11. Final integrity check

Re-read the spec, the e2e-manifest, the report, and the ledger, then verify mechanically:

1. Every spec AC appears exactly once in the report's AC table, in spec order.
2. The report's bucket counts equal the manifest's.
3. The `e2e` row count equals the journey's discovered test count.
4. Every `unit` row's evidence is the FQN the unit manifest maps to that same AC.
5. The verdict agrees with the AC tally and with the Verify row's State and Gate.
6. The persisted report is byte-for-byte what you emit.

Fix any inconsistency before returning. If you cannot, set the verdict to `🔴 NOT VERIFIED`, mark the gate `🔴 report integrity`, and state the mismatch. Never report VERIFIED over inconsistent artifacts.

## Templates for the non-running outcomes

Before any backend-only skip or pre-run halt, read `references/non-running-reports.md`. Persist Template B or C to `.specs/{arguments}/e2e-report.md`, update Verify row per Step 10, and emit identical text. No surrounding prose. Never skip report persistence.
