---
description: Implement a feature across all four layers using a sequence of layer-scoped sub-agents (Domain → Application → Infrastructure ‖ Web), with build gates between layers and produced-API handoff so each layer builds on the literal output of the previous one.
argument-hint: <feature-name>
---

# /theshop.implement

**Feature requested:** `$ARGUMENTS`

You are the orchestrator for the layered implementation workflow on **The Shop** project. You do not write production code, run database migrations, or fetch Figma nodes yourself — you delegate to four specialist sub-agents in a specific order, you enforce build gates between them, and you pass each layer's produced API to the next layer as a literal handoff.

This command produces working code across Domain, Application, Infrastructure, and Web, and applies the database migration. It does **not** add XML doc comments — documentation is a separate, manually-run step (`/theshop.document`). A Stop hook (configured separately in `.claude/settings.json`) runs `dotnet format` after your turn ends.

---

## Input validation

If `$ARGUMENTS` is empty, stop and ask:

> "Please provide a feature name. Usage: `/theshop.implement <feature-name>` — for example, `/theshop.implement add-to-cart`. The feature name must match an existing plan at `.claude/plans/{feature_name}.md`."

Wait for the reply. Do nothing else.

---

## Pre-flight checks

Run these in order. A failure halts the whole flow.

### Pre-flight 1 — Plan must exist

Check `.claude/plans/$ARGUMENTS.md` exists. If not, halt:

> "I couldn't find a plan at `.claude/plans/$ARGUMENTS.md`. Implementation works from a plan — please run `/theshop.plan $ARGUMENTS` first."

### Pre-flight 1b — Plan should be resolved (warn, not a hard gate)

Read the plan's **Status** footer and its **Section 11 (Open Questions, Risks & Assumptions)**. The plan is ready to build when its Status reads `Resolved` and Section 11 has no unresolved `❓ Open question` items. (A documented `⚠️ Risk` marked `✅ Accepted` or carrying a mitigation is fine — accepted risks do not block.)

If the Status still reads `Draft`, **or** Section 11 still contains an unresolved `❓ Open question`, warn the user — do not silently build on unconfirmed decisions:

> "Heads up: the plan for `$ARGUMENTS` isn't resolved yet (Status: Draft / {N} open question(s) in Section 11). Building on unresolved questions risks rework. Run `/theshop.resolve $ARGUMENTS` first, or reply `proceed` to build on the logged assumptions as-is."

Proceed only on the user's explicit go-ahead. This mirrors how `/theshop.plan` warns when a spec is still `Draft` — it's a soft gate, not a halt.

### Pre-flight 2 — Solution must build clean before we start

```bash
dotnet build TheShop.slnx --nologo
```

If the solution is already broken, halt:

> "The solution does not build cleanly before any implementation starts. I'm not going to add changes on top of a red build — fix the existing build errors first, then re-invoke me."

### Pre-flight 3 — Working tree note (informational, not a gate)

Run `git status --short`. If the working tree is dirty, note it once at the top of your output so the user knows what's already changed before this run begins. Do not halt — implementation can layer on top of existing in-progress work.

---

## Phase 1 — Domain (sequential)

Invoke `shop-domain-implementer` via the Task tool, `subagent_type: shop-domain-implementer`. Prompt:

> "Implement the Domain-layer slice of feature `$ARGUMENTS`. Read `.claude/plans/$ARGUMENTS.md`, focus on Sections 4 (Domain entities/VOs), 5 (relevant decisions), and 9 (Domain exceptions). Follow your standard protocol and end with the structured summary including the **Public API produced** signatures block."

Wait for completion.

### Phase 1 build gate

Parse the agent's summary. Three possible outcomes:

- **Build status ✅** in the summary AND "Public API produced" block is present → continue to Phase 2.
- **Build status ❌** OR no API block → re-invoke `shop-domain-implementer` ONCE with the compile errors quoted from the summary, and the instruction "Your previous run left a broken Domain build. Fix the errors and re-emit the structured summary." If the second run still fails, halt with template C (see Final output).
- **Halted on open questions** (the agent stopped and asked for clarification) → relay the question to the user verbatim and stop. The user resumes when answered.

Capture the **Public API produced** signatures block. This is the **Domain API handoff** for Phase 2.

---

## Phase 2 — Application (sequential)

Invoke `shop-application-implementer` via the Task tool, `subagent_type: shop-application-implementer`. Prompt:

> "Implement the Application-layer slice of feature `$ARGUMENTS`. Read `.claude/plans/$ARGUMENTS.md`, focus on Sections 3, 4 (DTOs), 6, 7 (Phase 2), and 9. Build against this exact Domain API produced by the upstream agent — do not re-derive it:
>
> {paste the Domain Public API block from Phase 1 verbatim, fenced as csharp}
>
> Follow your standard protocol and end with the structured summary including the **Interfaces produced** and **DTOs and Commands produced** blocks."

Wait for completion.

### Phase 2 build gate

Same logic as Phase 1:

- ✅ + both API blocks present → continue to Phase 3.
- ❌ → one retry with errors quoted → halt on second failure (template C).
- Halted on open questions → relay and stop.

Capture **Interfaces produced** (the Infrastructure handoff) and **DTOs and Commands produced** (the Web handoff).

---

## Phase 3 — Infrastructure ‖ Web (parallel)

These two layers are siblings — both depend on Application, neither depends on the other (per `shop-guideline` SKILL.md Rule 1 — dependency rule). Launch both in a **single response** with two Task tool calls — that is the parallelism trigger. Do NOT call one, wait, then call the other.

### Call 1 — `shop-infra-implementer`

Prompt:

> "Implement the Infrastructure-layer slice of feature `$ARGUMENTS`. Read `.claude/plans/$ARGUMENTS.md`, focus on Sections 4 (tables), 7 (Phase 3), and 10 (Schema + RLS). Build against these Application interfaces produced by the upstream agent — do not re-derive them:
>
> {paste the Interfaces produced block from Phase 2 verbatim}
>
> Apply the database migration via the Supabase MCP. Follow your standard protocol and end with the structured summary."

### Call 2 — `shop-ui-implementer`

Prompt:

> "Implement the Web-layer slice of feature `$ARGUMENTS`. Read `.claude/plans/$ARGUMENTS.md`, focus on Sections 6, 7 (Phase 4), and 9, plus the Figma references in the Web section. Build against these Application DTOs and Commands produced by the upstream agent — do not re-derive them:
>
> {paste the DTOs and Commands produced block from Phase 2 verbatim}
>
> Re-fetch the feature's Figma nodes from the plan's references. Follow your standard protocol and end with the structured summary."

Wait for **both** to complete.

### Phase 3 build gate

Verify each agent's summary independently:

- Both ✅ → continue to Phase 4.
- Either ❌ → re-invoke that one agent (the other stays as-is) with errors quoted → halt on second failure for that agent (template C).
- Either halted on open questions → relay and stop.

After both succeed, run the **full solution build** as the cross-layer integration gate:

```bash
dotnet build TheShop.slnx --nologo
```

If the solution build fails despite both layer builds passing, you have a cross-layer issue (e.g., the DI registration is wrong, or Web is using a DTO field Application didn't ship). Halt with template C and quote the solution-level errors — do not silently re-invoke; the user needs to see the cross-layer break.

---

## Phase 4 — Format (runs automatically after your turn)

You do not run `dotnet format` yourself. A `Stop` hook configured in `.claude/settings.json` runs `dotnet format` once your turn ends. Mention this in the final output so the user understands why a formatting commit may appear.

> **XML documentation is not part of this command.** Documenting the code is a separate, manually-run step. Do **not** invoke `shop-code-documenter` from here — point the user at `/theshop.document` in the Next steps instead.

---

## Handoff rules (enforce strictly)

1. **Phase 1 must complete before Phase 2.** Phase 2 must complete before Phase 3. The build gate between phases is a hard gate — no skipping.
2. **Phase 3's two agents launch in the same response.** Sequential invocation is a bug.
3. **API handoff payloads are literal C# signatures**, pasted verbatim from the upstream agent's summary. Do not paraphrase. Do not abbreviate. Do not "summarize the gist."
4. **Maximum one retry per agent.** Two failures in a row → halt and surface the failure to the user. Don't loop indefinitely.
5. **No edits by the orchestrator.** You delegate; you don't edit. If a layer agent's summary suggests a Domain change is needed, halt and ask the user — do not patch from here.
6. **No `Strings.resx` edits outside of the Application agent's explicit scope.** The Application agent owns resource-key additions per its protocol. Other agents flag missing keys as open questions; they don't add them.

---

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
| 4. Format (Stop hook) | `dotnet format` | will run when this turn ends |

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

- ✅ `dotnet build TheShop.slnx` — 0 warnings / 0 errors.

## Open items the agents flagged

{Aggregate the "Open questions / TODOs" sections from every agent. Group by agent. If none, write "None."}

## Next steps

1. `dotnet format` will run automatically when this turn ends (Stop hook).
2. Run `/theshop.test $ARGUMENTS` to generate and execute tests.
3. Run `/theshop.verify $ARGUMENTS` to smoke-test the feature against the running app (user-facing features).
4. Run `/theshop.review $ARGUMENTS` for parallel quality + security review.
5. Run `/theshop.document` manually to add XML doc comments once the code is final.
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

Answer the question, then re-invoke `/theshop.implement $ARGUMENTS`. The completed phases' output is preserved — the next run continues from where this one stopped.
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

Resolve the failure manually (read the agent's full output above for context), then re-invoke `/theshop.implement $ARGUMENTS` to continue from the failing phase, or run the specific layer agent directly if you only need to re-do one step.
```
