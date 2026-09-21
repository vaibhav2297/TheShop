---
name: theshop-execute
description: Implement one resolved feature plan in a single session, layer by layer, with scoped build gates and no sub-agents.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Execute Feature

Implement `.specs/{feature}/plan.md` in one session.

Order: **Domain → Application → Infrastructure → Web → integration**.

No sub-agents. Do not split layers across sessions.

## Input

Read [feature identity](../theshop-start/references/feature-identity.md). Resolve input; preserve full ID as `{feature}`.

Require:

- `.specs/{feature}/spec.md`
- `.specs/{feature}/plan.md`

Load `$theshop-constitution` once. Read core rules first; load routed references only when their layer starts.

Read the plan once. Build a phase checklist from its Development Plan.

## Pre-flight

1. Inspect branch and working tree. Dirty state is informational; preserve unrelated changes.
2. Run:
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 plan -Feature {feature}
   ```
3. If plan is not `Resolved`, show violations. Continue only after fix or explicit `proceed`; record a waiver.
4. Run:
   ```powershell
   dotnet build TheShop.slnx --nologo
   ```
5. Red baseline: halt. Do not implement over an unexplained broken solution.

## Phase loop

For each planned layer:

1. Re-read only that phase and relevant constitution reference.
2. Implement only its listed tasks and necessary same-layer support.
3. Run the narrowest relevant build.
4. Run scope gate:
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 scope -Phase {domain|application|infrastructure|web} -Files "{changed-files}"
   ```
5. Fix focused failures. Maximum two attempts.
6. Record completed tasks, files, build, scope result, and deviations.
7. Continue only when green.

If a later phase exposes an upstream gap, reopen the owning phase once. Re-run its build and scope gate before returning. If still unclear or red, halt; do not invent architecture.

## Layer contracts

### Domain

- Pure business model: entities, value objects, enums, domain rules.
- No UI, persistence, transport, or framework leakage.
- Preserve invariants and existing aggregate conventions.

### Application

- Commands, queries, handlers, DTOs, validators, mappings, interfaces.
- Depend inward only.
- User-visible strings use `.resx`; no hardcoded UI/error text.
- Contract changes must match the plan and downstream consumers.

### Infrastructure

- Implement Application contracts.
- Follow existing Supabase/Postgres patterns.
- Preserve tenant isolation and RLS.
- Never run destructive DDL, reset data, or rewrite migration history without explicit confirmation.
- Migration changes must be additive and reversible where practical.

### Web

Read [visual fidelity — Execute](../theshop-plan/references/visual-fidelity.md).
Load contract images/context; render, capture, compare and correct each surface before leaving Web.
Maximum three correction rounds. Unresolved differences block Implement Done.
Browser inspection/alignment allowed here. Formal tests: Test/E2E.

- Follow planned routes, component boundaries, resources, and Figma intent.
- Reuse project components and MudBlazor conventions.
- Do not invent design values when a design source exists.
- Add stable `data-testid` hooks required by planned E2E journeys.
- Keep business logic outside Razor components.

## Integration gate

After all layers:

```powershell
dotnet build TheShop.slnx --nologo
```

If red, assign failure to one owning phase and reopen it once. Re-run that phase gates, then the solution build. Still red: halt.

Do not write or run feature tests here; `$theshop-test` owns them.
Before marking Done, rerun `python .sdd/scripts/visual-fidelity.py align --feature {feature}`
against final source/captures. Backend skips. Stale captures: rebuild/recapture.

## Tracker

Only after full success, update `.specs/{feature}/status.md`:

- `Implement`: `Done`
- Gate: `✅ solution build + layer scope gates + visual alignment` (backend: record visual skip reason)
- Evidence: compact phase/build summary; include any waiver
- Date: today
- Refresh `Last updated`
- Next: `$theshop-test {feature}`

On halt, do not mark Done. Report completed phase, failure, evidence, and exact resume point.

## Output

Return:

- phases completed
- files changed by phase
- build/scope results
- deviations or waivers
- unresolved blockers
- next command

Success ends with `$theshop-test {feature}`.
