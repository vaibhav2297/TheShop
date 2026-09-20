---
name: theshop-resolve
description: Resolve technical questions, assumptions, and risks in one feature plan, then gate it.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Resolve Plan

Resolve open engineering decisions in `.specs/{feature}/plan.md`.

Stay at **HOW**, but keep a plan—not implementation.

## Input

Require one safe feature folder name. Strip optional `.md`; normalize lowercase-hyphenated.

Missing plan: halt; direct to `$theshop-plan {feature}`.

Read the full plan. Collect:

- Section 11 `❓ Open question`, `📌 Assumption`, and `⚠️ Risk`
- unmatched inline `📌` markers

Map each item to its destination in Sections 1–10: model/schema, design, flow, validation, RLS, AC mapping, or phase task.

If no open item exists or footer is already `Resolved`, report and stop.

## Resolve

Walk document order. Keep each decision separately answerable.

### Open question

Must be answered. Offer a recommended engineering choice plus realistic alternatives and “use your judgment.”

### Assumption

Allow:

- confirm default
- override
- delegate: accept default

All three resolve it.

### Risk

Choose:

- mitigate: add concrete mitigation to the owning section/task; remove risk line
- accept: retain as `⚠️ Risk — ✅ Accepted: {rationale}`

Accepted risks stay visible but do not block Resolved.

If a decision exposes a deeper architectural gap, resolve it before continuing.

## Fold decisions

For every settled item:

1. Write the decision into relevant Sections 1–10.
2. Update affected model, schema/RLS, validation, flow, AC mapping, or phase tasks.
3. Remove its inline/Section 11 marker, except accepted risks.

Never delete a decision without preserving it in the body. Fold; do not append bloat.

If the plan cannot satisfy the spec, do not edit the spec. Halt and route to `$theshop-clarify`, then re-plan.

## Status

Count remaining open questions and unratified assumptions. Accepted risks do not count.

Zero:

- Section 11: `None — all questions resolved.`, followed by accepted risks
- footer: `Status: Resolved`
- preserve Created; add/update Resolved date

Remaining:

- keep `Status: Draft`
- leave unresolved items visible

Never mark Resolved while a `❓` or unratified `📌` remains.

## Gate and tracker

Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 plan -Feature {feature}
```

Red: fix and rerun. Never resolve over a red gate.

Update `status.md`:

- Plan `Resolved` only at zero open; otherwise `Draft`
- Gate `✅ plan-gate pass`
- Evidence: resolved/open/accepted-risk counts
- Date today; refresh `Last updated`
- Next: `$theshop-execute {feature}`

If tracker is missing, create it from the `theshop-spec` status template.

## Output

Report decisions, risk dispositions, remaining count, material schema/design changes, status, and next command.

Edit this plan only.
