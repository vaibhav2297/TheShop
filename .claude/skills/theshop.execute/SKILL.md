---
name: theshop.execute
description: Single-agent implementation of a feature across all four layers (Domain → Application → Infrastructure → Web) in one context — the merged, sub-agent-free equivalent of /theshop.implement, created to test the merge before migration. Same contract - plan-driven, per-layer build + scope gates, Supabase migration, Figma parity, status-tracker update - but shared context is loaded once and layer handoffs are internal. Manually invoked only.
argument-hint: <feature-name>
disable-model-invocation: true
---

# /theshop.execute — single-agent implement (merge test bed)

Same contract as `/theshop.implement`, executed by **you alone** — no sub-agents. You write all four layers in one context, enforce the same gates, and produce the same report. This skill exists to A/B the merged design against the orchestrated one; keep behavior equivalent.

Out of scope, same as before: no tests (`/theshop.test`), no XML docs (`/theshop.document`), no manual `dotnet format` (Stop hook handles it).

---

## Inputs

`{feature}` = `$ARGUMENTS`. If empty, stop and ask:

> "Please provide a feature name. Usage: `/theshop.execute <feature-name>` — it must match an existing plan at `.specs/{feature}/plan.md`."

---

## Pre-flight (in order; failure halts)

1. **Plan exists.** `.specs/{feature}/plan.md` missing → halt: "No plan found. Run `/theshop.plan {feature}` first."
2. **Plan resolved (soft gate).** Read the plan's Status footer and Section 11. Status `Draft` or any unresolved `❓ Open question` → warn: "Plan isn't resolved (Status: Draft / {N} open question(s)). Run `/theshop.resolve {feature}`, or reply `proceed` to build on logged assumptions." Proceed only on explicit go-ahead; record the waiver in `status.md` (see Tracker). Accepted/mitigated `⚠️ Risk` items don't block.
3. **Clean baseline build.** `dotnet build TheShop.slnx --nologo`. Red → halt: fix the existing build first.
4. **Working tree note.** `git status --short`. Dirty → note it at the top of your output. Not a gate.

---

## Load once — the merge dividend

This is the point of the merge: nothing shared is loaded twice.

- **Read `.specs/{feature}/plan.md` in full, once.** Each phase below names its sections; don't re-read.
- **Load `theshop.constitution` now:** `SKILL.md` + `references/rules/architecture-core.md`. On any conflict, **the skill wins** over this file.
- **Load the remaining references lazily, at their phase, each at most once:**

| Phase | Load |
|---|---|
| 1 Domain | `examples/domain-entity.md` |
| 2 Application | `rules/architecture-patterns.md`, `examples/application-handler.md` |
| 3 Infrastructure | `examples/infrastructure-repository.md`; `rules/architecture-admin.md` if admin tables/RLS roles |
| 4 Web | `rules/design-theme.md`, `rules/design-components.md`, `rules/design-strings.md`, `examples/web-page.md`, `examples/web-component.md`; `rules/design-styles.md` if CSS/SCSS; `rules/architecture-admin.md` if admin UI (skip if already loaded) |

Never load `rules/documentation.md` (documenter's job) or `design-theme-setup.md`.

- **Orient with the graph, per phase:** if `graphify-out/graph.json` exists, `graphify query "..."` scoped to that layer + feature; `Read` only surfaced files. `Glob` the layer folder only as fallback. Reuse and extend existing types — never duplicate.

---

## Gates (apply to every phase)

**Build gate.** End each phase with `dotnet build src/TheShop.{Layer}/TheShop.{Layer}.csproj --nologo`. Red → fix and rebuild. Same phase still red after **2 focused fix attempts** → halt, Template C. Green → `graphify update .` (AST-only, non-fatal; skip silently if unavailable).

**Scope gate.** Snapshot `git status --porcelain --untracked-files=all` before each phase; diff after it to get the phase's newly changed files (`.specs/` and `tests/` exempt). Then:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 scope -Phase {domain|application|infra|web} -Files {comma-separated}
```

Exit 1 → you misplaced a file. Relocate it to the correct layer **once** and re-run the gate; still failing → halt, Template C. (Sub-agents had to hard-halt here; you may fix your own placement, once.)

**Reopen rule** (replaces the cold sub-agent retry). If a later phase reveals an upstream gap — a missing Domain method, a wrong Application interface — don't bend the downstream code. Explicitly **reopen** the upstream phase: make the change there, rebuild that layer, attribute those files to that phase's scope gate, and log the reopen in the final report. Never silently mix layers.

**Ask, don't invent.** Anything vague, contradictory, or missing in the plan → stop and ask the user (Template B). Every type, handler, table, and page must trace to a plan line — nothing "for completeness."

---

## Phase 1 — Domain (`src/TheShop.Domain/` only)

Plan sections: **4** (entities/VOs), **5** (domain-relevant decisions), **9** (domain exceptions + MessageKeys).

Rules:
- Zero dependencies: no Supabase/MudBlazor/Stripe/JSON usings; the Domain csproj references nothing.
- No Application concerns: no handlers, validators, DTOs, `Result<T>`.
- Invariants live **inside** entities; violations throw `DomainException` subtypes carrying `MessageKey = nameof(Strings.X)`.
- Extend existing entities/VOs in place; inherit from the existing `DomainException` base.

On green build, record the **Public API produced** block (exact signatures — entities, VOs, exceptions) for the report and as the contract Phase 2 builds against.

## Phase 2 — Application (`src/TheShop.Application/` + resx exception)

Plan sections: **3** (flow), **4** (DTOs), **6** (journeys → handlers), **7 Phase 2** (the explicit list), **9** (validators + error-key table).

Rules:
- No external SDKs, no `Microsoft.AspNetCore.*`. Depends on Domain only — build against Phase 1's exact API.
- Declare repository/service interfaces in `Common/Interfaces/`; Phase 3 implements them.
- Reuse cross-cutting types (`Result<T>`, `ValidationBehavior<,>`, `ICurrentUserService`) — never re-declare.
- New pipeline behaviors/services → register in `src/TheShop.Application/DependencyInjection.cs`; otherwise leave it alone.
- **Strings (the one out-of-layer write):** every referenced `nameof(Strings.X)` gets a key in `src/TheShop.Web/Resources/Strings.resx` (English from Section 9) **and** `Strings.fr.resx` (real French, or `[TODO] {English}` — the literal `[TODO]` is what the review localization gate scans for). Touch only `.resx` files there; **never** `Strings.Designer.cs` (auto-generated).

On green build, record the **Interfaces produced** and **DTOs and Commands produced** blocks.

## Phase 3 — Infrastructure (`src/TheShop.Infrastructure/` + Supabase MCP)

Plan sections: **4** (tables), **7 Phase 3**, **10** (schema + RLS — the migration source).

Database first:
1. `list_tables` + `list_migrations` — see what exists. Tables already correct → apply deltas only. Shape mismatch on a populated table → halt and confirm with the user.
2. Destructive DDL (`DROP TABLE`, dropping NOT NULL on populated columns, …) → surface and get confirmation **before** applying.
3. `apply_migration` with a snake_case name and the full Section 10 SQL: tables, indexes, `ENABLE ROW LEVEL SECURITY`, every policy. **Every new table needs RLS + ≥1 policy** — the plan omitting policies is a halt-and-surface, not a shrug. RLS is the only real security boundary (`architecture-admin.md`).
4. `get_advisors` (lint) after; report new warnings.

Code rules:
- Records/mappers/repositories follow the canonical trio (`internal sealed` records); match the shape of existing repositories.
- No business logic in repositories — rules live on Domain entities. No SDK types (`Supabase.Client`, `Stripe.*`, `Resend.*`) on public surfaces.
- Implement Phase 2's interfaces exactly; an interface that looks wrong → **reopen Phase 2**, don't edit it from here.
- One DI registration per implementation in `src/TheShop.Infrastructure/DependencyInjection.cs`, matching existing lifetimes/style.

## Phase 4 — Web (`src/TheShop.Web/` only)

Plan sections: **6**, **7 Phase 4** (pages/components/state/routes list), **9** (error keys → `Snackbar`/`MudAlert`), plus **Figma references** — non-negotiable; missing → halt.

Figma first (never build UI from imagination):
1. Per plan node ID: `figma_get_component_for_development` (`_deep` for nested).
2. Once: `figma_get_variables` + `figma_get_text_styles`. Map every token: color variable → `Color="Color.Primary"` when semantics match, else `mud-*` class, else `ShopColors.X` (last resort, commented); text style → `Typo.X` on `MudText`; spacing → utility classes (`pa-4`, `gap-2`). No `Shop*` equivalent → open question; don't mint tokens.
3. Unfamiliar MudBlazor component → `mcp__mudblazor__get_component_detail`.

Rules:
- Pages render and dispatch via `IMediator.Send` — no business logic in `@code`/code-behind, no Infrastructure SDK usings.
- **MudBlazor can't meet a requirement → halt and ask** (Rule 14). Never silently hand-roll a UI primitive.
- Reuse first: existing layouts, components, state stores. Append new `Routes.X` / `BusyKeys.X` constants before referencing them.
- Every user-facing string → key in both `Strings.resx` and `Strings.fr.resx` (`[TODO]` placeholder rule as above); never `Strings.Designer.cs`.
- The PostToolUse design-lint hook feeds violations back with rule numbers: fix each immediately; `design-rules: ignore` only with explicit user approval.

Visual validation (mandatory): build Web → `figma_take_screenshot` on the reference node → compare layout/spacing/typography/color → fix in-scope mismatches and re-check. **Max 3 iterations**; still off → halt and report what blocks parity. Then run `references/checklists/design.md` against your output.

---

## Integration gate

After Phase 4: `dotnet build TheShop.slnx --nologo`. Red despite green layers = cross-layer break (DI wiring, DTO drift). Diagnose, fix via **one** reopen of the owning phase (build + scope gate for it), and rebuild the solution. Still red → halt, Template C, quoting the solution errors. Then a final `graphify update .` as safety net.

---

## Tracker (full success only)

Update `.specs/{feature}/status.md` **Implement** row: State `Done`; Gate `✅ scope + build gates pass (single-agent)` — or `⚠️ waived: plan Draft, {N} open question(s)` if the user proceeded past Pre-flight 2; Evidence = one mechanical line (e.g. `4 layers built · migration add_x · scope clean · single-agent run`); today's date. Refresh **Last updated**; set **Next step** to `/theshop.test {feature}`. Missing `status.md` → create from the `theshop.spec` template first. Never touch the tracker on a halted run.

---

## Final output — one template, verbatim, no extra prose

### Template A — Full success

```markdown
# Implementation report — {feature} (single-agent run)

## Phases run

| Phase | Status |
|---|---|
| 1. Domain | ✅ |
| 2. Application | ✅ |
| 3. Infrastructure | ✅ |
| 4. Web | ✅ |
| Format (Stop hook) | runs when this turn ends |

## Files changed
{Per layer, cross-checked against `git diff --name-only`.}

## API surface produced
{The Domain public-API, Interfaces, and DTOs/Commands blocks recorded per phase.}

## Migrations applied
{Name, tables, indexes, RLS policies, advisor warnings. "None." if none.}

## Visual validation
{Figma parity per page/component, iterations used.}

## Reopens
{Any upstream phase reopened mid-run, with reason. "None." if none.}

## Build status
- ✅ `dotnet build TheShop.slnx` — 0 warnings / 0 errors.

## Open items
{Ambiguities you flagged. "None." if none.}

## Next steps
1. `dotnet format` runs automatically (Stop hook).
2. `/theshop.test {feature}` → 3. `/theshop.verify {feature}` (user-facing) → 4. `/theshop.review {feature}` → 5. `/theshop.document` when final.
```

### Template B — Halted on open question

```markdown
# Implementation report — {feature} (single-agent run)

## Phases run
{Table — halting phase ⛔, later phases "Not started".}

## Halt reason
**Phase {N} stopped on a clarifying question:**
> {The question.}

## What I'm not doing
- Later phases untouched; no code beyond the completed phases.

## Next step
Answer, then re-invoke `/theshop.execute {feature}` — completed phases are preserved on disk; the run continues from the halted phase.
```

### Template C — Halted on hard failure

```markdown
# Implementation report — {feature} (single-agent run)

## Phases run
{Table — failing phase 🔴.}

## Halt reason
**Phase {N} failed after the fix budget (2 attempts / 1 reopen):**
- {One line per attempt: what broke.}

## Evidence
```
{Error output from the last attempt.}
```

## What I'm not doing
- Later phases untouched. No third retry — that's an infinite loop in disguise.

## Next step
Resolve manually, then re-invoke `/theshop.execute {feature}` to continue from the failing phase.
```
