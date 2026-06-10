---
description: Write and run tests for a specific feature. Invokes shop-test-writer to generate test cases from the spec, then shop-test-runner to execute them, and produces a combined verdict.
argument-hint: <feature-name>
---

# /theshop.test

**Feature requested:** `$ARGUMENTS`

You are the orchestrator for a two-step test workflow on **The Shop** project. You do not write tests, run tests, or read specs yourself — you delegate to two specialist sub-agents and then combine their reports. You never edit code.

## Input validation

If `$ARGUMENTS` is empty (the user invoked `/theshop.test` with no feature name), stop and ask:

> "Please provide a feature name. Usage: `/theshop.test <feature-name>` — for example, `/theshop.test add-to-cart`. The feature name must match an existing spec at `.specs/{feature_name}/spec.md`."

Wait for the user's reply. Do nothing else.

If a feature name is present, proceed.

---

## Step 1 — Invoke `shop-test-writer`

Call the `shop-test-writer` sub-agent via the Task tool, with `subagent_type: shop-test-writer`. The prompt to that agent should be:

> "Write test cases for the feature `$ARGUMENTS`. Read the spec at `.specs/$ARGUMENTS/spec.md` (your behavioral oracle) and the plan at `.specs/$ARGUMENTS/plan.md` (your structural map — it reveals the Infrastructure seams the spec hides) and produce runnable test files in the appropriate `tests/TheShop.*.Tests/` projects. Follow your standard protocol and end with the structured summary."

Wait for the sub-agent to fully complete its turn. Do not begin Step 2 in parallel, and do not pre-empt the sub-agent's output.

When the sub-agent returns, inspect its closing summary:

- **If it created test files** (the "Files created/modified" list is non-empty), continue to Step 2.
- **If it halted without writing tests** — for example, the spec was missing, ambiguous, or contradicted itself, or the agent reported unresolved open questions — **stop immediately**. Do not invoke Step 2. Skip to the "Step 1 halted" output template below.

---

## Step 2 — Invoke `shop-test-runner`

Only reachable if Step 1 produced test files.

Call the `shop-test-runner` sub-agent via the Task tool, with `subagent_type: shop-test-runner`. The prompt to that agent should be:

> "Run the test cases for the feature `$ARGUMENTS`. Follow your standard protocol: read the manifest at `.specs/$ARGUMENTS/test-manifest.json`, run targeted by feature trait (`--filter \"Feature=$ARGUMENTS\"`), reconcile the discovered test count against the manifest's `totalTests`, evaluate each acceptance criterion against the manifest's `acceptanceCriteria` mapping, analyze across the five layers, and deliver the six-section structured report (including the Acceptance criteria table) with a final verdict. Treat any reconciliation mismatch, any failing acceptance criterion, or any uncovered acceptance criterion as 🔴 NOT READY."

Wait for it to fully complete.

---

## Handoff rules (enforce strictly)

1. **Do not start Step 2 until Step 1 is fully complete.** If the writer is still working, wait. No parallel invocation.
2. **Do not fix any code regardless of what the test results show.** Your job ends at delivering the combined summary — plus updating the feature's tracking artifact `.specs/$ARGUMENTS/status.md` (see below), which is not code. The user is the one who acts on it. If they ask you to fix something inside this command run, tell them the slash command is orchestration-only and they can request fixes in a follow-up message.
3. **Do not run anything outside `tests/`.** The runner agent handles all execution; you don't invoke `dotnet`, `bash`, or any other command yourself.
4. **If `shop-test-writer` could not write the test files, stop and report the reason.** Do not proceed to Step 2 under any circumstance — not even "to see what's already there".

---

## Update the status tracker

After you settle the verdict (Template A only), update `.specs/$ARGUMENTS/status.md`: set the **Test** row to `Passing` when the verdict is ✅ Ready, or `Failing` when it is ❌ Needs fixes, with today's date; refresh **Last updated**; point **Next step** at `/theshop.verify $ARGUMENTS` (Passing) or back at the fix the runner named (Failing). On Template B (writer halted) leave the tracker untouched; on Template C (build failed) set **Test** to `Failing`. Create `status.md` from the `theshop.spec` template first if it's missing.

## Final output

After both agents complete (or after Step 1 halts), produce a combined summary in **exactly** one of the three templates below — Template A when the run completed, Template B when the writer halted, Template C when the runner's build gate failed (the solution didn't compile, so no tests ran). No extra prose before or after.

### Template A — Both steps ran

```markdown
# Test report — {feature_name}

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

{One sentence describing what the user needs to do next — typically "Resolve the issue reported above (e.g., create or fix the spec at `.specs/{feature_name}/spec.md`) and re-run `/theshop.test {feature_name}`."}
```

### Template C — Runner build gate failed

Use this when Step 1 produced tests but `shop-test-runner` reported a **build failure** — its Step 2 build gate tripped, the solution did not compile, and no tests ran. You'll recognize it by the runner returning the trimmed build-failure report: a `Build: 🔴 Failed` summary row and a `🔴 NOT READY — build failed` verdict, with no pass/fail metrics. Do **not** force this into Template A — there are no test counts to show.

```markdown
# Test report — {feature_name}

## Tests written (shop-test-writer)

{One-line list of files created, taken from the writer's summary.}

- `tests/TheShop.{Layer}.Tests/{path}/{File}.cs` — {N} tests
- ...

## Tests run (shop-test-runner)

**Status:** 🔴 Build failed — the solution did not compile, so no tests ran.

**Build errors (from the runner):**
- `{project}` — `{file}:{line}` — `error CSxxxx`: {message}
- ...

{If the runner flagged that the break is in a project NOT part of this feature's test set, surface that here — the feature's own tests are blocked by an unrelated compile error.}

## Acceptance criteria

Not verified — nothing compiled, so no acceptance criterion could be exercised. All ACs remain ⚠️ unverified until the build is green.

## Verdict

**❌ Needs fixes — build failed**

The code does not compile, so the tests for `{feature_name}` never ran and no acceptance criterion is verified. Fix the build error(s) listed above and re-run `/theshop.test {feature_name}`. This is a "tests did not execute" state, not a test failure.
```

---

## Verdict rules (be strict)

The two-outcome verdict is intentional and the boundary is sharp:

- **"Ready for code review"** is reserved for a fully clean run — the discovered test count reconciled exactly against the manifest (every test that should have run did run, nothing extra), every one passed, **every acceptance criterion is ✅ Passed**, nothing was skipped, no sneaky-issue warnings were flagged.
- **"Needs fixes"** covers everything else: any reconciliation mismatch (fewer or more tests ran than the manifest promised), any failure, any build error, any skipped test, any ❌ Failed acceptance criterion, any ⚠️ Not Covered acceptance criterion, any warning the runner surfaced (e.g., `Thread.Sleep` in tests, vacuous tests, `[Fact(Skip)]`). A skipped test is not a passed test, a mismatch means coverage is unverified, an uncovered AC means the definition of done is unverified, and a warning means the green light is misleading.

A reconciliation mismatch is a hard blocker on its own — even if every test that ran passed, you do **not** call it "Ready", because some of the feature's tests may never have executed. The same is true of the acceptance criteria: passing tests are necessary but not sufficient — if any AC is ❌ Failed or ⚠️ Not Covered, it is "Needs fixes", because the feature's definition of done has not been met.

If you're tempted to call something "Ready" with a footnote — don't. That's what "Needs fixes" is for.

A **build failure** reported by the runner is a special case of "Needs fixes": render it with **Template C**, not Template A, because the solution never compiled and there are no pass/fail metrics to report — the tests did not execute. It is still "Needs fixes," just with a build-error breakdown in place of the metrics table.
