---
name: theshop-spec
description: Create product spec at .specs/{slug}/spec.md. Use after $theshop-start. Product WHAT/WHY only; no technical plan.
argument-hint: <feature-name> [--desc <description>]
disable-model-invocation: true
---

# $theshop-spec

Input: `$ARGUMENTS`.

Create `.specs/{slug}/spec.md`. Next: `$theshop-clarify {slug}`.

## Input

Parse: `<feature-name> [--desc <description>]`.

No name: ask. Stop.

Read [feature identity](../theshop-start/references/feature-identity.md).
Run `feature_identity.py resolve`; preserve full ID as `{slug}`.
New spec requires numbered Start branch. Tracked legacy specs stay valid. Title: readable feature name.

`--desc` is product input. Use stated facts. Ignore technical design; send it to `$theshop-plan`.

## Scope

Product WHAT and WHY. No API, schema, library, component, deployment, or performance design.

Ask blocking product questions before writing. Blocking means answer changes feature identity, access, scope, or expensive decision.

Cheap unknown: choose default. Mark `(Assumption: ...)` in body and appendix.

Check only applicable: actors/access, English/French, accessibility, scope boundaries.

UI work: read [visual fidelity — Spec / Clarify](../theshop-plan/references/visual-fidelity.md).
Record frames, viewports, states, responsive behavior in existing sections.
Add visual Given/When/Then ACs. Missing behavior: explicit product question.

## Write

Read `templates/spec-template.md`. Follow exactly.

Follow `.sdd/README.md` artifact writing style for SDD prose.

Required:

- Six numbered sections only.
- Scope and Actors & Access in section 1.
- Sequential `FR-n`, `RULE-n`, `AC-n`.
- Business Rules in section 4.
- Every AC: Given, when, then.
- Assumptions appendix and Status footer.
- Remove template guidance before save.

Existing `spec.md`: ask overwrite or cancel. Never create versioned spec file.

Overwrite: preserve old file in Git history. Mark Plan, Implement, Test, Verify, Document rows stale. Existing downstream files require regeneration.

## Gate and tracker

Save `.specs/{slug}/spec.md`. Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 spec -Feature {slug}
```

Gate fails: fix, rerun. Never report saved spec while red.

Create or update `.specs/{slug}/status.md` from `templates/status-template.md`.

Set Spec: `Draft`; gate `✅ spec-gate pass`; evidence: FR, AC, open-assumption counts; today. Later rows stay `—`. Set Next step `$theshop-clarify {slug}`.

## Output

```markdown
Saved `.specs/{slug}/spec.md`.

{N} open assumption(s). Next: `$theshop-clarify {slug}`.
```

No open assumptions: omit first line. Do not claim completion before gate passes.
