---
name: theshop-test
description: "Write and run feature unit/component tests through separate writer/runner agents. Reconcile manifest and AC coverage. E2E belongs to /theshop-e2e."
argument-hint: "<feature-name>"
---

<!-- Generated from .sdd/skills/theshop-test/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/claude/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

Read `.sdd/contracts/test-proof.md` before classification or reporting. It defines Passed, Deferred, Failed, and Not Covered, including the stage-specific `Ready for E2E — deferred proof remains` verdict. Deferred ACs stay outside passed counts; supporting tests still must pass.


# /theshop-test

**Feature requested:** `$ARGUMENTS`

Delegate to test writer, then runner; combine reports. Never write tests, execute tests, read specs yourself, or edit code.

## Inputs

If `$ARGUMENTS` is empty (the user invoked `/theshop-test` with no feature name), stop and ask:

> "Please provide a feature name. Usage: `/theshop-test <feature-name>` — for example, `/theshop-test add-to-cart`. The feature name must match an existing spec at `.specs/{feature_name}/spec.md`."

Wait for the user's reply. Do nothing else.

If a feature name is present, proceed.

---

## Step 1 — Invoke `shop-test-writer`

Call the `shop-test-writer` sub-agent via the delegation capability, with `role: shop-test-writer`. The prompt to that agent should be:

> "Write test cases for the feature `$ARGUMENTS`. Read the spec at `.specs/$ARGUMENTS/spec.md` (your behavioral oracle) and the plan at `.specs/$ARGUMENTS/plan.md` (your structural map — it reveals the Infrastructure seams the spec hides) and produce runnable test files in the appropriate `tests/TheShop.*.Tests/` projects. Do not write an E2E journey — `/theshop-e2e` owns E2E entirely. Follow your standard protocol and end with the structured summary."

Wait for the sub-agent to fully complete its turn. Do not begin Step 2 in parallel, and do not pre-empt the sub-agent's output.

When the sub-agent returns, inspect its closing summary:

- **If it created test files** (the "Files created/modified" list is non-empty), continue to Step 1.5.
- **If it halted without writing tests** — for example, the spec was missing, ambiguous, or contradicted itself, or the agent reported unresolved open questions — **stop immediately**. Do not invoke Step 2. Skip to the "Step 1 halted" output template below.

---

## Step 1.5 — Manifest + compile gates (deterministic, between writer and runner)

Before runner, verify manifest then compile listed test projects. Each gate permits at most **one** writer retry.

### Gate A — manifest

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature $ARGUMENTS
```

The script checks what the runner would otherwise discover only after a full diagnostic pass: the manifest parses, `totalTests` equals the sum of the per-class counts, every listed file exists on disk and carries the `[Trait("Feature", "$ARGUMENTS")]` stamp, every AC id from the spec appears in `acceptanceCriteria` (and none are invented), and every mapped test name belongs to a listed class.

- **Exit 0** → proceed to Gate B.
- **Exit 1** → re-invoke `shop-test-writer` **once**, quoting the gate's violation list verbatim with the instruction: "Your manifest/test handoff failed the deterministic gate. Fix exactly these violations and re-emit the structured summary." Then re-run the gate. If it fails a second time, **stop** — report with Template B, quoting the gate output as the halt reason. Do not hand a broken manifest to the runner; its reconciliation would fail anyway, just more expensively.

### Gate B — compile

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 compile -Feature $ARGUMENTS
```

Build every manifested test project and referenced production layer. Compiler errors carry `[tests]` or `[src]` tags by file location. Gate builds only; never executes tests.

Route by the tags in the gate's output:

- **Exit 0** → refresh the knowledge graph so the new test files land in it — `graphify update .` (AST-only; writes only under `graphify-out/`; skip silently if `graphify` or `graphify-out/graph.json` is unavailable) — then proceed to Step 2.
- **Exit 1, all errors tagged `[tests]`** → re-invoke `shop-test-writer` **once**, quoting the error list verbatim with the instruction: "Your test files do not compile. Fix exactly these compiler errors. You may glance at production code to align type/method names and signatures so the tests compile — never to change what a test expects. If an error is caused by a production type or member that does not exist yet (the feature is unimplemented), do not weaken, comment out, or delete the test — leave it and report the missing symbol in your summary." Then re-run Gate B.
  - If the writer reports the errors come from **missing production symbols** (the feature hasn't been implemented yet), **stop** and report with Template C, noting explicitly that the tests are written and awaiting implementation — an expected pre-implementation state, not a writer defect. Point the user at `/theshop-implement $ARGUMENTS`.
  - If Gate B fails a second time on `[tests]` errors that were the writer's to fix, **stop** — report with Template C, quoting the gate output.
- **Exit 1, any error tagged `[src]`** → do **not** re-invoke the writer — it is forbidden from touching production code and cannot fix this. **Stop** and report with Template C: production code does not compile, so the feature's tests are written but blocked. Name the broken project/file(s) from the gate output.


---

## Step 2 — Invoke `shop-test-runner`

Only reachable if Step 1 produced test files.

Call the `shop-test-runner` sub-agent via the delegation capability, with `role: shop-test-runner`. The prompt to that agent should be:

> "Run the test cases for the feature `$ARGUMENTS`. Follow your standard protocol: read the manifest at `.specs/$ARGUMENTS/test-manifest.json`, run targeted by feature trait (`--filter \"Feature=$ARGUMENTS\"`), reconcile the discovered test count against the manifest's `totalTests`, evaluate each acceptance criterion against the manifest's `acceptanceCriteria` mapping, analyze across the five layers, and deliver the six-section structured report (including the Acceptance criteria table) with a final verdict. Treat any reconciliation mismatch, any failing acceptance criterion, or any uncovered acceptance criterion as 🔴 NOT READY."

Wait for it to fully complete.

---

## Handoff rules (enforce strictly)

1. **Do not start Step 2 until Step 1 is fully complete.** If the writer is still working, wait. No parallel invocation.
2. **Do not fix any code regardless of what the test results show.** Your job ends at delivering the combined summary — plus updating the feature's tracking artifact `.specs/$ARGUMENTS/status.md` and persisting the combined report to `.specs/$ARGUMENTS/test-report.md` (see below), neither of which is code. The user is the one who acts on it. If they ask you to fix something inside this command run, tell them the slash command is orchestration-only and they can request fixes in a follow-up message.
3. **Do not run anything outside `tests/`.** The runner agent handles all test execution; you never invoke `dotnet test` yourself. The commands you do run are the Step 1.5 gates (`check-sdd-gates.ps1 manifest` and `compile`) — the first is a read-only artifact check, the second builds the manifest's test projects but never executes a test — plus the post-Gate-B `graphify update .`, which writes only under `graphify-out/` (a knowledge-graph refresh, not code), plus a read-only `git rev-parse --short HEAD` to stamp the persisted report.
4. **If `shop-test-writer` could not write the test files, stop and report the reason.** Do not proceed to Step 2 under any circumstance — not even "to see what's already there".
5. **This command never writes or runs E2E journeys.** `/theshop-e2e` owns E2E entirely — writing and running the journey in its own single context. If the writer's summary mentions an E2E journey, that is stale behavior from before the split; do not include it in any count, filter, or report section.

---

## Update the status tracker

After you settle the verdict (Template A only), update `.specs/$ARGUMENTS/status.md`: set the **Test** row to State `Passing` when the verdict is ✅ Ready, or `Failing` when it is ❌ Needs fixes; Gate `✅ manifest + reconciliation pass` or `🔴 {which gate failed}`; Evidence one line of the run's numbers **plus a link to the persisted report** (e.g. `194/194 reconciled · 12/12 ACs ✅ — see [test-report.md](./test-report.md)` or `reconciliation mismatch 180/194 — see [test-report.md](./test-report.md)`); today's date. Refresh **Last updated**; point **Next step** at `/theshop-e2e $ARGUMENTS` (Passing) or back at the fix the runner named (Failing). On Template B (writer halted) leave the tracker untouched; on Template C (build failed) set **Test** to `Failing` with Gate `🔴 build gate` and the failing project as Evidence (append `— see [test-report.md](./test-report.md)`). Create `status.md` from the `theshop-spec` template first if it's missing.

## Persist the test report

Persist Template A (tests ran) or Template C (build failed) to `.specs/$ARGUMENTS/test-report.md`. Retain failures, reconciliation, and AC outcomes. Manifest defines expected tests; report records observed run.

Rules:

- **One file, overwrite.** Write (never append) the full combined summary to `.specs/$ARGUMENTS/test-report.md` — the *exact same* Template A or Template C content you emit as Final output, stamp included. Each run replaces the last; git carries the history. Never accumulate dated report files and never create a `test/` subfolder.
- **The stamp is part of the template.** Templates A and C already carry the two stamp lines under the heading. Fill `{date}` with today's date, `{short-sha}` from `git rev-parse --short HEAD` (write `(unknown)` if that command fails — e.g. not a git checkout), and `{verdict}` with the settled verdict. The persisted file and your Final output must be identical.
- **Template B (writer halted): do not write the report.** Nothing was produced, so there is nothing to persist — this mirrors leaving `status.md` untouched. The in-session halt reason is enough.
- Write the file as part of the same step that updates `status.md`, before emitting Final output. This is the one file you write outside `status.md`; it is a record, not code, so it does not violate the orchestration-only handoff rules.

## Outputs and completion evidence

Read `references/reports.md` before any final response. Select A for completed run, B for writer halt or repeated manifest-gate failure, C for compile/build failure. Preserve exact template, persistence rules, and verdict rules below. No surrounding prose.

## Verdict rules (be strict)

Apply `.sdd/contracts/test-proof.md`. Clean unit/component run requires exact reconciliation, no failures/skips/warnings, and every AC Passed or validly Deferred. Uncovered unit ACs and failed supporting tests block Test.

With deferrals, report `Ready for E2E — deferred proof remains`; Test row may be Passing. List deferred IDs/reasons separately, never as passed or feature-complete. Without deferrals, use existing all-pass verdict.

Build failure uses Template C, says tests did not run, and blocks Test. Missing production symbols route to Implement; never weaken tests to compile.
