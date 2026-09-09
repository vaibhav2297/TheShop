---
name: theshop-test-merged
description: "Write, compile, and run spec-driven feature unit/component tests in one context; produce manifest, report, and ledger. No subagents or E2E."
argument-hint: "<feature-name>"
---

<!-- Generated from .sdd/skills/theshop-test-merged/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/claude/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

Read `.sdd/contracts/test-proof.md` before classification or reporting. It defines Passed, Deferred, Failed, and Not Covered, including the stage-specific `Ready for E2E — deferred proof remains` verdict. Deferred ACs stay outside passed counts; supporting tests still must pass.


# /theshop-test-merged

**Feature requested:** `$ARGUMENTS`

Own feature testing in one context. Read sources, write tests, validate, compile, run, analyze, and persist.

Never delegate. Read spec and plan once; retain inventory without repeating their contents in updates.

## Scope and authority

You may:

- Read the feature artifacts, relevant source signatures, existing tests, test project files, and repository guidance.
- Create or edit feature-owned tests under `tests/TheShop.*.Tests/`.
- Overwrite `.specs/$ARGUMENTS/test-manifest.json` with the current complete test inventory.
- Overwrite `.specs/$ARGUMENTS/test-report.md` and update the Test row in `.specs/$ARGUMENTS/status.md` as specified below.
- Run the deterministic gates, builds, targeted tests, focused diagnostics, `graphify update .`, and `git rev-parse --short HEAD`.

You must not:

- Edit production code, shared E2E harness files, specs, plans, project files, package references, or dependencies.
- Derive expected behavior from production code. Use production code only to align names, signatures, and existing seams.
- Weaken, delete, skip, or comment out a valid test to obtain a green result.
- Run the full test suite.
- Write, edit, or run E2E journeys. `/theshop-e2e` owns E2E entirely — writing and running the journey in its own single context.
- Install packages or modify anything outside the paths explicitly authorized above.

If the user asks for a production fix during this command, finish the report and direct them to a follow-up workflow.

## 1. Validate input and read the sources of truth

If `$ARGUMENTS` is empty, contains a path separator, or contains `..`, stop and ask for a feature folder name. Do not write anything.

Read `.specs/$ARGUMENTS/spec.md`. It is required and is the behavioral oracle:

- Assertions and expected outcomes come from the spec.
- Every functional requirement, specified validation rule, edge case, and acceptance criterion must be represented.
- If the spec is missing, materially ambiguous, self-contradictory, or has unresolved questions that affect expected behavior, stop before writing and identify the exact blocker.

Read `.specs/$ARGUMENTS/plan.md` when present. It is the structural map:

- Use it to identify layers, types, repositories, mappers, schema/RLS contracts, external error mappings, and other technical seams.
- If plan and spec conflict on behavior, stop; the spec wins but the disagreement must be resolved before tests are written.
- If the plan is missing, proceed with behavior derivable from the spec, do not invent structural contracts, and carry a prominent degraded-coverage warning into the final report.

Use `graphify query` first for codebase discovery when `graphify-out/graph.json` exists. Otherwise use targeted `Glob`/`Grep`/`Read`. Inspect existing nearby tests and project files for real conventions and available helpers. Read local architecture reference through [§9](#9-architectural-context--baked-in-do-not-look-it-up) before writing/diagnosis. Read required constitution rules and Tests checklist; load other references only when applicable.

Before editing, form a compact internal inventory of:

- Spec requirements and AC ids.
- Required Domain, Application, Infrastructure, and Web seams from the plan.
- Happy-path, validation, boundary/edge, auth/permission, and structural cases.
- Existing files to extend versus new files to create.

Do not emit this inventory as a separate report.

## 2. Write the feature tests

Follow `CLAUDE.md`, the baked-in contracts in [§9](#9-architectural-context--baked-in-do-not-look-it-up), and the conventions already demonstrated by the matching test projects. Use the packages already referenced by each test project's `.csproj`; do not add packages.

Test every layer named by the plan:

| Plan seam | Project and test style |
|---|---|
| Domain | `TheShop.Domain.Tests` — pure unit tests |
| Application | `TheShop.Application.Tests` — handlers, validators, and contracts with substitutes |
| Infrastructure | `TheShop.Infrastructure.Tests` — mappings, persistence, schema/RLS, and external error translation |
| Web | `TheShop.Web.Tests` — bUnit component/page behavior and auth presentation |

For every ordinary unit, integration, or component test:

- Name it `{MethodOrFeature}_{Scenario}_{ExpectedOutcome}`.
- Apply `[Trait("Feature", "$ARGUMENTS")]` at the method level to every `[Fact]` and `[Theory]`; never apply it to an ordinary test class.
- Take expectations from the spec and structural details from the plan.
- Cover every applicable category in the mandatory-coverage list below.
- Add a short class XML documentation comment that references `.specs/$ARGUMENTS/spec.md`.
- Preserve other features' methods when extending a shared test class.
- Keep tests deterministic and runnable; reuse existing builders and fixtures when appropriate.

### Mandatory coverage categories

Every feature gets tests in these categories — not only the cases the spec happens to enumerate. The spec sets the *expected outcome*; this list sets the *floor on scenarios*.

1. **Happy path** — at least one test per functional requirement, correct input producing the correct outcome.
2. **Validation** — for every input the feature accepts: null, empty string, whitespace, negative number, zero, out-of-range value, malformed identifier. Each must fail gracefully with the right resource key, never an unhandled exception.
3. **Edge cases** — boundary values (`0`, `1`, max, max+1), empty collections, duplicate actions, concurrent/repeated state changes, and missing related entities. Every edge case the spec names must have a test, plus the boundaries it implies.
4. **Auth guard** — applies to any feature reachable from an authenticated page or depending on `ICurrentUserService`:
   - **Admin features** (any `/admin/*` page or admin-only handler): one test that unauthenticated requests are blocked **and** one test that authenticated non-admin users are blocked. Both are required — the second is the one that is usually missed.
   - **Authenticated-user features:** a test that unauthenticated requests are blocked.
   - **Public features:** skip this category entirely; do not invent auth the spec does not imply.
5. **Structural / Infrastructure** — whenever the plan declares an Infrastructure seam. Contract from the plan, intent from the spec:
   - **Repository round-trip** — an inserted record reads back as an equal Domain entity, using the plan's column ↔ field mapping.
   - **Constraint backstop** — storage-level rules the plan specifies (e.g. a `UNIQUE(lower(email))` index rejecting a duplicate).
   - **Error translation** — each external-error → resource-key mapping in the plan (e.g. Supabase `otp_expired` → `Auth_CodeExpired`) gets a test that the adapter returns the right key.
   - Take columns, indexes, and policies from the plan. Never invent them; if the plan omits a contract you would need, flag it in the report instead of guessing.

Anything the spec or plan additionally calls out gets tests on top of these.

If a required production symbol is absent, write the test the confirmed spec requires. Do not invent a substitute production contract merely to compile; let the compile gate route the feature to implementation.

## 3. Write the manifest

Overwrite `.specs/$ARGUMENTS/test-manifest.json` with the complete current inventory of this feature's tests:

```json
{
  "feature": "$ARGUMENTS",
  "trait": "$ARGUMENTS",
  "writtenAt": "YYYY-MM-DD",
  "totalTests": 0,
  "classes": [
    { "fqn": "Namespace.ClassTests", "file": "tests/Project/Path/ClassTests.cs", "tests": 0 }
  ],
  "acceptanceCriteria": [
    { "id": "AC-1", "tests": ["Namespace.ClassTests.Method_Name"] }
  ]
}
```

Manifest rules:

- List every test class created or extended for this feature and no unrelated class.
- Count only cases carrying this feature's trait. Count one `[Fact]` as one case and each data row of a `[Theory]` as one discovered case.
- Make `totalTests` equal the sum of class counts.
- List every spec AC in order. Map it to fully-qualified test method names; use `tests: []` when uncovered rather than hiding the gap.
- For a theory, map the AC to its method name; every discovered data row for that method must pass.

## 4. Run deterministic handoff gates

Run the manifest gate:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature $ARGUMENTS
```

If it fails, fix only the reported test/manifest violations and run it once more. If it fails again, stop with the Artifact-gate outcome below. Do not describe this state as "no tests were produced."

Run the compile gate:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 compile -Feature $ARGUMENTS
```

Route compile failures:

- Only `[tests]` errors caused by test code: fix exactly those errors in this context and run the compile gate once more.
- Missing production symbols required by the spec: do not weaken the tests; stop with the Build-blocked outcome and direct the user to `/theshop-implement $ARGUMENTS`.
- Any `[src]` error: do not edit production; stop with the Build-blocked outcome and name the affected project/file.
- Remaining `[tests]` errors after one correction, or an unparseable build failure: stop with the Build-blocked outcome.

After the compile gate succeeds, run `graphify update .` only when both `graphify` and `graphify-out/graph.json` are available. Ignore their absence.

## 5. Run only the manifested test projects

Derive the unique `tests/<Project>/` roots from `manifest.classes[].file`, locate each root's `.csproj`, and run each project once:

```powershell
dotnet test <project.csproj> --filter "Feature=$ARGUMENTS" --no-build --logger "console;verbosity=minimal" --nologo
```

Do not run at the solution root. Aggregate each project's passed, failed, and skipped counts. The aggregate discovered count is `passed + failed + skipped`.

If output is insufficient to diagnose a failure, rerun only the failing project or test with normal/detailed verbosity. Do not rerun clean projects.

Reconcile aggregate discovered cases against `manifest.totalTests`:

- Equal: continue.
- Unequal: inspect only the listed files for missing/mistyped traits, theory-row counts, stale manifest entries, or unrelated tagged tests. Repair a clearly mechanical test/manifest defect once, rerun the manifest and compile gates as needed, then rerun only affected projects.
- If reconciliation still differs or the cause is unclear, keep the mismatch and return `Needs fixes`. Never broaden execution to whole shared classes, because that can run other features' tests.

## 6. Analyze results

Determine each AC status from the manifest and actual results:

- `Passed`: one or more mapped methods ran, and every discovered case for those methods passed.
- `Failed`: a mapped method failed, errored, or did not run.
- `Not Covered`: unit-proof mapping is empty. `Deferred`: justified e2e/manual proof, with every mapped supporting test passed.

When every discovered test passed and reconciliation matched, all non-empty AC mappings may be treated as passed. When anything failed or did not run, obtain enough focused output to map the affected method accurately.

Scan only manifest-listed files for skipped tests, vacuous tests, improper async signatures, timing sleeps/delays, swallowed exceptions, nondeterministic time, unfinished markers, placeholder names, and assertions that verify only success without required state/side effects. Report file and line for every confirmed warning; do not flag harmless matches without inspection.

For each failure, record:

- Fully-qualified test name and layer.
- Failure classification: assertion, exception, setup, mock verification, build, or environment.
- Exact concise symptom.
- Evidence-based root-cause hypothesis, explicitly labelled a hypothesis when not proven.
- A concrete recommended file/class/method to inspect or change; never implement the production fix.

Verdict rules are strict:

- `✅ Ready`: exact reconciliation, all tests pass, no skips, every AC Passed or validly Deferred, and no warning. With deferrals, use `Ready for E2E — deferred proof remains`; report exact IDs separately.
- `❌ Needs fixes`: any failure, skip, warning, mismatch, failed AC, or uncovered AC.
- `⛔ Blocked`: tests could not be authored because the input/spec was missing or unresolved.
- A compile failure is `Needs fixes — build failed`, and must say tests did not run rather than tests failed.

## 7. Persist the canonical report and status

Obtain today's date and `git rev-parse --short HEAD`; use `(unknown)` if the SHA cannot be read.

### Outcome-specific report

Read `references/reports.md` before final output. Pre-write halt: emit halt template only; never write report/ledger. Any run that wrote tests and reached a gate: overwrite `.specs/$ARGUMENTS/test-report.md` with full report and emit identical text. Preserve actual metrics, exact errors, AC rows, and explicit “tests did not run” when blocked. Then update ledger below.

Update `.specs/$ARGUMENTS/status.md` after writing the report:

- Ready: Test State `Passing`; Gate `✅ manifest + reconciliation pass`; evidence `{discovered}/{expected} reconciled · {passedAC}/{totalAC} ACs ✅ — see [test-report.md](./test-report.md)`; Next step `/theshop-e2e $ARGUMENTS`.
- Test failure/mismatch/warning/uncovered AC: State `Failing`; Gate `🔴 {test failures | reconciliation | warnings | AC coverage}`; concise evidence plus report link; Next step names the required correction and rerun.
- Artifact gate failure: State `Failing`; Gate `🔴 manifest gate`; evidence links the report; Next step fixes the listed violations.
- Build failure: State `Failing`; Gate `🔴 build gate`; evidence names the failing project/symbol and links the report; Next step fixes production/build or runs `/theshop-implement $ARGUMENTS` for missing feature symbols.

Refresh `Last updated`. If `status.md` is missing, read `.claude/skills/theshop-spec/references/status-tracker.md`, create the standard tracker, then apply the Test update.

## 8. Final integrity check

Before returning, re-read the spec, manifest, report, and status tracker and mechanically verify:

1. Manifest class counts sum to `totalTests`.
2. Manifest AC ids exactly equal the spec AC ids in order.
3. Report expected count equals manifest `totalTests`.
4. For completed runs, report discovered count and status evidence agree.
5. Report AC row/count totals agree with the manifest and spec.
6. Report verdict agrees with the status Test state and gate.
7. The persisted report is byte-for-byte the same content you will emit.

Correct a writing/count inconsistency before returning. If it cannot be corrected from available evidence, set the verdict and Test state to `Needs fixes`/`Failing`, mark `🔴 report integrity`, and state the mismatch explicitly. Never report Ready with inconsistent artifacts.

Emit the canonical report with no prose before or after it.

---

## 9. Architectural context — baked in, do not look it up

Read `references/architecture.md` before Step 2 writing and Step 6 diagnosis. It contains this workflow's original architecture, tooling, error contracts, seams, and failure guidance. Read required constitution rules and Tests checklist; load other references only when applicable. Reuse loaded context; reread when changed or uncertain.
