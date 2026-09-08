---
name: theshop-spec
description: "Create product-level feature spec and status ledger; resolve blocking questions, log cheap defaults. Accepts --desc. Explicit invocation only."
disable-model-invocation: true
argument-hint: "<feature-name> [--desc <description>]"
---

<!-- Generated from .sdd/skills/theshop-spec/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/claude/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# Create Spec

Write product WHAT/WHY at `.specs/{feature_name}/spec.md`. Explicit invocation only; never auto-trigger.

Do not add UI choices merely to fill a template: capacity counters, field clearing, toggle semantics, navigation, and reload behavior need supplied requirements or explicit open assumptions. Confirmed supplied choices never confirm these additions. Omit unnecessary choices; retain required but unresolved choices as questions.

## Inputs

```
/theshop-spec <feature-name> [--desc <description>]
```

Before `--desc`: feature name. After it: free-text description; no quoting needed. Without `--desc`, use entire input as name. Empty description means absent.

Missing name, including input starting with `--desc`: ask for a short feature name and optional description. Wait. Never infer name from context or create a generic template.

Treat description as authoritative product input. Include stated requirements, scope, and behavior; never re-ask answered questions. Still classify remaining uncertainty in Step 2. Acknowledge technical direction separately and defer it to `/theshop-plan`. Surface contradictions or multiple features before writing.

## Scope

Write for product, design, QA, and stakeholders without codebase knowledge. Include user actions/goals, business rules, visible outcomes, eligibility, time/quantity limits, messages, completion conditions, and user-experience edge cases.

Exclude endpoints, request/response shapes, database schemas, libraries/frameworks, performance metrics, component names/CSS/markup, code organization/deployment/testing, and infrastructure failure modes. Rewrite technical passages from user viewpoint.

Use observable business terms: an item appears before the next user action, rather than an unmeasurable claim that the cart is fast. Every requirement and AC must be externally checkable. Cross-reference FR IDs when an AC's relationship is unclear.

## Procedure

### 1. Normalize feature name

Use lowercase, hyphen-separated alphanumerics for folder name. Convert spaces/underscores to hyphens; strip special characters. `Add To Cart` becomes `add-to-cart`; `user_authentication` becomes `user-authentication`. Preserve user casing/spacing for title, or Title Case a slug.

### 2. Gather context and classify uncertainty

Read obviously relevant existing specs, README, or product documents. Ask focused WHAT/WHY questions when one or two details materially change the spec.

- **Blocking:** answer changes feature identity/scope or is expensive to reverse. Stop and ask before writing. Never record a blocking choice as an assumption.
- **Resolvable default:** sensible default exists and changing it is cheap. Mark `(Assumption: …)` inline and list it in **Assumptions & Open Questions** for `/theshop-clarify`.
- If a wrong answer invalidates the spec, ask. If it changes one line, assume and mark. When uncertain, ask.

Mark every non-blocking judgment inline and in appendix, including inferred current-state claims and causes. Never present guesses as facts.

Prefer omission over invented background. A user goal alone can fill Problem Statement; do not infer that users currently use paper, memory, spreadsheets, or a broken workflow. Preserve undefined lifecycle terms exactly: `session ends` does not mean browser close, refresh, logout, or timeout unless input says so. Put implementation interpretation in Plan questions. Confirmation of supplied choices never confirms added choices.

Consider every applicability dimension; include only relevant content:

- Roles/access: distinguish guest, registered customer, and admin capabilities.
- Localization: consider English and French for Canadian product/legal needs; record currency, tax wording, date/number differences.
- Accessibility: record observable WCAG expectations, keyboard access, announcements, and visible focus.
- Boundaries: record explicit In Scope / Out of Scope in Section 1.

### 3. Write from canonical template

Read `.claude/skills/theshop-spec/templates/spec-template.md`; follow exactly, never reconstruct from memory. Remove authoring guidance blockquotes.

Preserve six numbered sections; Section 1 Scope/In/Out and Actors & Access; Section 4 Business Rules with `RULE-n`; `**AC-n:**` using **Given …, when …, then …**; appendix below body and above status footer. Preserve FR/AC sequencing. Scope and appendix are unnumbered; add no extra top-level section such as Future Work.

Target 1–3 pages. If growing beyond that, flag possible feature split. Fold repeated prose; retain every requirement and exception.

Page range is guidance, never a minimum. Each distinct required outcome needs explicit AC coverage, including localization, access/privacy, keyboard use, focus, announcements, clearing, and rejection preservation when supplied. Add `Covers FR-n` references where coverage would otherwise be unclear; do not substitute a generic happy-path AC for those outcomes.

### 4. Save and verify

Before saving, check factual fidelity and selected prose level:

- Trace claims and product choices to user input or read context. Omit unsupported history and causes; mark cheap defaults as assumptions. User-confirmed choices do not confirm new inferences.
- Compare requirements, rules, edge cases, and ACs. Preserve identical limits, operation order, exceptions, and rejection behavior throughout. Resolve contradictions before running gates.
- Apply selected Caveman level to prose; preserve schema, literals, and auto-clarity. Structural gate success proves neither factual fidelity nor style.

Create feature directory if absent. Save only `.specs/{feature_name}/spec.md`. Existing spec: ask overwrite or cancel. Never create `spec-v2.md`; use git history (`git log -- .specs/{feature_name}/spec.md`) for prior revisions.

On overwrite, reset downstream status rows (Plan, Implement, Test, Verify, Review, Document) to `—`; add `stale: spec rewritten {date}` in Gate cells that previously had results. Clarify updates Spec row; no separate Clarify row exists. Keep existing `plan.md` / `test-manifest.json` on disk; reconcile through amendment/evidence contract before continuation.

  ```bash
  pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 spec -Feature {feature_name}
  ```

Gate verifies template structure, rule/FR/AC sequencing, Given/When/Then, appendix, and footer count. Exit 1: fix and rerun. Never report saved success while gate fails.

### 5. Initialize or update ledger

Read `references/status-tracker.md` before writing `.specs/{feature_name}/status.md`; use its exact template and vocabulary. Set Spec: `Draft`, `✅ spec-gate pass`, FR/AC/open-assumption counts, today's date. Set later rows to `—`; Next step: `/theshop-clarify {feature_name}`.

Existing ledger: update Spec rather than replacing whole file; apply Step 4 resets. Preserve entry checks, waivers, row ownership, and missing-ledger backfill from the reference.

## Outputs and completion evidence

Save spec and ledger only after blocking questions are resolved. Report saved path and open-assumption count briefly. Open assumptions: point to `/theshop-clarify`; none: offer refinement. Required spec gate must pass before completion.

## References

- **Spec template:** `.claude/skills/theshop-spec/templates/spec-template.md`; required in Step 3.
- **Status tracker template:** `references/status-tracker.md`; required for ledger creation/update, including callers from later workflows.
- **Examples:** `references/invocation-examples.md`; only when interaction examples are needed.
