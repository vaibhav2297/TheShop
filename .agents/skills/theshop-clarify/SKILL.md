---
name: theshop-clarify
description: "Resolve product assumptions in one feature spec; incorporate confirmed decisions and update status. Explicit invocation only."
---

<!-- Generated from .sdd/skills/theshop-clarify/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/codex/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# Clarify Spec

Resolve one spec's assumptions with user. Incorporate decisions into body before marking `Confirmed`. Explicit invocation only.

## Inputs

Require feature name matching `.specs/{feature_name}/spec.md`. Strip trailing `.md`; normalize lowercase-hyphenated name as in `theshop-spec`.

Missing name: ask and wait; never guess from context. Missing spec: report path, direct user to `$theshop-spec {name}`, and halt.

## Scope

Product WHAT/WHY only, readable without codebase knowledge. No endpoints, schemas, libraries, or performance metrics. Translate technical answers into visible behavior; implementation belongs in `$theshop-plan`.

Edit one spec only. Note cross-feature dependencies in Constraints or inline and final report; never edit another spec. Target 1–3 pages; fold decisions into existing sentences.

## Procedure

### 1. Read spec and collect open items

Read full spec. Collect appendix `📌 Assumption` / `❓ Open question` items plus inline `(Assumption: …)` markers absent from appendix. Reconcile duplicates: body locates decisions; appendix indexes them. Record affected sections for each item.

### 2. Short-circuit when no work remains

Only an empty worklist with consistent `Confirmed` footer can short-circuit. Run spec/status gates before reporting no work. A `Confirmed` footer with open markers is inconsistent: process those items. Empty worklist with Draft footer requires Step 5–6 reconciliation, without inventing questions or user decisions.

### 3. Resolve one decision at a time

Follow document order. Ask focused WHAT/WHY question; offer logged default as *(Recommended)*, realistic alternatives, and “let me decide for you.” Use choices for discrete answers; plain language for open-ended answers.

- Confirm: ratify default.
- Override: record user's value.
- “You decide” / “use your judgment”: accept default; do not leave unresolved.
- Deferral to a later pass: leave item open.

Never batch unrelated questions. Tightly related items may share a turn if separately answerable. Resolve exposed blocking scope/identity gaps immediately; add new cheap sub-defaults to worklist. Never retain a load-bearing guess as a cheap default.

### 4. Incorporate each decision before removing its marker

1. State settled fact in relevant Sections 1–6. Replace inline assumption marker. Update Scope for boundary changes, Constraints for rules, Functional Behaviors/Edge Cases for visible behavior, and ACs when completion conditions change.
2. Remove resolved item from appendix only after body records decision.
3. Keep appendix for still-open items. Never erase a decision or grow prose unnecessarily.

### 5. Recount and update footer

Count remaining open items (`N`).

- `N = 0`: appendix reads `None — all assumptions confirmed.`; use footer:

  ```
  **Status:** Confirmed   ·   **Created:** {original date}   ·   **Clarified:** {today YYYY-MM-DD}
  ```

- `N > 0`: retain unresolved items and `Draft — N open assumption(s)` with actual reduced count.

Preserve **Created** date; add/update **Clarified** only. Never mark `Confirmed` with open items.

### 6. Save, gate, and update ledger

Save same spec path. Run mandatory exit gate:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 spec -Feature {feature_name}
```

Gate checks structure, footer count, and empty appendix for `Confirmed`. Exit 1: fix and rerun; never claim confirmation while gate fails.

Update `.specs/{feature_name}/status.md` Spec row: `Confirmed` only at `N = 0`, otherwise `Draft`; Gate `✅ spec-gate pass`; resolved/open counts; today's date. Refresh **Last updated**. **Next step:** Plan only at `N = 0`; otherwise another Clarify pass. Missing ledger: read `.agents/skills/theshop-spec/references/status-tracker.md` and create from its template first.

## Outputs and completion evidence

Report resolved count, notable changes (especially scope), new Status, and remaining items. State planning unblocked only when fully confirmed. Otherwise name open items and point to another clarify pass. Keep decision in body, appendix/footer counts consistent, and required gate passing.

## Examples

Read `references/invocation-examples.md` only when interaction examples are needed.
