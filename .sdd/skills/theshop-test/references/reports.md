## Final output

Read `.sdd/contracts/test-proof.md` before classification or reporting. It defines Passed, Deferred, Failed, and Not Covered, including the stage-specific `Ready for E2E — deferred proof remains` verdict. Deferred ACs stay outside passed counts; supporting tests still must pass.


After both agents complete (or after Step 1 halts, or a Step 1.5 gate stops the pipeline), produce a combined summary in **exactly** one of the three templates below — Template A when the run completed, Template B when the writer halted (or Gate A failed twice), Template C when a build failed — whether at the Step 1.5 compile gate or the runner's own build gate — so no tests ran. No extra prose before or after.

### Template A — Both steps ran

```markdown
# Test report — {feature_name}

_Run: {date} · commit `{short-sha}` · verdict {✅ Ready / ❌ Needs fixes}_
_Snapshot of one run — regenerate with `{{command:theshop-test}} {feature_name}`._

## Tests written (shop-test-writer)

{One-line list of files created, taken from the writer's summary.}

- `tests/TheShop.{Layer}.Tests/{path}/{File}.cs` — {N} tests
- ...

**Coverage by category:** Happy path: {✅/⚠️} · Validation: {✅/⚠️} · Edge cases: {✅/⚠️} · Auth guard: {✅/⚠️/N-A}

## Tests run (shop-test-runner)

| Metric | Value |
|---|---|
| Expected (manifest) | {N} |
| Discovered/run | {N} |
| Reconciliation | {✅ matched / 🔴 mismatch — {discovered} vs {expected}} |
| Passed | {N} |
| Failed | {N} |
| Skipped | {N} |
| Pass rate | {%} |
| Runner status | {🟢 Green / 🟡 Yellow / 🔴 Red} |

{If reconciliation did not match, add a single line naming the cause: 🔴 Reconciliation mismatch — {missing classes not run, or extra classes that ran}. This alone forces the "Needs fixes" verdict.}

{If the runner reported failures, list each one in a single line: ❌ `{TestFullName}` — {one-line cause}. If none, write "No failures."}

{If the runner flagged warnings, list each in a single line: ⚠️ {one-line description}. If none, write "No warnings flagged."}

## Acceptance criteria

{One row per AC from the runner's Acceptance criteria section, referenced by id only — the spec owns the wording.}

| AC | Status |
|---|---|
| AC-1 | {✅ Passed / ❌ Failed / ⚠️ Not Covered} |
| AC-2 | {✅ Passed / ❌ Failed / ⚠️ Not Covered} |

**AC status:** {N} passed · {N} failed · {N} not covered

{If the runner could not verify ACs — no `acceptanceCriteria` in the manifest and no `// AC → Test mapping` footer — replace the table with: "⚠️ AC verification not performed — no AC oracle available." and treat it as a blocker for the "Ready" verdict.}

## Verdict

{One of:}
- **✅ Ready for code review — all tests pass and all acceptance criteria pass**  *(use only when runner status is 🟢 Green: 100% pass, no skipped tests, no warnings, and every AC ✅ Passed)*
- **❌ Needs fixes**  *(use for 🟡 Yellow or 🔴 Red — including clean failures, skipped tests, warning flags, any ❌ Failed AC, or any ⚠️ Not Covered AC)*

{One sentence justifying the verdict. "Ready" requires BOTH every test passing AND every acceptance criterion passing. For "Needs fixes", point at the specific blocker(s) — name failing/uncovered AC ids where relevant.}

---

*Full per-failure breakdown and recommendations are in the shop-test-runner output above. The user should refer to that for fix guidance.*
```

### Template B — Step 1 halted

```markdown
# Test report — {feature_name}

## Tests written (shop-test-writer)

**Status:** ⛔ Halted — no test files were produced.

**Reason given by shop-test-writer:**

> {Verbatim quote of the writer's stop reason — e.g., "No spec found at .specs/{feature_name}/spec.md", or "Spec section 'Acceptance Criteria' is empty", or "Open questions remain — agent asked the user for clarification before writing tests".}

## Tests run (shop-test-runner)

**Skipped** — per the handoff rules, the runner is not invoked when no tests exist.

## Verdict

**⛔ Blocked — tests could not be written**

{One sentence describing what the user needs to do next — typically "Resolve the issue reported above (e.g., create or fix the spec at `.specs/{feature_name}/spec.md`) and re-run `{{command:theshop-test}} {feature_name}`."}
```

### Template C — Build gate failed (Step 1.5 compile gate or runner)

Use this when Step 1 produced tests but a **build failure** stopped the pipeline. Two sources:

- **The Step 1.5 compile gate** tripped and could not be cleared — `[src]` errors (production code broken), missing production symbols (feature not yet implemented), or `[tests]` errors that survived the single writer retry. The runner was never invoked; say so in the "Tests run" section and use the gate's tagged error list as the build errors.
- **`shop-test-runner`'s own Step 2 build gate** tripped — recognizable by the runner returning the trimmed build-failure report: a `Build: 🔴 Failed` summary row and a `🔴 NOT READY — build failed` verdict, with no pass/fail metrics.

Either way, the solution did not compile and no tests ran. Do **not** force this into Template A — there are no test counts to show.

```markdown
# Test report — {feature_name}

_Run: {date} · commit `{short-sha}` · verdict ❌ Needs fixes_
_Snapshot of one run — regenerate with `{{command:theshop-test}} {feature_name}`._

## Tests written (shop-test-writer)

{One-line list of files created, taken from the writer's summary.}

- `tests/TheShop.{Layer}.Tests/{path}/{File}.cs` — {N} tests
- ...

## Tests run (shop-test-runner)

**Status:** 🔴 Build failed — the solution did not compile, so no tests ran. {If the Step 1.5 compile gate stopped the pipeline, add: "The runner was not invoked — the compile gate caught this first."}

**Build errors (from the compile gate or the runner):**
- `{project}` — `{file}:{line}` — `error CSxxxx`: {message}
- ...

{If the break is in a project/file NOT part of this feature's test set (the gate's `[src]` tag, or the runner's flag), surface that here — the feature's own tests are blocked by an unrelated compile error.}

{If the writer reported the errors come from production symbols that don't exist yet, say instead: "The tests are written and compile-blocked only because the feature is not implemented yet — run `{{command:theshop-implement}} {feature_name}` first, then re-run `{{command:theshop-test}} {feature_name}`."}

## Acceptance criteria

Not verified — nothing compiled, so no acceptance criterion could be exercised. All ACs remain ⚠️ unverified until the build is green.

## Verdict

**❌ Needs fixes — build failed**

The code does not compile, so the tests for `{feature_name}` never ran and no acceptance criterion is verified. Fix the build error(s) listed above and re-run `{{command:theshop-test}} {feature_name}`. This is a "tests did not execute" state, not a test failure.
```

---
