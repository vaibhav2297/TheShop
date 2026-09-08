---
name: theshop-resolve
description: "Resolve technical questions, assumptions, and risks in one feature plan; incorporate decisions and update status. Explicit invocation only."
---

<!-- Generated from .sdd/skills/theshop-resolve/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/codex/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# Resolve Plan

Resolve one plan's Section 11 questions, risks, and assumptions with user. Incorporate decisions into body before marking `Resolved`. Explicit invocation only.

## Inputs

Require feature name matching `.specs/{feature_name}/plan.md`. Strip trailing `.md`; normalize lowercase-hyphenated name. Missing name: ask and wait; never guess from context. Missing plan: report path, direct user to `$theshop-plan {name}`, and halt.

## Scope

Technical HOW: endpoints, schemas, concurrency, RLS, validators, library choices. No product/marketing rewrite or full implementation. Edit one plan only; target 3–6 pages by folding decisions into existing sections.

Spec contradiction: flag and direct user to `$theshop-clarify` or spec update, then re-plan. Never edit spec or related plans here.

## Procedure

### 1. Read plan and collect open items

Read full plan. Collect Section 11 `❓ Open question`, `⚠️ Risk`, and `📌 Assumption`, plus inline `📌 Assumption` markers absent from Section 11. Record destination sections:

- Data shape/entity/table: Sections 4 and 10.
- Behavior/architecture: Section 5.
- Failure/validation: Section 9.
- Access/tenancy: Section 10 RLS.
- Sequencing/new work: Section 7.

### 2. Short-circuit when no work remains

Only an empty unresolved worklist with consistent `Resolved` footer can short-circuit. Accepted risks remain visible but are not unresolved work. Run plan/status gates before reporting no work. A `Resolved` footer with unanswered questions, unratified assumptions, or undispositioned risks is inconsistent: process those items. Empty worklist with Draft footer requires Step 5–6 reconciliation without inventing decisions.

### 3. Resolve one decision at a time

Follow document order; keep each decision separately answerable. Tightly related items may share a turn; never batch unrelated questions.

- **Open question:** ask focused technical question. Offer engineering default as *(Recommended)* when available, realistic alternatives, and “use your judgment.” Every question must be answered before `Resolved`.
- **Assumption:** recommend logged default. Confirm ratifies it; override records user's value; “you decide” accepts default. Deferral to a later pass leaves it open.
- **Risk:** ask for disposition. Mitigate: record concrete mitigation in relevant decision/task/constraint, then remove risk from Section 11. Accept: retain `⚠️ Risk — ✅ Accepted: {one-line rationale}` in Section 11. Accepted risks remain visible and do not block `Resolved`.

Resolve deeper architectural gaps immediately; add newly exposed cheap sub-decisions to worklist. Pin vague answers to concrete engineering decisions before recording them.

### 4. Incorporate each decision before removing its marker

1. State settled decision in Sections 1–10; replace inline `📌`. Update Section 9 for rules, Sections 4/10 for schema, Section 7 for tasks, Section 6 for flows, and Section 8 when `AC → Task` mapping changes.
2. Remove resolved item from Section 11, except relabeled Accepted risks.
3. Never leave a decision only in Section 11; it indexes open items and accepted risks. Fold wording rather than append repetition.

### 5. Recount and update footer

Count remaining `❓` questions and unratified `📌` assumptions. Accepted risks do not count. Every remaining risk still requires Accepted disposition.

- Count zero: Section 11 reads `None — all questions resolved.` followed by any `✅ Accepted` risks. Use footer:

  ```
  **Status:** Resolved · **Spec:** `.specs/{file_name}/spec.md` · **Created:** {original date} · **Resolved:** {today YYYY-MM-DD}
  ```

- Count above zero: retain `Draft`, open items, and report remaining count.

Preserve **Created** date; add/update **Resolved** only. Never mark `Resolved` with unanswered questions, unratified assumptions, or undispositioned risks.

### 6. Save, gate, and update ledger

Save same plan path. Run mandatory exit gate:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 plan -Feature {feature_name}
```

Gate checks eleven sections, AC coverage, zero open questions/unratified assumptions for `Resolved`, and Accepted disposition for remaining risks. Exit 1: fix and rerun; never claim resolution while gate fails.

Update `.specs/{feature_name}/status.md` Plan row: `Resolved` only after resolution conditions hold, otherwise `Draft`; Gate `✅ plan-gate pass`; resolved count and accepted risks; today's date. Refresh **Last updated**. **Next step:** Implement only when fully resolved; otherwise another Resolve pass. Missing ledger: read `.agents/skills/theshop-spec/references/status-tracker.md` and create from its template first.

## Outputs and completion evidence

Report resolved count, notable changes (especially schema/architecture), each risk disposition, new Status, and remaining items. State implementation unblocked only when fully resolved. Preserve decisions in body and passing gate evidence.

## Examples

Read `references/invocation-examples.md` only when interaction examples are needed.
