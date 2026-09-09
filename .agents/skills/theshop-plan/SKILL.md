---
name: theshop-plan
description: "Create technical implementation plan from feature spec, with AC/task mapping and optional --desc/--figma input. Explicit invocation only."
---

<!-- Generated from .sdd/skills/theshop-plan/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/codex/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# Create Plan

Read feature spec; write technical HOW at `.specs/{feature_name}/plan.md`. Explicit invocation only.

## Inputs

```
$theshop-plan <feature-name> [--desc <description>] [--figma <url|nodeId>]
```

Before first `--` flag: feature name. Flags accept either order; each value ends at next flag or input end. Empty flag means absent. Strip supplied `.md`; require matching `.specs/{file_name}/spec.md`.

Missing name: ask which spec, then wait. Missing spec: report path, direct user to `$theshop-spec`, and halt.

**`--desc`:** user technical direction, reuse choices, schema constraints, and exclusions. Incorporate in relevant sections; cite where it settles a choice. It steers HOW, never expands product scope. New behavior requires spec update through `$theshop-spec` / `$theshop-clarify` first. Constitution violation: stop and raise it. Contradictions: record in Section 11, or ask first when load-bearing.

**`--figma`:** full URL (extract file key and `node-id`) or comma-separated `node:id` values. Fetch correct file/starting frames and verify children. Supplied input skips Step 4's initial Figma question. Read `references/flag-examples.md` only for concrete flag examples.

## Scope

Plan for developers: layer architecture, MediatR commands/queries, repository interfaces, tables/columns/indexes/RLS, validators, error keys, exceptions, `Result<T>`, file changes, ordered tasks, and concrete signatures/key methods.

Do not restate product behavior, marketing, business motivation, personas, or customer journeys. No full implementation. Preserve spec scope and use actual project names: `ShopColors`, `ShopIcons`, `Strings.{KeyName}`, `Result<T>`, `nameof(Strings.X)`, `MediatR`, `MudBlazor`.

## Procedure

### 1. Plan deliberately

Check consequences for implementation, tests, and review before drafting. Use authorized runtime model/reasoning settings; preserve rigor across providers.

### 2. Read entire spec

- Problem Statement informs Objective.
- Every FR maps to development tasks; each behavior maps to layered flow.
- Every constraint maps to Section 5 decision, Section 9 validation, or Section 10 database constraint.
- Every edge/error case gets Section 9 handling: validator, `Result.Fail(...)` key, or exception.
- Every AC maps to at least one task. Flag uncertain coverage in Section 11; never omit ACs.

Read Status and appendix. `Draft — N open assumption(s)` with unresolved `📌` / `❓`: warn about unconfirmed defaults; offer `$theshop-clarify` or proceeding. Wait for explicit go-ahead. If authorized, carry every open assumption into Section 11 as `📌 Assumption` and record ledger waiver.

Empty, vague, or contradictory spec section: stop and ask before planning. Never invent behavior.

### 3. Load governing rules

Invoke `theshop-constitution`; follow its rule/reference routing rather than searching bare filenames or scanning whole project. Read Rules 1–30 and `references/rules/architecture-core.md`. Add `architecture-patterns.md` for Application/Infrastructure, `architecture-admin.md` for admin, and relevant `design-*.md` for UI. Read `AGENTS.md` if absent from context.

### 4. Resolve design input when applicable

For any UI feature, read `references/design-input.md` and follow its full conditional procedure. Capture canonical Figma URL, every affected page/component node ID, and visual intent in Section 7 Step 5 (Web). Missing/ambiguous IDs become Section 11 questions. Supplied `--figma` skips asking for it again. Backend/domain-only work can skip both integrations.

### 5. Resolve architecture before drafting

Determine affected layers; named MediatR signatures; new/extended Domain entities, value objects, and exceptions; tables, columns, indexes, RLS; error/UI resource keys; and AC verification risks. Error keys require `Strings.resx` plus French placeholder in `Strings.fr.resx`. UI keys include titles, buttons, errors, and validation messages.

Check reuse through graph when `graphify-out/graph.json` exists: `graphify query "<what exists related to {feature}>"`; use `graphify explain "<concept>"` / `graphify path "<A>" "<B>"` for focused follow-ups. Read surfaced files. Scan relevant folders only if graph absent or query irrelevant. Sketch decisions internally before drafting.

### 6. Write canonical plan

Read `.agents/skills/theshop-plan/templates/plan-template.md`; follow exactly, never reconstruct from memory. Remove guidance blockquotes and replace placeholders.

- Eleven numbered sections with template title keywords.
- Section 7: agent-aligned steps for Domain, Application, Contract Freeze, parallel Infrastructure/Web, then Integration. Continuous `TASK-nnn` IDs from 001; never reset. Preserve per-step gates and deviation procedure. Unaffected layer: one-line skip, no empty scaffolding.
- Every step ends buildable; each TASK is one committable outcome.
- UI: Figma references in Section 7 Step 5.
- Section 8: every spec AC mapped to defined TASK IDs.
- Section 11: `❓` / `⚠️` / `📌` labels consumed by `$theshop-resolve`.

Target 3–6 pages. Longer plan: flag oversized feature or excessive implementation detail; preserve necessary facts.

### 7. Save and verify

Before saving, compare plan against every spec requirement and AC. Preserve limits, validation order, rejection state, access, and lifecycle terms. Do not narrow `session ends` to sign-out alone or claim structural isolation without verified implementation evidence. Unverified reuse and lifecycle mechanisms belong in Section 11. Test tasks belong to test specialists, even when listed beside their production layer. Document remains manually invoked; never include automatic documentation in execution tasks.

Save `.specs/{file_name}/plan.md` beside spec; create folder if missing. Existing plan: ask overwrite or cancel. Never create `plan-v2.md`; git history (`git log -- .specs/{file_name}/plan.md`) holds prior versions.

On overwrite, reset Implement, Test, Verify, Review, Document rows to `—`; add `stale: plan rewritten {date}` in Gate cells that previously had results.

  ```bash
  pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 plan -Feature {file_name}
  ```

Gate checks eleven sections/footer, present/unique/sequential Section 7 TASK IDs, and complete Section 8 mapping from spec Section 6 ACs to defined tasks. Exit 1: fix and rerun. Never report saved success while gate fails.

### 8. Update ledger

Set `.specs/{file_name}/status.md` Plan row: `Draft`, `✅ plan-gate pass`, AC coverage and Section 11 question/assumption/risk counts, today's date. If proceeding on Draft spec, use `⚠️ waived: spec Draft, {N} open assumption(s)` instead. Refresh **Last updated**.

Set **Next step** to `$theshop-resolve {file_name}`, or `$theshop-implement {file_name}` if Section 11 has no open questions. Missing ledger: read `.agents/skills/theshop-spec/references/status-tracker.md` and create from its template.

## Outputs and completion evidence

Report saved path and decisions needing human judgment. Any `❓` or unratified `📌`: point to `$theshop-resolve` before implementation. Preserve passing gate and complete AC mapping; never hide unknowns.

## Examples

Read `references/invocation-examples.md` only when interaction examples are needed.
