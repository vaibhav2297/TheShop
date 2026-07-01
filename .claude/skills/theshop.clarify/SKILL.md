---
name: theshop.clarify
description: Resolve the open assumptions and questions in a feature spec. Reads `.specs/{feature_name}/spec.md`, walks each 📌 Assumption and ❓ Open question (plus any inline `(Assumption: …)` markers), asks the user one product-level question at a time with the logged default offered as the recommended answer, folds every confirmed decision back into the relevant spec section, empties the Assumptions & Open Questions appendix, and flips the Status footer from `Draft — N open assumption(s)` to `Confirmed`. Stays strictly product-level (WHAT/WHY) — never introduces HOW. This is the second step of the spec pipeline, between `/theshop.spec` and `/theshop.plan`. Manually invoked only; requires a feature name.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Clarify Spec

Turn a draft spec's open assumptions into ratified decisions. You read `.specs/{feature_name}/spec.md`, resolve every item in its **Assumptions & Open Questions** appendix with the user, write each decision into the body of the spec, and mark the spec `Confirmed`.

This skill is **manually invoked only** — it does not auto-trigger. The user explicitly calls it (e.g., `/theshop.clarify add-to-cart`).

## Core principle: resolve, then bake in — still WHAT/WHY only

This is a clarification pass over a **product document**. Everything in `theshop.spec`'s discipline still holds: the spec stays non-technical, readable by a product manager, focused on WHAT the feature does and WHY it matters — never HOW it's built.

Two rules define the job:

1. **Every resolved assumption must change the body of the spec, not just disappear from the appendix.** Deleting an appendix line without writing the confirmed fact into Section 1–6 loses the decision. The spec must now *state* what was previously only assumed.
2. **Keep the same altitude as the spec.** If the user's answer drifts into implementation ("store it in localStorage"), translate it back to user-visible terms ("the cart is remembered on this device") and record that. Implementation choices belong in `/theshop.plan`, not here.

## Inputs

One required input: a **feature name** (matching `.specs/{feature_name}/spec.md`).

- If the user provided a name, use it. Strip a trailing `.md` if they included it, and normalize to the lowercase-hyphenated form (same rules as `theshop.spec`).
- If the user did **not** provide a name, stop and ask:

  > "Which spec should I clarify? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.specs/{name}/spec.md`."

  Wait for the reply. Do not guess from recent context.

- If the named spec does not exist, stop and tell the user:

  > "I couldn't find a spec at `.specs/{name}/spec.md`. I clarify existing specs — create one first with `/theshop.spec {name}`, then re-invoke me."

  Do not proceed without a spec.

## Workflow

### 1. Read the spec and collect every open item

Open `.specs/{feature_name}/spec.md` and read it in full. Build the worklist from two sources:

- The **Assumptions & Open Questions** appendix — each `📌 Assumption` and `❓ Open question`.
- Any inline `(Assumption: …)` markers in the body that are **not** already represented in the appendix. (A well-formed spec keeps these in sync, but reconcile if they've drifted — the body is authoritative for *where* a decision lives; the appendix is the index.)

For each item, note which section(s) of the spec it touches (e.g., a cart-TTL assumption touches Constraints; a "guests can also do X" assumption touches Functional Requirements and possibly Behaviors and Scope).

### 2. Short-circuit if there's nothing to do

- If the worklist is empty **or** the Status footer already reads `Confirmed`, don't interrogate the user. Report it and stop:

  > "`add-to-cart` has no open assumptions — Status is already `Confirmed`. Nothing to clarify. Want me to review any section instead?"

### 3. Resolve each item with the user — one decision at a time

Walk the worklist in document order. For each item, ask **one focused, product-level question**, and present the logged default as the recommended answer so confirming is one tap:

- Frame the question in WHAT/WHY terms, never HOW.
  - ✅ "For guests, how long should the cart be remembered? (Assumed: 30 days.)"
  - ❌ "Should the cart live in localStorage or a server session?"
- Offer the assumed value as the **recommended** option, plus the realistic alternatives, plus "let me decide for you." When the choices are discrete, a multiple-choice prompt with the assumption marked *(Recommended)* keeps it fast; for open-ended ones, ask in plain language.
- Accept three kinds of response and handle each:
  - **Confirm the default** → the assumption is ratified as-is.
  - **Override** → record the user's value instead.
  - **Defer ("you decide" / "use your judgment")** → keep the default, but treat it as *accepted*, not unresolved. It still gets baked into the body; it just leaves the appendix.
- Don't batch unrelated questions into one wall of text. A couple of tightly related items can share a turn, but keep each decision separately answerable.

**If an answer exposes a blocking, load-bearing gap** (something that changes the feature's identity or scope), surface it plainly and resolve it now — that is exactly the kind of thing that should never have been a silent default. If resolving it spawns new cheap sub-defaults, add them to the worklist and keep going.

### 4. Bake each decision into the body, then clear it from the appendix

For every resolved item, in this order:

1. **Edit the relevant section(s)** so the spec now states the decision as settled fact. Rewrite the inline `(Assumption: …)` marker into a plain declarative sentence (e.g., `(Assumption: cart persists 30 days for guests.)` → "A guest's cart is remembered for 30 days."). If the decision changes scope, update the In/Out of Scope block in Section 1. If it adds a rule, update Constraints. If it changes what the user sees, update Functional Behaviors or Edge Cases. Touch Acceptance Criteria if the definition of done shifts.
2. **Remove the item from the Assumptions & Open Questions appendix.**
3. Never leave a decision recorded in only one place — the body is where it lives now; the appendix only indexes what's *still open*.

Do not expand the spec while doing this. Resolving an assumption should tighten the prose, not bloat it — fold, don't append.

### 5. Update Status and the appendix footer

Recount the open items remaining (`N`):

- **All resolved (`N = 0`):** the appendix body reads `None — all assumptions confirmed.` and the footer becomes:

  ```
  **Status:** Confirmed   ·   **Created:** {original date}   ·   **Clarified:** {today YYYY-MM-DD}
  ```

- **Partially resolved (`N > 0`, user deferred some to a later pass):** keep the footer as `Draft — N open assumption(s)` with the reduced count, and leave the still-open items in the appendix.

Preserve the original **Created** date; only add/update **Clarified**.

### 6. Save, run the gate, and confirm

Save the spec back to the same path. **Run the template gate (exit gate — mandatory):**

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 spec -Feature {feature_name}
```

This mechanically verifies what this skill promises: that the footer's `N` matches the appendix count, that `Confirmed` means a genuinely empty appendix, and that folding decisions into the body didn't break the template structure. **Exit 1 → fix the spec and re-run. Never flip the Status to `Confirmed` while this gate fails.**

**Update the status tracker:** in `.specs/{feature_name}/status.md`, set the **Spec** row to `Confirmed` (only when `N = 0`; otherwise leave it `Draft`), Gate `✅ spec-gate pass`, Evidence one line (e.g. `3 assumptions resolved · appendix empty`), today's date; refresh **Last updated**, and point **Next step** at `/theshop.plan {feature_name}`. (If `status.md` is missing — a pre-tracker feature — create it from the template in `theshop.spec` first.) Then report, in a couple of sentences: how many items were resolved, anything notable that changed (especially scope), the new Status, and — when fully confirmed — that the plan stage is unblocked. Example:

> "Resolved 3 assumptions in `add-to-cart` — confirmed the 30-day guest cart, narrowed scope to exclude saved-for-later, and set the max-items rule to 20 distinct products. Status is now `Confirmed`; you're clear to run `/theshop.plan add-to-cart`."

If items remain open:

> "Resolved 2 of 3 — the gift-wrapping question is still open and stays logged in the appendix. Re-run `/theshop.clarify add-to-cart` when you've decided, or `/theshop.plan` will warn that one assumption is unconfirmed."

## Quality guidelines

- **Stay product-level the whole way.** This is still the spec. No endpoints, schemas, libraries, or performance numbers creep in during clarification. If the user answers in HOW terms, restate the user-visible WHAT and record that.
- **Default-first, never interrogation.** The logged assumption is a *proposal*; confirming it should be the path of least resistance. Don't relitigate cheap defaults the user is happy with.
- **Surface, don't bury, real gaps.** If a "cheap default" turns out to be load-bearing, escalate it instead of quietly keeping the guess. That's the whole reason this step exists.
- **Decisions live in the body.** An empty appendix is only correct if every removed item now reads as settled fact in Sections 1–6 (or the Scope block). Never delete an assumption without writing its resolution.
- **Keep it tight.** Resolving assumptions should not grow the spec past its 1–3 page sweet spot. Fold answers into existing sentences; don't tack on paragraphs.
- **Count honestly.** The Status footer's `N` must match the number of items actually left in the appendix. `Confirmed` means zero open items — never mark it while questions remain.
- **One spec only.** Don't wander into related specs. If clarification reveals a cross-feature dependency, note it in the spec (Constraints or an inline note) and mention it in your closing report — don't start editing the other spec.

## Example invocations

**Example 1 — feature with open assumptions:**

> User: `/theshop.clarify add-to-cart`
>
> Claude: reads `.specs/add-to-cart/spec.md` → finds 3 open items → asks one product-level question per item (assumed value marked *Recommended*) → folds each answer into the body → empties the appendix → sets Status `Confirmed` → "Resolved 3 assumptions… you're clear to run `/theshop.plan add-to-cart`."

**Example 2 — no feature name provided:**

> User: `/theshop.clarify`
>
> Claude: "Which spec should I clarify? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.specs/{name}/spec.md`."

**Example 3 — nothing to clarify:**

> User: `/theshop.clarify checkout`
>
> Claude: "`checkout` has no open assumptions — Status is already `Confirmed`. Nothing to clarify. Want me to review any section instead?"

**Example 4 — spec doesn't exist:**

> User: `/theshop.clarify wishlist`
>
> Claude: "I couldn't find a spec at `.specs/wishlist/spec.md`. I clarify existing specs — create one first with `/theshop.spec wishlist`, then re-invoke me."
