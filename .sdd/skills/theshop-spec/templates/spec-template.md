# Feature Specification Template

> Canonical spec template, read by `{{command:theshop-spec}}` and enforced by `check-sdd-gates.ps1 spec`:
> exactly six numbered sections with these exact titles, the **Scope** and **Actors & Access**
> sub-sections inside Section 1, the **Business Rules** table inside Section 4, sequential FR/AC
> ids, Given/When/Then acceptance criteria, the Assumptions & Open Questions appendix, and the
> Status footer.
>
> This spec is a **product document**: WHAT the feature does and WHY it matters — never HOW it's
> built. A non-developer product manager should be able to read it and fully understand the feature.
>
> Replace placeholders in `{curly braces}`. Delete guidance blockquotes when generating a real spec.

---

# {Feature Title}

## 1. Problem Statement

{State supplied user goal and supported problem facts. One sentence is sufficient when input gives
only a goal. Never invent current tools, history, causes, losses, or business costs to fill this
section. Omit unsupported backstory; ask only when missing information changes product scope.}

**Solution (one line):** {A single sentence describing what the feature will do for the user. No
mention of how it's built.}

### Scope

**In scope:**
- {Required capability or user action this feature explicitly includes}
- {…}

**Out of scope:**
- {Excluded capability, behavior handled by another feature, or future enhancement}
- {Anything that must not be assumed as part of this feature}

> The out-of-scope list is the boundary that stops scope creep — write "None" only if there is
> genuinely nothing to exclude. (Kept inside Section 1 as a sub-section: the pipeline gate requires
> the In/Out markers here and exactly six numbered sections.)

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| {Guest customer} | {What this actor can do in this feature} | {What is denied, and what they see instead} |
| {Signed-in customer} | {…} | {…} |
| {Admin} | {…} | {…} |

> Include only actors whose behavior actually differs. If the feature treats everyone identically,
> replace the table with one sentence saying so. "Who is allowed" is product-level — name it here,
> don't leave it to the plan.

## 2. Functional Requirements

{Numbered list of what the feature must do, written as user-visible or business-observable
statements. Each item is complete and testable from the outside, without looking at code.}

1. **FR-1:** {…}
2. **FR-2:** {…}
3. **FR-3:** {…}

> Right level: ✅ "Users can add a product to their cart from the product detail page."
> ❌ "The system calls the /cart/add endpoint and updates the store." (too technical)

## 3. Functional Behaviors

{For each significant user interaction: what the user does and what the user observes in response.
Input is a user action or business event, not a payload. Output is what the user sees, not a
response object.}

### Behavior 1: {Short name, e.g., "Add an item to the cart"}
- **User does:** {The user-facing action.}
- **User sees:** {The observable result.}

### Behavior 2: {Name}
- **User does:** {…}
- **User sees:** {…}

## 4. Constraints

{Bullet list of business, policy, regulatory, or user-experience constraints the feature must
respect. Numbers belong here when they're user-facing or policy-driven — never performance or
infrastructure numbers.}

- {…}
- {…}

### Business Rules

{Precise, individually verifiable rules that control the feature's behavior — required information,
eligibility, duplicate prevention, ownership, pricing/rounding, quantity limits, allowed state
transitions, expiry. Each rule states its condition **and** the outcome when it's violated.}

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | {Verifiable condition — e.g., "Brand names must be unique regardless of capitalization and leading/trailing spaces."} | {Rejected with a clear message / restricted / recalculated} |
| **RULE-2** | {…} | {…} |

> A rule must be precise enough to verify. "Names should be unique" is vague; "unique regardless of
> capitalization and surrounding spaces" is a rule. Rules here are the direct source for validators
> and ACs downstream — cross-reference them (e.g., "AC-3 verifies RULE-1").

## 5. Edge Cases & Error Handling

{User-experience edge cases and abnormal scenarios, each paired with what the user sees or
experiences. Frame everything from the user's perspective. Don't list infrastructure failures; list
their user-visible consequences.}

- **Edge case:** {Description from user's perspective} → **User experience:** {What the user sees or is told}
- **Edge case:** {Description} → **User experience:** {…}

> Right framing: ✅ "User tries to add an out-of-stock item → 'Add to Cart' is disabled and shows
> 'Out of stock'." ❌ "Inventory service times out → retry with backoff." (that's HOW)

## 6. Acceptance Criteria

{The definition of done. Each criterion is objectively verifiable by observing the feature from the
outside. Prefer Given/When/Then phrasing inside each item — it forces a testable initial condition,
action, and observable outcome. Cover: the primary success path, key business rules, access
restrictions, and at least one failure/edge scenario.}

- [ ] **AC-1:** Given {initial condition}, when {user action}, then {observable outcome}.
- [ ] **AC-2:** Given {actor without required access}, when {they attempt the action}, then {observable access-denied outcome}.
- [ ] **AC-3:** Given {invalid input or violated RULE-n}, when {submitted}, then {observable rejection and guidance}.

---

## Assumptions & Open Questions

{A working appendix — not a product section. It aggregates every inline `(Assumption: …)` marker
from the body plus any question a reviewer should answer, so `{{command:theshop-clarify}}` has one list to
walk. As each item is resolved, fold the decision into the section above and delete it from here.
When the list is empty, write "None — all assumptions confirmed."}

- **📌 Assumption:** {A default you chose to fill a non-blocking gap.} → *Resolve via `{{command:theshop-clarify}}`.*
- **❓ Open question:** {Something genuinely undecided that a reviewer should answer before the plan stage.}

> ⚠️ Blocking, load-bearing uncertainties do **not** belong here — those are asked before the spec
> is written. This list holds only cheap-to-change defaults.

---
**Status:** Draft — {N} open assumption(s)   ·   **Created:** {YYYY-MM-DD}

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once {{command:theshop-clarify}} resolves them all (N = 0). -->
