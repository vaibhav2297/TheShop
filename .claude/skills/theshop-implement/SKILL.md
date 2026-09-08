---
name: theshop-implement
description: "Implement feature through Domain, Application, then parallel Infrastructure/Web specialists. Enforce scope/build gates and literal API handoffs."
argument-hint: "<feature-name>"
---

<!-- Generated from .sdd/skills/theshop-implement/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/claude/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# /theshop-implement

**Feature requested:** `$ARGUMENTS`

Orchestrate four layer specialists in prescribed order. Enforce build gates and literal API handoffs. Never write production code, run migrations, or fetch Figma nodes yourself.

## Scope

Deliver four layers and database migration through specialists. XML documentation remains manual: `/theshop-document`. Run `.sdd/scripts/format-changes.ps1` before final verification; repeat affected build checks.

---

## Inputs

If `$ARGUMENTS` is empty, stop and ask:

> "Please provide a feature name. Usage: `/theshop-implement <feature-name>` — for example, `/theshop-implement add-to-cart`. The feature name must match an existing plan at `.specs/{feature_name}/plan.md`."

Wait for the reply. Do nothing else.

---

## Pre-flight checks

Run these in order. A failure halts the whole flow.

### Pre-flight 1 — Plan must exist

Check `.specs/$ARGUMENTS/plan.md` exists (the feature's home folder is `.specs/$ARGUMENTS/`). If not, halt:

> "I couldn't find a plan at `.specs/$ARGUMENTS/plan.md`. Implementation works from a plan — please run `/theshop-plan $ARGUMENTS` first."

### Pre-flight 1b — Plan should be resolved (warn, not a hard gate)

Read the plan's **Status** footer and its **Section 11 (Open Questions, Risks & Assumptions)**. The plan is ready to build when its Status reads `Resolved` and Section 11 has no unresolved `❓ Open question` items. (A documented `⚠️ Risk` marked `✅ Accepted` or carrying a mitigation is fine — accepted risks do not block.)

If the Status still reads `Draft`, **or** Section 11 still contains an unresolved `❓ Open question`, warn the user — do not silently build on unconfirmed decisions:

> "Heads up: the plan for `$ARGUMENTS` isn't resolved yet (Status: Draft / {N} open question(s) in Section 11). Building on unresolved questions risks rework. Run `/theshop-resolve $ARGUMENTS` first, or reply `proceed` to build on the logged assumptions as-is."

Proceed only on the user's explicit go-ahead. This mirrors how `/theshop-plan` warns when a spec is still `Draft` — it's a soft gate, not a halt. **If the user says proceed, the waiver gets recorded**: when you update `.specs/$ARGUMENTS/status.md` at the end, the Implement row's Gate cell reads `⚠️ waived: plan Draft, {N} open question(s)` instead of a plain pass. Skipped gates are visible, never silent.

### Pre-flight 2 — Solution must build clean before we start

```bash
dotnet build TheShop.slnx --nologo
```

If the solution is already broken, halt:

> "The solution does not build cleanly before any implementation starts. I'm not going to add changes on top of a red build — fix the existing build errors first, then re-invoke me."

### Pre-flight 3 — Working tree note (informational, not a gate)

Run `git status --short`. If the working tree is dirty, note it once at the top of your output so the user knows what's already changed before this run begins. Do not halt — implementation can layer on top of existing in-progress work.

---

## Layer-scope gate (applies to every phase)

Verify each role's `src/TheShop.{Layer}/` ownership mechanically after every phase:

Persist required handoff first. Immediately before delegating each production phase, run `.sdd/scripts/check-worker-scope.ps1 -Action snapshot -SnapshotPath .sdd/.test-work/{feature}-{phase}-{unique}.json`. After workers finish, run the same script with `-Action check`, same snapshot, and `-Phase domain|application|infra+web`. This byte comparison is mandatory: Git status alone misses edits to already-dirty files. Tests and feature artifacts are forbidden worker writes; orchestrator writes outcome metadata after checking scope. Graph refresh is the explicit exception. Parallel Infrastructure/Web checks prove union scope, not individual attribution.

1. **Before invoking the phase's agent(s)**, snapshot the changed-file list:

   ```bash
   git status --porcelain --untracked-files=all
   ```

   Keep the list of paths in your context as the phase baseline.

2. **After the agent completes**, run the same command again for reporting. The mandatory byte snapshot above detects actual changes, including previously dirty files. The legacy gate below supplements that check; its artifact/test exemption never grants worker write permission.

3. **Run the scope check** with the phase's owner:

   ```bash
   pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 scope -Phase {domain|application|infra|web|infra+web} -Files {comma-separated newly changed files}
   ```

   Phase 1 → `domain` · Phase 2 → `application` (the script already allows the two `Strings*.resx` exceptions) · Phase 3 → `infra+web` (the two agents run in parallel, so their changes are checked as a union against the combined allowed scope).

4. **Exit 1 → halt with Template C.** Do not retry the agent — it has already written outside its layer, and unwinding that is a human decision. Quote the gate output as the evidence block. A scope escape is a contract breach, not a compile error.

---

## Phase 1 — Domain (sequential)

Invoke `shop-domain-implementer` via the delegation capability, `role: shop-domain-implementer`. Prompt:

> "Implement the Domain-layer slice of feature `$ARGUMENTS`. Read `.specs/$ARGUMENTS/plan.md`, focus on Sections 4 (Domain entities/VOs), 5 (relevant decisions), and 9 (Domain exceptions). Follow your standard protocol and end with the structured summary including the **Public API produced** signatures block."

Wait for completion.

### Phase 1 build gate

First run the **layer-scope gate** for `domain` (see above) — a scope escape halts before the build outcome even matters. Then parse the agent's summary. Three possible outcomes:

- **Build status ✅** in the summary AND "Public API produced" block is present → continue to Phase 2.
- **Build status ❌** OR no API block → re-invoke `shop-domain-implementer` ONCE with the compile errors quoted from the summary, and the instruction "Your previous run left a broken Domain build. Fix the errors and re-emit the structured summary." If the second run still fails, halt with template C (see Final output).
- **Halted on open questions** (the agent stopped and asked for clarification) → relay the question to the user verbatim and stop. The user resumes when answered.

Capture the **Public API produced** signatures block. This is the **Domain API handoff** for Phase 2.

---

## Phase 2 — Application (sequential)

Invoke `shop-application-implementer` via the delegation capability, `role: shop-application-implementer`. Prompt:

> "Implement the Application-layer slice of feature `$ARGUMENTS`. Read `.specs/$ARGUMENTS/plan.md`, focus on Sections 3, 4 (DTOs), 6, 7 (Phase 2), and 9. Build against this exact Domain API produced by the upstream agent — do not re-derive it:
>
> {paste the Domain Public API block from Phase 1 verbatim, fenced as csharp}
>
> Follow your standard protocol and end with the structured summary including the **Interfaces produced** and **DTOs and Commands produced** blocks."

Wait for completion.

### Phase 2 build gate

First run the **layer-scope gate** for `application`. Then same logic as Phase 1:

- ✅ + both API blocks present → continue to Phase 3.
- ❌ → one retry with errors quoted → halt on second failure (template C).
- Halted on open questions → relay and stop.

Capture **Interfaces produced** (the Infrastructure handoff) and **DTOs and Commands produced** (the Web handoff).

---

## Phase 3 — Infrastructure ‖ Web (parallel)

Both layers depend on Application, never each other (constitution Rule 1). Launch both delegation calls in **one response**. Never wait for one before launching the other.

### Call 1 — `shop-infra-implementer`

Prompt:

> "Implement the Infrastructure-layer slice of feature `$ARGUMENTS`. Read `.specs/$ARGUMENTS/plan.md`, focus on Sections 4 (tables), 7 (Phase 3), and 10 (Schema + RLS). Build against these Application interfaces produced by the upstream agent — do not re-derive them:
>
> {paste the Interfaces produced block from Phase 2 verbatim}
>
> Apply the database migration via the Supabase MCP. Follow your standard protocol and end with the structured summary."

### Call 2 — `shop-ui-implementer`

Prompt:

> "Implement the Web-layer slice of feature `$ARGUMENTS`. Read `.specs/$ARGUMENTS/plan.md`, focus on Sections 6, 7 (Phase 4), and 9, plus the Figma references in the Web section. Build against these Application DTOs and Commands produced by the upstream agent — do not re-derive them:
>
> {paste the DTOs and Commands produced block from Phase 2 verbatim}
>
> Re-fetch the feature's Figma nodes from the plan's references. Follow your standard protocol and end with the structured summary."

Wait for **both** to complete.

### Phase 3 build gate

First run the **layer-scope gate** for `infra+web` over the union of both agents' newly changed files. Then verify each agent's summary independently:

- Both ✅ → continue to Phase 4.
- Either ❌ → re-invoke that one agent (the other stays as-is) with errors quoted → halt on second failure for that agent (template C).
- Either halted on open questions → relay and stop.

After both succeed, run the **full solution build** as the cross-layer integration gate:

```bash
dotnet build TheShop.slnx --nologo
```

Failed solution build despite passing layer builds: halt with Template C and exact solution errors. Never silently retry cross-layer failures.

### Knowledge-graph refresh (after the solution build passes)

Refresh once after integration, even if workers already refreshed:

```bash
graphify update .
```

AST-only, no API cost, and it writes only under `graphify-out/` — it is not a production-code edit, so it does not violate Handoff rule 5. Non-fatal: if `graphify` or `graphify-out/` is unavailable, skip silently.

---

## Phase 4 — Format and final verification

Read `references/implementation-tests.md`. Delegate required test authoring to `shop-test-writer` after production workers finish. Test files remain exclusively test-writer owned. Use handoff purpose `implementation-tests`, which requires fresh Plan evidence and does not mark separate Test stage complete.

After test writer finishes, run `pwsh -NoProfile -File .sdd/scripts/format-changes.ps1`, repeat solution build and manifest/compile gates, then delegate execution to `shop-test-runner` with same handoff purpose. Apply reference's completion criteria. A failed formatter, missing Rule 29 test, failing/skipped test, or discovery mismatch blocks Implement completion. Record actual results; never fill warning counts from template examples.

> **XML documentation is not part of this command.** Documenting the code is a separate, manually-run step. Do **not** invoke `shop-code-documenter` from here — point the user at `/theshop-document` in the Next steps instead.

---

## Handoff rules (enforce strictly)

1. **Phase 1 must complete before Phase 2.** Phase 2 must complete before Phase 3. The build gate between phases is a hard gate — no skipping.
2. **Phase 3's two agents launch in the same response.** Sequential invocation is a bug.
3. **Paste upstream C# API signatures verbatim.** Never paraphrase or abbreviate handoff payloads.
4. **Maximum one retry per agent.** Two failures in a row → halt and surface the failure to the user. Don't loop indefinitely.
5. **No production or test code edits by the orchestrator.** Delegate to owning role. Orchestrator may write feature ledger, evidence sidecars, and reports required by execution contract. If a layer summary needs Domain changes, halt and surface required reopen; do not patch from here.
6. **No `Strings.resx` edits outside of the Application agent's explicit scope.** The Application agent owns resource-key additions per its protocol. Other agents flag missing keys as open questions; they don't add them.

---

## Update the status tracker (full success only)

Only after Phase 4 formatting, build, test execution, and required member coverage pass, record Implement evidence and update `.specs/$ARGUMENTS/status.md`: State `Done`, Gate `✅ scope + build + implementation tests pass`, or existing explicit waiver text when applicable. Evidence contains observed counts and log paths. Refresh **Last updated** and point **Next step** at `/theshop-test $ARGUMENTS`. Do not mark separate Test stage complete here. If ledger is missing, use Spec tracker template. Failed run cannot receive Done; record its failure through evidence contract.

## Final output

Always produce one of these three templates verbatim. No extra prose.

### Template A — Full success

```markdown
# Implementation report — $ARGUMENTS

## Phases run

| Phase | Agent | Status |
|---|---|---|
| 1. Domain | shop-domain-implementer | ✅ |
| 2. Application | shop-application-implementer | ✅ |
| 3a. Infrastructure | shop-infra-implementer | ✅ |
| 3b. Web | shop-ui-implementer | ✅ |
| 4. Format | `dotnet format` | {observed formatter and post-format build result} |

## Files changed

{Aggregate list of every file each agent reported as new/modified — grouped by layer. Use `git diff --name-only` to cross-check.}

- **Domain:** `src/TheShop.Domain/Entities/Cart.cs`, …
- **Application:** `src/TheShop.Application/Features/Cart/...`, `src/TheShop.Web/Resources/Strings.resx` (keys added)
- **Infrastructure:** `src/TheShop.Infrastructure/Persistence/...`
- **Web:** `src/TheShop.Web/Pages/Cart/...`, `src/TheShop.Web/Common/Routes.cs`, …

## Migrations applied

- {Name from Infrastructure agent's summary. If none, write "None."}

## Visual validation

- {From Web agent's summary — Figma parity status. If any iteration was needed, note it.}

## Build status

- `dotnet build TheShop.slnx` — {observed exit code, warning count, error count, and evidence path}.

## Implementation tests

{Writer/runner results: new-member coverage, manifest/discovered/passed/failed/skipped counts, and evidence paths. Keep unresolved AC proof explicit.}

## Open items the agents flagged

{Aggregate the "Open questions / TODOs" sections from every agent. Group by agent. If none, write "None."}

## Next steps

1. Formatting and post-format verification must pass before reporting completion.
2. Run `/theshop-test $ARGUMENTS` to generate and execute tests.
3. Run `/theshop-e2e $ARGUMENTS` for browser acceptance proof (user-facing features).
4. Run `/theshop-review $ARGUMENTS` for parallel quality + security review.
5. Run `/theshop-document` manually to add XML doc comments once the code is final.
```

### Template B — Halted on open question

```markdown
# Implementation report — $ARGUMENTS

## Phases run

{Same table as Template A — but the halting phase is marked ⛔ and subsequent phases show "Not started"}

## Halt reason

**Phase {N} ({agent-name}) stopped to ask a clarifying question:**

> {Verbatim quote of the question the agent posed.}

## What I'm not doing

- Subsequent phases were not started.
- No code beyond {phase that did complete} has been written or applied.

## Next step

Answer the question, then re-invoke `/theshop-implement $ARGUMENTS`. The completed phases' output is preserved — the next run continues from where this one stopped.
```

### Template C — Halted on hard failure

```markdown
# Implementation report — $ARGUMENTS

## Phases run

{Same table — failing phase marked 🔴}

## Halt reason

**Phase {N} ({agent-name}) failed after one retry:**

- First attempt: {one-line summary of what broke}
- Retry attempt: {one-line summary of what still broke}

## Evidence

```
{Quoted error output from the second attempt — typically the dotnet build error block}
```

## What I'm not doing

- Subsequent phases were not started.
- I will not retry a third time — that's an infinite loop in disguise.

## Next step

Resolve the failure manually (read the agent's full output above for context), then re-invoke `/theshop-implement $ARGUMENTS` to continue from the failing phase, or run the specific layer agent directly if you only need to re-do one step.
```
