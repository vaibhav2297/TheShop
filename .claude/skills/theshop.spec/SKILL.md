---
name: theshop.spec
description: Generate a non-technical specification document for a single feature and save it to `.specs/{feature_name}/spec.md`. The spec is product-level only — focused on WHAT the feature does and WHY it matters, never on HOW it's built. It contains six fixed numbered sections (problem statement, functional requirements, functional behaviors, constraints, edge cases and error handling, acceptance criteria), an In/Out of Scope block inside Section 1, and an Assumptions & Open Questions appendix that the `/theshop.clarify` skill later resolves. Blocking, load-bearing questions are asked up front; only cheap-to-change defaults are assumed and logged. This skill is manually invoked only (typically via slash command) and requires a feature name from the user.
disable-model-invocation: true
---

# Create Spec

Generate a non-technical feature specification and save it to `.specs/{feature_name}/spec.md`.

This skill is **manually invoked only**. It does not auto-trigger from conversational cues — the user must explicitly call it (e.g., via a slash command).

## Core principle: WHAT and WHY only — never HOW

This spec is a **product document**, not an engineering document. The audience is anyone who needs to understand what the feature does and why — product, design, QA, stakeholders. They should not need to know the codebase to read it.

Stay strictly in WHAT/WHY territory:

| Stay in (✅) | Stay out of (❌) |
|---|---|
| User actions and goals | API endpoints, request/response shapes |
| Business rules and policies | Database schemas, table designs |
| User-visible behavior and outcomes | Libraries, frameworks, tech stack |
| Time/quantity/eligibility in user terms ("within 1 business day", "for orders over $50") | Performance metrics ("200ms p95", "throughput", "memory usage") |
| What the user sees, hears, or is told | UI component names, CSS, layout markup |
| What must be true for the feature to be considered done | How code is organized, deployed, or tested |
| User-experience edge cases ("what if the cart is empty?") | Infrastructure failure modes ("what if the database is down?") |

If a section starts pulling toward implementation, rewrite it from the user's point of view. A good test: **could a non-developer product manager read this and fully understand the feature?** If no, it's gone technical.

## Inputs

The skill takes one required input: a **feature name**.

- If the user provided a feature name when invoking the skill, use it.
- If the user did **not** provide a feature name, stop and ask:

  > "Which feature should I create the spec for? Please give me a short feature name (e.g., `add-to-cart`, `user-authentication`)."

  Wait for the reply before doing anything else. Do not invent a feature name, do not pick one from recent conversation context, and do not generate a generic template. The feature name must come from the user.

## Workflow

### 1. Normalize the feature name

- Filename form: lowercase, hyphen-separated, alphanumerics and hyphens only. Strip spaces, underscores, and special characters.
  - `Add To Cart` → `add-to-cart`
  - `user_authentication` → `user-authentication`
- Title form (for the document heading): keep the user's casing and spacing, or Title Case it if they gave a slug.
  - `add-to-cart` → `Add To Cart`

### 2. Gather just enough context

Before writing, take a quick pass for context — but don't turn this into a long interview.

- Glance at related files in the workspace if obviously relevant (existing specs in `.specs/`, project README, product docs).
- If one or two product-level details would meaningfully change the spec, ask focused questions. Keep questions in WHAT/WHY territory:
  - ✅ "Does this apply to logged-out users too, or only signed-in?"
  - ✅ "Should the cart persist across sessions?"
  - ❌ "Should we use localStorage or a backend session?" (that's HOW)
- **Classify every uncertainty before you resolve it — this is what decides whether you ask or assume:**
  - **Blocking / load-bearing** — the answer changes the feature's identity or scope, or guessing wrong would be expensive to reverse (e.g., "guests vs. signed-in only?", "does this touch the admin panel?", "is age-gating in scope?"). **Stop and ask up front, before writing.** These are *never* allowed to become assumptions.
  - **Resolvable default** — a sensible default exists and a wrong guess is cheap to change later (e.g., "cart persists 30 days for guests"). **Assume it, mark it inline with `(Assumption: …)`, and log it in the Assumptions & Open Questions appendix** so the user can ratify or override it. The `/theshop.clarify` skill walks that list.
  - The test: *if getting it wrong would invalidate the spec, ask; if it would change one line later, assume-and-mark.* When genuinely in doubt, ask — the appendix is for cheap defaults, not for dodging hard questions.

**Applicability checklist — consider each dimension; add a line only when it actually applies (don't force empty sections):**

- **Roles / actors** — does behavior differ for guest vs. registered customer vs. admin? If so, name who can do what. (The Shop has a full admin panel — *who is allowed* is product-level, not just implementation.)
- **Localization** — which languages must the UI support? For a premium Canadian shop, English **and** French is typically a product/legal requirement, not a detail. Note any locale-specific behavior (currency, tax wording, date/number formats).
- **Accessibility** — are there WCAG-level expectations a tester could check by hand (keyboard-reachable, screen reader announces cart/checkout changes, visible focus)? Keep it user-observable, not technical.
- **Scope boundaries** — what is explicitly **not** part of this feature? Capture it in the In Scope / Out of Scope block in Section 1. Undefined scope is the single biggest source of rework.

### 3. Write the spec using the template below

Use the exact six numbered sections — don't add or drop a numbered section. The **In Scope / Out of Scope** block (inside Section 1) and the **Assumptions & Open Questions** appendix (below the status line) are part of the fixed template, not extra sections.

### 4. Save the file

- Path: `.specs/{feature_name}/spec.md` (lowercase hyphenated folder, generic `spec.md` file name)
- Create the `.specs/{feature_name}/` directory if it does not already exist. This is the feature's home folder — its plan, test manifest, and status tracker all live here too.
- If a `spec.md` already exists in that folder, ask the user whether to overwrite, save with a version suffix (e.g., `spec-v2.md`), or cancel.
- **Run the template gate (exit gate — mandatory).** After saving, run:

  ```bash
  pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 spec -Feature {feature_name}
  ```

  The script deterministically verifies the six numbered sections, the In/Out-of-Scope block, FR/AC id sequencing, the Assumptions appendix, and that the Status footer's `N` matches the appendix count. **Exit 1 → fix the spec and re-run the gate. Never report the spec as saved while this gate fails.** Record the gate result in the status tracker (next step).

### 5. Initialize the status tracker

Write `.specs/{feature_name}/status.md` — the feature's at-a-glance SDD pipeline tracker **and gate ledger** — using the **Status tracker template** at the end of this file. Set the **Spec** row: State `Draft`, Gate `✅ spec-gate pass` (it must pass before you get here), Evidence one line of counts (e.g. `9 FRs · 12 ACs · 2 open assumptions`), today's date. Leave every later stage as `—`, and point **Next step** at `/theshop.clarify {feature_name}`. If a `status.md` already exists (e.g., the spec is being regenerated), update the Spec row rather than overwriting the whole file.

### 6. Confirm

Report the saved path in one short sentence. If the spec has open assumptions, point the user at `/theshop.clarify`; otherwise just offer to refine. Examples:

> "Saved to `.specs/add-to-cart/spec.md` — 2 open assumptions logged. Run `/theshop.clarify add-to-cart` to resolve them, or tell me to tighten any section."

> "Saved to `.specs/add-to-cart/spec.md` — no open assumptions. Want me to tighten any section?"

## Spec template

Use this exact structure. Replace placeholders in `{curly braces}`. Every section stays product-level.

```markdown
# {Feature Title}

## 1. Problem Statement

{2–4 sentences in plain language: who has the problem, when it occurs, and why it matters. Frame it from the user's or business's perspective — not the system's. Name the user, the scenario, and the cost of leaving it unsolved.}

**Solution (one line):** {A single sentence describing what the feature will do for the user. No mention of how it's built.}

**In scope:** {1–3 bullets naming what this feature explicitly includes.}
**Out of scope:** {1–3 bullets naming what it explicitly does **not** cover — the boundary that stops scope creep. Write "None" only if there is genuinely nothing to exclude.}

## 2. Functional Requirements

{Numbered list of what the feature must do, written as user-visible or business-observable statements. Each item is complete and testable from the outside, without looking at code.}

Examples of the right level:
- ✅ "Users can add a product to their cart from the product detail page."
- ✅ "The cart shows the running total updated in real time as items are added or removed."
- ❌ "The system calls the /cart/add endpoint and updates the Redux store." (too technical)

1. **FR-1:** ...
2. **FR-2:** ...
3. **FR-3:** ...

## 3. Functional Behaviors

{For each significant user interaction, describe what the user does and what the user observes in response. Input is a user action or business event, not a payload. Output is what the user sees or experiences, not a response object.}

### Behavior 1: {Short name, e.g., "Add an item to the cart"}
- **User does:** {The user-facing action — e.g., "Clicks 'Add to Cart' on a product page after choosing a quantity."}
- **User sees:** {The observable result — e.g., "The cart icon updates with the new item count, and a brief confirmation message appears."}

### Behavior 2: {Name}
- **User does:** ...
- **User sees:** ...

## 4. Constraints

{Bullet list of business, policy, regulatory, or user-experience constraints the feature must respect. Numbers belong here when they're user-facing or policy-driven — never performance or infrastructure numbers.}

Examples of the right kind of constraint:
- ✅ "Users must be 19 or older to purchase (Ontario regulation)."
- ✅ "A cart can hold a maximum of 20 distinct items."
- ✅ "Discounts cannot stack — only the largest applicable discount applies."
- ❌ "Must respond within 200ms." (technical performance, belongs in engineering docs)
- ❌ "Use Postgres for storage." (implementation choice)

- ...
- ...

## 5. Edge Cases & Error Handling

{Bullet list of user-experience edge cases and abnormal scenarios, each paired with what the user sees or experiences. Frame everything from the user's perspective — not the system's. Don't list infrastructure failures; list the user-visible consequences.}

Examples of the right framing:
- ✅ "User tries to add an out-of-stock item → The 'Add to Cart' button is disabled and shows 'Out of stock'."
- ✅ "User adds the last available unit while another user is checking out with it → User sees a 'No longer available' message and the item is removed from their cart."
- ❌ "Inventory service times out → Retry with exponential backoff." (that's HOW)

- **Edge case:** {Description from user's perspective} → **User experience:** {What the user sees or is told}
- **Edge case:** {Description} → **User experience:** {What the user sees or is told}

## 6. Acceptance Criteria

{The definition of done. Each criterion is objectively verifiable by observing the feature from the outside — a tester or stakeholder could check it without reading code. The feature is considered complete only when every criterion passes.}

Examples of the right level:
- ✅ "A logged-in user can add an item from the product page and see it reflected in the cart on the next page load."
- ✅ "An age-gated checkout flow blocks anyone who has not confirmed they are 19 or older."
- ❌ "Unit test coverage exceeds 80%." (engineering process, not feature behavior)

- [ ] **AC-1:** ...
- [ ] **AC-2:** ...
- [ ] **AC-3:** ...

---

## Assumptions & Open Questions

{A working appendix — *not* a product section. It aggregates every inline `(Assumption: …)` marker from the body, plus any question a reviewer should answer, so `/theshop.clarify` has one list to walk. As each item is resolved, fold the decision into the relevant section above and delete it from here. When the list is empty, write "None — all assumptions confirmed."}

- **📌 Assumption:** {A default you chose to fill a non-blocking gap — e.g., "Cart persists 30 days for guests."} → *Resolve via `/theshop.clarify`.*
- **❓ Open question:** {Something genuinely undecided that a reviewer should answer before the plan stage.}

> ⚠️ Blocking, load-bearing uncertainties do **not** belong here — those are asked before the spec is written. This list holds only cheap-to-change defaults.

---
**Status:** Draft — {N} open assumption(s)   ·   **Created:** {YYYY-MM-DD}

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once /theshop.clarify resolves them all (N = 0). -->
```

## Quality guidelines

- **Stay product-level the whole way through.** If a section drifts into endpoints, schemas, libraries, or performance numbers, rewrite it from the user's viewpoint. The reader should never have to know what language or framework the feature is built in.
- **Be specific in user/business terms.** "The cart is fast" is vague. "Items appear in the cart immediately after being added, before the user takes another action" is specific without being technical.
- **Make every requirement and acceptance criterion observable from the outside.** If you couldn't check it by clicking through the product, it's at the wrong level.
- **Cross-reference IDs when helpful.** Each acceptance criterion should map back to one or more functional requirements when the link isn't obvious (e.g., "AC-2 verifies FR-1 and FR-3").
- **Match the numbered-section count exactly.** Six numbered product sections, no more, no less. Scope lives in the In/Out block inside Section 1; assumptions live in the appendix below the status line. Neither is a numbered section, and neither counts against the six. Don't invent other top-level sections like "Future Work."
- **Keep it tight.** A useful spec is usually 1–3 pages. If it's growing past that, the "feature" is probably two features — flag this instead of writing a sprawling doc.
- **Surface assumptions inline _and_ in the appendix.** Anywhere you made a non-blocking judgment call instead of asking, mark it in place — `(Assumption: cart persists for 30 days for logged-out users.)` — and also list it in the Assumptions & Open Questions appendix so it's reviewable in one place. The inline marker is the flag; the appendix is the index `/theshop.clarify` walks. Load-bearing decisions are asked, never marked.

## Example invocations

**Example 1 — Feature name provided:**

> User: `/theshop.spec add-to-cart`
>
> Claude: [optionally asks one product-level question, e.g., "Does this need to support logged-out users, or only signed-in?"] → writes `.specs/add-to-cart/spec.md` and `.specs/add-to-cart/status.md` → "Saved to `.specs/add-to-cart/spec.md`. Want me to tighten any section?"

**Example 2 — No feature name provided:**

> User: `/theshop.spec`
>
> Claude: "Which feature should I create the spec for? Please give me a short feature name (e.g., `add-to-cart`, `user-authentication`)."

**Example 3 — File already exists:**

> User: `/theshop.spec add-to-cart`
>
> Claude: "A spec at `.specs/add-to-cart/spec.md` already exists. Should I overwrite it, save as `spec-v2.md`, or cancel?"

## Status tracker template

Every feature carries a `.specs/{feature_name}/status.md` — a one-glance view of where it sits in the SDD pipeline **and the ledger of every verification-gate outcome**. This skill **creates** it; each later step (`/theshop.clarify`, `/theshop.plan`, `/theshop.resolve`, `/theshop.implement`, `/theshop.test`, `/theshop.verify`, `/theshop.review`, `/theshop.document`) **updates its own row** plus the **Last updated** and **Next step** lines. Use this exact structure:

```markdown
# {Feature Title} — SDD Status

**Feature:** `{feature_name}`
**Last updated:** {YYYY-MM-DD}

| Stage | State | Gate | Evidence | Date |
|---|---|---|---|---|
| 1. Spec       | Draft | ✅ spec-gate pass | {N} FRs · {M} ACs · {K} open assumption(s) | {YYYY-MM-DD} |
| 2. Plan       | —     | — | — | — |
| 3. Implement  | —     | — | — | — |
| 4. Test       | —     | — | — | — |
| 5. Verify     | —     | — | — | — |
| 6. Review     | —     | — | — | — |
| 7. Document   | —     | — | — | — |

**Next step:** `/theshop.clarify {feature_name}`
```

**Gate column vocabulary** — every step records the outcome of its verification gate(s) in its own row:

| Cell | Meaning |
|---|---|
| `✅ {gate} pass` | The step's gate(s) passed — `check-sdd-gates.ps1` modes, build gates, lint, reconciliation. |
| `🔴 {gate} fail` | A gate failed and the step ended in that state (the State cell should reflect it too). |
| `⚠️ waived: {reason}` | The step proceeded past a warning gate on the user's explicit go-ahead. Waivers are **always recorded, never silent**. |
| `—` | Stage not reached yet. |

The **Evidence** cell is one line of mechanical fact — counts, migration names, failing project, gate-script summary — never a prose claim like "looks good".

**Two pipeline-wide rules every step follows:**

1. **Read the tracker on entry.** Before doing anything, check the upstream rows are in the state you expect (e.g. `/theshop.implement` expects Plan `Resolved`). If they aren't, warn the user; if the user says proceed, record `⚠️ waived: {reason}` in your own Gate cell so the skip stays visible downstream.
2. **Record your gate on exit.** State + Gate + Evidence + Date, refresh **Last updated** and **Next step**. A step only ever writes its own row.

Stage state vocabulary (a step only ever writes its own row):

| Stage | Set by | State transition |
|---|---|---|
| 1. Spec | `/theshop.spec` → `/theshop.clarify` | `Draft` → `Confirmed` |
| 2. Plan | `/theshop.plan` → `/theshop.resolve` | `Draft` → `Resolved` |
| 3. Implement | `/theshop.implement` | `Pending` → `Done` |
| 4. Test | `/theshop.test` | `Pending` → `Passing` / `Failing` |
| 5. Verify | `/theshop.verify` | `Pending` → `Verified` / `Skipped` |
| 6. Review | `/theshop.review` | `Pending` → `Approved` / `Changes requested` |
| 7. Document | `/theshop.document` | `Pending` → `Done` |

A step that runs against a feature created before this tracker existed should create `status.md` from the template first (back-filling earlier rows as best it can from the spec/plan status footers), then set its own row.