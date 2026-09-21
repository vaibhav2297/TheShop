---
name: theshop-clarify
description: Resolve product-level assumptions and questions in one feature spec, then gate and confirm it.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Clarify Spec

Resolve open product decisions in `.specs/{feature}/spec.md`.

Stay at **WHAT/WHY**. Implementation belongs to `$theshop-plan`.

## Input

Require one safe feature folder name. Strip optional `.md`; normalize lowercase-hyphenated.

Missing spec: halt; direct to `$theshop-spec {feature}`.

Read the full spec. Collect:

- appendix `📌 Assumption` and `❓ Open question` items
- unmatched inline `(Assumption: ...)` markers

Note which Sections 1–6 each item affects.

If nothing is open or footer is already `Confirmed`, report and stop.

## Resolve

Walk document order. Ask one focused product question at a time.

For each item:

- offer logged/default value as **Recommended**
- offer realistic alternatives
- allow “use your judgment”

Responses:

- confirm: use default
- override: use supplied value
- delegate: accept default

A delegated/default decision is resolved, not deferred.

If an answer exposes a load-bearing scope gap, resolve it now. Keep questions separately answerable.

Never ask about storage, endpoints, schema, libraries, or other HOW. Translate technical answers into user-visible behavior.

## Fold decisions

Follow `.sdd/README.md` artifact writing style for SDD prose.

For each answer:

1. Rewrite relevant body text as settled fact.
2. Update scope, behaviors, constraints, edge cases, and ACs only where affected.
3. Remove its appendix and inline assumption markers.

Decisions live in Sections 1–6. Never delete an item without preserving its resolved meaning.

Fold into existing prose; do not bloat the spec.

## Status

Recount open items.

Zero:

- appendix: `None — all assumptions confirmed.`
- footer: `Status: Confirmed`
- preserve Created; add/update Clarified date

Remaining items:

- keep `Status: Draft — N open assumption(s)`
- leave those items visible
- preserve Created

Never mark Confirmed with open items.

## Gate and tracker

Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 spec -Feature {feature}
```

Red: fix and rerun. Never confirm over a red gate.

Update `status.md`:

- Spec `Confirmed` only at zero open; otherwise `Draft`
- Gate `✅ spec-gate pass`
- Evidence: resolved count + remaining count
- Date today; refresh `Last updated`
- Next: `$theshop-plan {feature}`

If tracker is missing, create it from the `theshop-spec` status template.

## Output

Report resolved count, remaining count, material scope changes, status, and next command.

Edit this spec only. Cross-feature dependency: note it here; do not edit another spec.
