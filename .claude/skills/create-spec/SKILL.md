---
name: create-spec
description: Generate a non-technical specification document for a single feature and save it to `.claude/specs/{feature_name}.md`. The spec is product-level only — focused on WHAT the feature does and WHY it matters, never on HOW it's built. It contains six fixed sections: problem statement, functional requirements, functional behaviors, constraints, edge cases and error handling, and acceptance criteria. This skill is manually invoked only (typically via slash command) and requires a feature name from the user.
disable-model-invocation: true
---

# Create Spec

Generate a non-technical feature specification and save it to `.claude/specs/{feature_name}.md`.

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

- Glance at related files in the workspace if obviously relevant (existing specs in `.claude/specs/`, project README, product docs).
- If one or two product-level details would meaningfully change the spec, ask up to **two** focused questions. Keep questions in WHAT/WHY territory:
  - ✅ "Does this apply to logged-out users too, or only signed-in?"
  - ✅ "Should the cart persist across sessions?"
  - ❌ "Should we use localStorage or a backend session?" (that's HOW)
- Otherwise, make reasonable assumptions and call them out explicitly inside the spec.

### 3. Write the spec using the template below

Use the exact six-section structure. Do not add extra sections.

### 4. Save the file

- Path: `.claude/specs/{feature_name}.md` (lowercase hyphenated form)
- Create the `.claude/specs/` directory if it does not already exist.
- If a file at that path already exists, ask the user whether to overwrite, save with a version suffix (e.g., `add-to-cart-v2.md`), or cancel.

### 5. Confirm

Report the saved path in one short sentence and offer to refine any section. Example:

> "Saved to `.claude/specs/add-to-cart.md`. Want me to tighten any section?"

## Spec template

Use this exact structure. Replace placeholders in `{curly braces}`. Every section stays product-level.

```markdown
# {Feature Title}

## 1. Problem Statement

{2–4 sentences in plain language: who has the problem, when it occurs, and why it matters. Frame it from the user's or business's perspective — not the system's. Name the user, the scenario, and the cost of leaving it unsolved.}

**Solution (one line):** {A single sentence describing what the feature will do for the user. No mention of how it's built.}

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
**Status:** Draft
**Created:** {YYYY-MM-DD}
```

## Quality guidelines

- **Stay product-level the whole way through.** If a section drifts into endpoints, schemas, libraries, or performance numbers, rewrite it from the user's viewpoint. The reader should never have to know what language or framework the feature is built in.
- **Be specific in user/business terms.** "The cart is fast" is vague. "Items appear in the cart immediately after being added, before the user takes another action" is specific without being technical.
- **Make every requirement and acceptance criterion observable from the outside.** If you couldn't check it by clicking through the product, it's at the wrong level.
- **Cross-reference IDs when helpful.** Each acceptance criterion should map back to one or more functional requirements when the link isn't obvious (e.g., "AC-2 verifies FR-1 and FR-3").
- **Match the section count exactly.** Six sections, no more, no less. No "Future Work", no "Out of Scope" as top-level sections — fold these into Constraints or inline notes if needed.
- **Keep it tight.** A useful spec is usually 1–3 pages. If it's growing past that, the "feature" is probably two features — flag this instead of writing a sprawling doc.
- **Surface assumptions inline.** Anywhere you made a judgment call instead of asking, mark it: `(Assumption: cart persists for 30 days for logged-out users.)`

## Example invocations

**Example 1 — Feature name provided:**

> User: `/create-spec add-to-cart`
>
> Claude: [optionally asks one product-level question, e.g., "Does this need to support logged-out users, or only signed-in?"] → writes `.claude/specs/add-to-cart.md` → "Saved to `.claude/specs/add-to-cart.md`. Want me to tighten any section?"

**Example 2 — No feature name provided:**

> User: `/create-spec`
>
> Claude: "Which feature should I create the spec for? Please give me a short feature name (e.g., `add-to-cart`, `user-authentication`)."

**Example 3 — File already exists:**

> User: `/create-spec add-to-cart`
>
> Claude: "A spec at `.claude/specs/add-to-cart.md` already exists. Should I overwrite it, save as `add-to-cart-v2.md`, or cancel?"