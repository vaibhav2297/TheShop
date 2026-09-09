# Feature Implementation Plan Template

> Canonical plan template, read by `/theshop-plan` and enforced by `check-sdd-gates.ps1 plan`:
> eleven numbered sections with the required title keywords, Section 7 as an agent-aligned
> execution plan with sequential `TASK-nnn` ids (one step per implementation agent, a
> contract-freeze gate before the parallel Infra ‖ Web steps, per-step completion gates, a
> deviation procedure), Section 8 mapping every spec AC to TASK ids, Section 11's ❓/⚠️/📌
> vocabulary, and the Status footer.
>
> The plan is a **decision document with executable artifacts**: real SQL, real `Strings.resx`
> keys, real Figma node ids — things downstream agents can act on literally. Keep it 3–6 pages;
> past that, the feature is probably two features.
>
> Replace placeholders in `{curly braces}`. Delete guidance blockquotes when generating a real plan.

---

# Implementation Plan — {Feature Title}

> Companion to `.specs/{file_name}/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

{2–4 sentences stating the engineering goal in technical terms, referencing the spec's problem
statement briefly. e.g., "Add a use case that lets an authenticated customer add a product to a
server-persisted cart, enforced by domain invariants (max 20 distinct items) and gated by RLS on
the `carts` table."}

## 2. Tech Stack

{Only what this feature actually relies on, with the layer it lives in. Don't list every library in
the project.}

- **Domain:** C# (no external deps).
- **Application:** MediatR, FluentValidation, AutoMapper, `Result<T>` (project-internal).
- **Infrastructure:** `supabase-csharp` for persistence. {+ Stripe/Resend if used}
- **Web:** MudBlazor, bUnit for component tests.
- **Persistence:** Supabase (PostgreSQL + RLS).

## 3. High-level Architecture

{One short paragraph + a flow diagram showing how a single user action propagates through the
layers. Be specific to this feature — real command names, real repository interfaces.}

```
User clicks "{action}" in {Page}.razor
   ↓
IMediator.Send({Feature}Command)
   ↓
{Feature}Handler (Application)
   ├── I{X}Repository.{Method}(...)
   └── {entity}.{DomainMethod}(...)   // invariants enforced here
   ↓
Supabase{X}Repository (Infrastructure) → {tables} (RLS-gated)
   ↓
Result<{X}Dto> → {Page}.razor → {State} updated → UI re-renders
```

## 4. Data Model

### Domain entities & value objects
{New or modified entities, their key methods, and the invariants they enforce. Name the exceptions
they throw.}

- **`{Entity}`** — methods: `{…}`. Invariants: {…}. Throws `{DomainException}`.

### DTOs (Application → Web)
- **`{X}Dto`** — fields: {…}.

### Database tables (new or modified)
| Table | Purpose | Key columns |
|---|---|---|
| `{table}` ({new/modified}) | {…} | {…} |

### Indexes
- `{table} ({columns})` — {which query it serves}.

## 5. Core Design Decisions

{Numbered. Each decision has: what we chose, why, and what we rejected. Tie back to spec
constraints/RULE ids and `theshop-constitution` rule numbers where relevant. This section is the
highest-value content the plan carries into review — don't thin it out.}

1. **Decision:** {…}
   - **Why:** {…}
   - **Rejected:** {alternative} — {why not}.

2. **Decision:** {…}
   - **Why:** {…}
   - **Rejected:** {…}

## 6. Core Functional Flow

{One subsection per behavior from spec Section 3, mapped step-by-step to the implementation:
component event → command → validator → handler → domain call → repository → result key → UI
feedback.}

### Flow 1: {Behavior name}

1. `{Page}.razor` — user clicks `MudButton` bound to `{Method}()`.
2. Page calls `Mediator.Send(new {Feature}Command(...))`.
3. `ValidationBehavior` runs `{Feature}CommandValidator`. On failure → `Result.Fail(nameof(Strings.{Key}))`.
4. `{Feature}Handler` — {loads, checks, calls domain}.
5. On `DomainException` → `Result.Fail(ex.MessageKey)`.
6. Saves; returns `Result.Ok(_mapper.Map<{X}Dto>(…))`.
7. Page updates `{State}`; `Snackbar` shows `Strings.{SuccessKey}` or `Localizer[result.Error]`.

### Flow 2: {next behavior}
{…}

## 7. Development Plan

{Agent-aligned execution plan. **Task-id rules:** ids are continuous across the whole plan
(`TASK-001`, `TASK-002`, …), never reset per step, and stay stable once the plan is Resolved.
Removed ids are not reused; newly discovered tasks take the next free id. One id = one committable
outcome. Skip any step whose layer has no impact — say so in one line, don't leave empty scaffolding.}

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — {Create/extend `{Entity}` in `TheShop.Domain/Entities/` with {invariants}.}
- [ ] **TASK-002** — {Create `{DomainException}` in `TheShop.Domain/Exceptions/`.}
- [ ] **TASK-003** — {Domain unit tests for invariants (`{Entity}Tests.cs`), owned by `shop-test-writer` during implementation test verification. Domain implementer supplies literal API only.}

**Completion gate:** Domain builds · no outer-layer type leaked into Domain · public API reported for Application. Test specialist covers invariants before overall Implement completion.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-004** — {`{Feature}Command` + handler + validator in `Features/{Feature}/Commands/{Command}/`.}
- [ ] **TASK-005** — {`I{X}Repository` in `Common/Interfaces/`; DTOs + mapper under the feature folder.}
- [ ] **TASK-006** — {New keys in `Strings.resx` (see Section 9) mirrored in `Strings.fr.resx` with `[TODO]` placeholders.}
- [ ] **TASK-007** — {Application unit tests (`{Feature}HandlerTests.cs`), owned by `shop-test-writer` before Implement completion.}

**Completion gate:** command/query folders follow the feature-folder convention · handler translates
every Section 9 outcome · all contracts consumed by Infra/Web are written and compiling.

### Step 3 — Contract freeze

{Freeze every contract Infrastructure or Web consumes. Infra and Web may start only when the rows
they depend on are `Stable`. A frozen contract changes only via the deviation procedure below.}

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `{Feature}Command` / `Result<{X}Dto>` | Application | Web | Stable / Blocked |
| `I{X}Repository` | Application | Infrastructure | Stable / Blocked |
| `{X}Dto` shape | Application | Web | Stable / Blocked |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`I{X}Repository` stable).

- [ ] **TASK-008** — {Migration: tables + indexes + RLS policies from Section 10, applied via Supabase MCP.}
- [ ] **TASK-009** — {`{X}Record` in `Persistence/Records/` + mapper in `Persistence/Mappers/`.}
- [ ] **TASK-010** — {`Supabase{X}Repository : I{X}Repository` in `Persistence/Repositories/`; register in `DependencyInjection.cs`.}
- [ ] **TASK-011** — {Failure translation: Supabase/provider errors → the Result outcomes in Section 9; concurrency/idempotency protection where Section 5 calls for it.}

**Completion gate:** migration applies cleanly · RLS policies match Section 10 verbatim · repository
satisfies the frozen interface · no Infrastructure type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (command/DTO shapes stable).

**Figma references** *(required when this step touches UI — re-fetched by `shop-ui-implementer` at
implementation time; omit only for backend-only features and log the gap in Section 11)*

- **File:** {full Figma file URL}
- **Nodes:**
  - `{123:456}` — {one-sentence visual intent: what this node is and where it fits in the flow}
  - `{123:457}` — {…}

- [ ] **TASK-012** — {Page/component work in `Pages/{Area}/` or `Components/{Feature}/`, matching the nodes above.}
- [ ] **TASK-013** — {Wire to `Mediator.Send`; handle every applicable UI state (loading/empty/validation/conflict/success/failure/unauthorized).}
- [ ] **TASK-014** — {`Routes.{…}` constants, `BusyKeys.{…}`, consume resource keys added by Application from Section 9.}
- [ ] **TASK-015** — {bUnit component tests, owned by `shop-test-writer` before Implement completion.}

**Completion gate:** matches Figma nodes · MudBlazor-only, no hardcoded strings or design tokens
(constitution rules 2–5) · consumes only frozen contracts · unauthorized users see the specified
denied experience.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-016** — Cross-layer verification: solution builds, DI resolves, migrations applied, primary + failure flows work end-to-end.
- [ ] **TASK-017** — Test specialists write and run required unit/component tests before Implement completion. Record actual counts, member coverage, and remaining AC proof. Follow with `/theshop-test {feature}`, `/theshop-e2e {feature}` for user-facing work, and `/theshop-review {feature}` when invoked. Document stays a separate manual invocation; never schedule it automatically.

### Deviation procedure

{When an agent wants to diverge from this plan or a frozen contract must change:}

- **Accept** a deviation only if it preserves approved behavior and layer boundaries, aligns better
  with existing project conventions, and doesn't weaken authorization/integrity or expand scope.
- **Reject** it if it changes a requirement, adds business behavior, violates dependency direction,
  silently alters a frozen contract, or smuggles in unrelated refactoring.
- **Contract change:** stop dependent work → record the change here (update the freeze table and
  affected TASK ids) → resume only after the contract is re-frozen.

## 8. Acceptance Criteria → Task Mapping

{Every AC in the spec must appear here, mapped to TASK ids. If an AC has no mapping, mark it
`⛔ UNMAPPED` and surface it in Section 11 — the plan gate enforces full coverage.}

| AC from spec | Maps to |
|---|---|
| AC-1: {summary} | TASK-004 (handler happy path), TASK-012 (page wiring) |
| AC-2: {summary} | TASK-001 (domain invariant), TASK-003 (tests) |
| AC-3: {summary} | {…} |

## 9. Validation & Error Handling Strategy

{Every spec edge case and RULE lands here as a validator rule, a domain exception, or a
`Result.Fail` key — nothing handled "somehow".}

### Validators (Application layer)
- `{Feature}CommandValidator`:
  - {field rule, citing spec RULE-n} → `Strings.{Key}`

### Domain exceptions
- `{DomainException}` — thrown when {…}. `MessageKey = nameof(Strings.{Key})`.

### Result.Fail error keys (new entries in `Strings.resx`)
| Key | English text |
|---|---|
| `Strings.{Key}` | "{…}" |
| `Strings.{Key}` | "{…}" |

All keys mirrored in `Strings.fr.resx` (`[TODO]` placeholder acceptable for the first pass — the
review step's French-completeness gate catches stragglers).

## 10. Database Schema & RLS Policies

### Schema
```sql
CREATE TABLE {table} (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    {…}
);

CREATE INDEX {idx_name} ON {table}({columns});
```

### RLS policies (the only real security boundary — per `rules/architecture-admin.md`)
```sql
ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;

CREATE POLICY "{policy_name}" ON {table}
    FOR {ALL|SELECT|…} USING ({predicate});
```

{Literal, runnable SQL — TASK-008 applies exactly this. If a detail is undecided, that's a Section
11 item, not a vague policy.}

## 11. Open Questions, Risks & Assumptions

{Each item carries one of three labels — `/theshop-resolve` walks this list:}

- **❓ Open question:** {the spec didn't say, and the answer materially affects the plan. Must be answered before implementation.}
- **⚠️ Risk:** {could go wrong even with a correct implementation; name the mitigation and the TASK that owns it.}
- **📌 Assumption:** {judgment call made to fill a gap — surface for ratification.}

---
**Status:** Draft · **Spec:** `.specs/{file_name}/spec.md` · **Created:** {YYYY-MM-DD}

<!-- Status lifecycle: "Draft" → "Resolved" once /theshop-resolve settles every ❓ and ratifies every 📌 in Section 11 (accepted ⚠️ risks may remain, labeled). /theshop-implement warns while the plan is still Draft. -->
