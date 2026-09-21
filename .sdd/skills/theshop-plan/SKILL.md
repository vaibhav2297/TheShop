---
name: theshop-plan
description: Create technical plan at .specs/{slug}/plan.md from confirmed feature spec. Use after $theshop-clarify.
argument-hint: <feature-name> [--desc <technical direction>] [--figma <url|node-id>]
disable-model-invocation: true
---

# $theshop-plan

Input: `$ARGUMENTS`.

Create `.specs/{slug}/plan.md`. Technical HOW only. Next: `$theshop-resolve {slug}`.

## Input

Parse `<feature-name> [--desc <technical direction>] [--figma <url|node-id>]`. Flags may appear either order.

No name: ask. Stop.

Require `.specs/{slug}/spec.md`. Missing: stop. Send user to `$theshop-spec`.

`--desc`: technical direction. Use it only if spec scope and constitution allow it. Product-scope change: stop; send user to `$theshop-spec` or `$theshop-clarify`.

`--figma`: URL or node ID. Use only for UI work.

## Before write

Read spec. Empty, vague, conflicting, or blocking product gap: stop and ask.

Spec still Draft: warn. Continue only with explicit user approval. Carry unresolved items into plan Section 11.

Load `$theshop-constitution`. Read only routed references matching planned layers.

Inspect existing code only for reuse, naming, and layer placement. Do not invent types, routes, or UI primitives.

UI work: use supplied Figma reference. No reference: ask once for URL/node ID or `skip`. `skip`: record missing design reference in Section 11. Backend work: skip Figma.

## Write

Read `templates/plan-template.md`. Follow exactly.

Follow `.sdd/README.md` artifact writing style for SDD prose.

Required:

- Eleven numbered sections.
- Concrete layer, type, contract, migration, validation, error-key, test, route, busy-state, and UI decisions only when needed.
- Section 7 `TASK-nnn`: unique, continuous from `TASK-001`, one committable outcome each.
- Complete layers in `$theshop-execute` order: Domain, Application, Infrastructure, Web. Keep Application contracts stable after Application work.
- Section 8 maps every spec AC to defined TASK ids.
- Section 9 covers every spec rule and edge case.
- Section 10 uses literal runnable SQL and RLS when persistence changes.
- Section 11 uses `❓`, `⚠️`, `📌`.
- Remove template guidance before save.

Existing `plan.md`: ask overwrite or cancel. Never create versioned plan file.

Overwrite: retain history in Git. Mark Implement, Test, Verify, Document rows stale. Existing downstream files require regeneration.

## Gate and tracker

Save `.specs/{slug}/plan.md`. Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 plan -Feature {slug}
```

Gate fails: fix, rerun. Never report saved plan while red.

Update `.specs/{slug}/status.md`. Missing: create from `$theshop-spec` status template.

Set Plan: `Draft`; gate `✅ plan-gate pass`; evidence: AC mapping and Section 11 counts; today. Set Next step `$theshop-resolve {slug}`.

If user approved Draft spec: record `⚠️ waived: spec Draft, {N} open assumption(s)`.

## Output

```markdown
Saved `.specs/{slug}/plan.md`.

{N} open question(s) or assumption(s). Next: `$theshop-resolve {slug}`.
```

No open items: state `$theshop-execute {slug}` available. Do not claim resolved plan before `$theshop-resolve`.