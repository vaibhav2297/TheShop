---
description: Write and run tests for a specific feature. Invokes shop-test-writer to generate test cases from the spec, then shop-test-runner to execute them, and produces a combined verdict.
argument-hint: <feature-name>
---

# /shop-test-feature

**Feature requested:** `$ARGUMENTS`

You are the orchestrator for a two-step test workflow on **The Shop** project. You do not write tests, run tests, or read specs yourself — you delegate to two specialist sub-agents and then combine their reports. You never edit code.

## Input validation

If `$ARGUMENTS` is empty (the user invoked `/shop-test-feature` with no feature name), stop and ask:

> "Please provide a feature name. Usage: `/shop-test-feature <feature-name>` — for example, `/shop-test-feature add-to-cart`. The feature name must match an existing spec at `.claude/specs/{feature_name}.md`."

Wait for the user's reply. Do nothing else.

If a feature name is present, proceed.

---

## Step 1 — Invoke `shop-test-writer`

Call the `shop-test-writer` sub-agent via the Task tool, with `subagent_type: shop-test-writer`. The prompt to that agent should be:

> "Write test cases for the feature `$ARGUMENTS`. Read the spec at `.claude/specs/$ARGUMENTS.md` and produce runnable test files in the appropriate `tests/TheShop.*.Tests/` projects. Follow your standard protocol and end with the structured summary."

Wait for the sub-agent to fully complete its turn. Do not begin Step 2 in parallel, and do not pre-empt the sub-agent's output.

When the sub-agent returns, inspect its closing summary:

- **If it created test files** (the "Files created/modified" list is non-empty), continue to Step 2.
- **If it halted without writing tests** — for example, the spec was missing, ambiguous, or contradicted itself, or the agent reported unresolved open questions — **stop immediately**. Do not invoke Step 2. Skip to the "Step 1 halted" output template below.

---

## Step 2 — Invoke `shop-test-runner`

Only reachable if Step 1 produced test files.

Call the `shop-test-runner` sub-agent via the Task tool, with `subagent_type: shop-test-runner`. The prompt to that agent should be:

> "Run the test cases for the feature `$ARGUMENTS`. Follow your standard protocol: verify tests exist, run targeted, analyze across the four layers, and deliver the five-section structured report with a final verdict."

Wait for it to fully complete.

---

## Handoff rules (enforce strictly)

1. **Do not start Step 2 until Step 1 is fully complete.** If the writer is still working, wait. No parallel invocation.
2. **Do not fix any code regardless of what the test results show.** Your job ends at delivering the combined summary. The user is the one who acts on it. If they ask you to fix something inside this command run, tell them the slash command is orchestration-only and they can request fixes in a follow-up message.
3. **Do not run anything outside `tests/`.** The runner agent handles all execution; you don't invoke `dotnet`, `bash`, or any other command yourself.
4. **If `shop-test-writer` could not write the test files, stop and report the reason.** Do not proceed to Step 2 under any circumstance — not even "to see what's already there".

---

## Final output

After both agents complete (or after Step 1 halts), produce a combined summary in **exactly** one of the two templates below. No extra prose before or after.

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
| Tests run | {N} |
| Passed | {N} |
| Failed | {N} |
| Skipped | {N} |
| Pass rate | {%} |
| Runner status | {🟢 Green / 🟡 Yellow / 🔴 Red} |

{If the runner reported failures, list each one in a single line: ❌ `{TestFullName}` — {one-line cause}. If none, write "No failures."}

{If the runner flagged warnings, list each in a single line: ⚠️ {one-line description}. If none, write "No warnings flagged."}

## Verdict

{One of:}
- **✅ Ready for code review — all tests pass**  *(use only when runner status is 🟢 Green: 100% pass, no skipped tests, no warnings)*
- **❌ Needs fixes**  *(use for 🟡 Yellow or 🔴 Red — including clean failures, skipped tests, or warning flags)*

{One sentence justifying the verdict. For "Needs fixes", point at the specific blocker(s).}

---

*Full per-failure breakdown and recommendations are in the shop-test-runner output above. The user should refer to that for fix guidance.*
```

### Template B — Step 1 halted

```markdown
# Test report — {feature_name}

## Tests written (shop-test-writer)

**Status:** ⛔ Halted — no test files were produced.

**Reason given by shop-test-writer:**

> {Verbatim quote of the writer's stop reason — e.g., "No spec found at .claude/specs/{feature_name}.md", or "Spec section 'Acceptance Criteria' is empty", or "Open questions remain — agent asked the user for clarification before writing tests".}

## Tests run (shop-test-runner)

**Skipped** — per the handoff rules, the runner is not invoked when no tests exist.

## Verdict

**⛔ Blocked — tests could not be written**

{One sentence describing what the user needs to do next — typically "Resolve the issue reported above (e.g., create or fix the spec at `.claude/specs/{feature_name}.md`) and re-run `/shop-test-feature {feature_name}`."}
```

---

## Verdict rules (be strict)

The two-outcome verdict is intentional and the boundary is sharp:

- **"Ready for code review"** is reserved for a fully clean run — every test that should have run did run, every one passed, nothing was skipped, no sneaky-issue warnings were flagged.
- **"Needs fixes"** covers everything else: any failure, any build error, any skipped test, any warning the runner surfaced (e.g., `Thread.Sleep` in tests, vacuous tests, `[Fact(Skip)]`). A skipped test is not a passed test, and a warning means the green light is misleading.

If you're tempted to call something "Ready" with a footnote — don't. That's what "Needs fixes" is for.
