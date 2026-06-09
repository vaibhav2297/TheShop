---
name: theshop.resolve
description: Resolve the open questions, risks, and assumptions in a feature's technical implementation plan. Reads `.claude/plans/{feature_name}.md`, walks every ❓ Open question, ⚠️ Risk, and 📌 Assumption in Section 11 (plus any inline `📌` markers in the body), asks the user one technical question at a time with the logged default offered as the recommended answer, folds every confirmed decision back into the relevant plan section (Data Model, Design Decisions, Validation, Schema/RLS, Development Plan), records a mitigation or explicit acceptance for each risk, empties Section 11 of everything that's now settled, and flips the Status footer from `Draft` to `Resolved`. Stays at technical (HOW) altitude — this is the plan, not the spec. This is the fourth step of the SDD pipeline, between `/theshop.plan` and `/theshop.implement`. Manually invoked only; requires a feature name.
disable-model-invocation: true
---

# Resolve Plan

Turn a draft plan's open questions, risks, and assumptions into ratified engineering decisions. You read `.claude/plans/{feature_name}.md`, resolve every item in its **Section 11 (Open Questions, Risks & Assumptions)** with the user, write each decision into the body of the plan, and mark the plan `Resolved`.

This is the plan-level counterpart to `/theshop.clarify` (which does the same job for the *spec*). The difference is altitude: `clarify` stays product-level (WHAT/WHY); **`resolve` is technical (HOW)** — it is fine and expected to discuss endpoints, schema, concurrency, RLS, validators, and library choices here.

This skill is **manually invoked only** — it does not auto-trigger. The user explicitly calls it (e.g., `/theshop.resolve add-to-cart`).

## Core principle: resolve, then bake in — at the plan's altitude

Two rules define the job:

1. **Every resolved item must change the body of the plan, not just disappear from Section 11.** Deleting a Section 11 line without writing the decision into Sections 1–10 loses it. The plan must now *state* what was previously only open. An empty Section 11 is only correct if every removed item is now reflected as a settled decision elsewhere in the plan.
2. **Stay technical and concrete.** Don't drift up into product/marketing language (that's the spec's job, already done) and don't drift down into writing the full implementation (that's `/theshop.implement`'s job). The plan is the map; keep it a map.

## Inputs

One required input: a **feature name** (matching `.claude/plans/{feature_name}.md`).

- If the user provided a name, use it. Strip a trailing `.md` if included, and normalize to the lowercase-hyphenated form.
- If the user did **not** provide a name, stop and ask:

  > "Which plan should I resolve? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.claude/plans/{name}.md`."

  Wait for the reply. Do not guess from recent context.

- If the named plan does not exist, stop and tell the user:

  > "I couldn't find a plan at `.claude/plans/{name}.md`. I resolve existing plans — create one first with `/theshop.plan {name}`, then re-invoke me."

  Do not proceed without a plan.

## Workflow

### 1. Read the plan and collect every open item

Open `.claude/plans/{feature_name}.md` and read it in full. Build the worklist from two sources:

- **Section 11 (Open Questions, Risks & Assumptions)** — each `❓ Open question`, `⚠️ Risk`, and `📌 Assumption`.
- Any inline `📌 Assumption` markers in the body (carried forward from the spec when the plan was written on a still-`Draft` spec) that are **not** already represented in Section 11.

For each item, note which section(s) of the plan it will land in once resolved:

| Item touches… | Likely lands in |
|---|---|
| A data shape, entity, or table | Section 4 (Data Model), Section 10 (Schema) |
| A behavioral or architectural choice | Section 5 (Core Design Decisions) |
| A failure mode / validation rule | Section 9 (Validation & Error Handling) |
| Access control / tenancy | Section 10 (RLS Policies) |
| Sequencing or a new task | Section 7 (Development Plan) |

### 2. Short-circuit if there's nothing to do

If the worklist is empty **or** the Status footer already reads `Resolved`, don't interrogate the user. Report it and stop:

> "`add-to-cart` has no open items — Status is already `Resolved`. Nothing to resolve. Want me to re-review any section instead?"

### 3. Resolve each item with the user — one decision at a time

Walk the worklist in document order. Handle each item by type:

**❓ Open question — must be answered.** Ask one focused, technical question. Offer the most likely engineering default as *(Recommended)* when one exists, plus realistic alternatives, plus "use your judgment." An open question is a hard blocker — it cannot survive into `Resolved`.
- ✅ "When two tabs add the same product concurrently, do we want a unique `(cart_id, product_id)` index with `ON CONFLICT` upsert (recommended), or last-write-wins?"
- ✅ "Should cart TTL be enforced in the DB (a `cron` purge) or lazily at read-time?"

**📌 Assumption — ratify, override, or defer.** Present the logged default as *(Recommended)* so confirming is one tap. Accept three responses: **confirm** (ratify as-is), **override** (record the user's value), **defer / "you decide"** (keep the default but treat it as *accepted*, not open — it still gets baked into the body).

**⚠️ Risk — decide a disposition.** A risk is not always "fixed"; it needs a *disposition*. For each risk, ask the user to choose one:
- **Mitigate** → record a concrete mitigation and fold it into the relevant section (a new Section 7 task, a Section 5 decision, a Section 10 constraint). The risk line in Section 11 is then removed because the mitigation now lives in the plan.
- **Accept** → the team knowingly accepts the risk. Keep the line in Section 11 but relabel it `⚠️ Risk — ✅ Accepted: {one-line rationale}`. Accepted risks stay visible and do **not** block `Resolved`.

Don't batch unrelated questions into a wall of text. A couple of tightly related items can share a turn, but keep each decision separately answerable.

**If an answer exposes a deeper architectural gap** (e.g., the chosen concurrency strategy forces a schema change the plan didn't anticipate), surface it plainly and resolve it now. If resolving it spawns new cheap sub-decisions, add them to the worklist and keep going.

### 4. Bake each decision into the body, then clear it from Section 11

For every resolved item, in this order:

1. **Edit the relevant section(s)** so the plan now states the decision as a settled engineering fact. Rewrite an inline `📌` marker into a plain declarative line. If the decision adds a rule, update Section 9. If it changes the schema, update Sections 4 and 10. If it adds work, update Section 7. If it changes how a flow runs, update Section 6. If an `AC → Task` mapping shifts, update Section 8.
2. **Remove the item from Section 11** — except an `Accepted` risk, which stays (relabeled) by design.
3. Never leave a decision recorded only in Section 11 — the body is where it lives now; Section 11 only indexes what's *still open or knowingly accepted*.

Resolving an item should tighten the plan, not bloat it — fold, don't append.

### 5. Update Status and Section 11

Recount what remains in Section 11 (`❓` open questions + unratified `📌`; `Accepted` risks don't count):

- **All open questions answered and all assumptions ratified (count = 0):** Section 11 reads `None — all questions resolved.` (followed by any `✅ Accepted` risks, if present), and the footer becomes:

  ```
  **Status:** Resolved · **Spec:** `.claude/specs/{file_name}.md` · **Created:** {original date} · **Resolved:** {today YYYY-MM-DD}
  ```

- **Partially resolved (count > 0, user deferred some to a later pass):** keep the footer as `Draft`, leave the still-open items in Section 11, and report the remaining count.

Preserve the original **Created** date; only add/update **Resolved**.

### 6. Save and confirm

Save the plan back to the same path. Then report, in a couple of sentences: how many items were resolved, anything notable that changed (especially schema or a load-bearing design decision), how each risk was dispositioned, the new Status, and — when fully resolved — that implementation is unblocked. Example:

> "Resolved 4 items in `add-to-cart` — chose the `(cart_id, product_id)` unique-index upsert for the concurrency question, ratified the 20-distinct-items cap, added a lazy TTL purge to Phase 3, and accepted the price-drift risk with a noted mitigation. Status is now `Resolved`; you're clear to run `/theshop.implement add-to-cart`."

If items remain open:

> "Resolved 2 of 3 — the cart-TTL question is still open and stays logged in Section 11. Re-run `/theshop.resolve add-to-cart` when you've decided, or `/theshop.implement` will warn that the plan isn't resolved."

## Quality guidelines

- **Stay at the plan's altitude.** Technical and concrete, but still a map — no full implementations, no product/marketing prose. If the user answers vaguely, pin it to a concrete engineering decision before recording it.
- **Open questions are hard blockers.** `Resolved` is never correct while a `❓` remains. Risks are different — an `Accepted` risk is a legitimate end-state.
- **Default-first, never interrogation.** The logged assumption is a *proposal*; confirming it should be the path of least resistance. Don't relitigate cheap defaults the user is happy with.
- **Decisions live in the body.** An empty (or Accepted-only) Section 11 is only correct if every removed item now reads as a settled decision in Sections 1–10. Never delete an item without writing its resolution.
- **Keep it tight.** Resolving items should not grow the plan past its 3–6 page sweet spot. Fold answers into existing sections; don't tack on paragraphs.
- **Count honestly.** The Status footer flips to `Resolved` only when zero open questions and zero unratified assumptions remain. Accepted risks are documented, not open.
- **One plan only.** Don't wander into related plans or the spec. If resolution reveals a spec-level contradiction (the plan can't satisfy the spec as written), don't edit the spec — flag it and tell the user to re-run `/theshop.clarify` or update the spec, then re-plan.

## Example invocations

**Example 1 — plan with open items:**

> User: `/theshop.resolve add-to-cart`
>
> Claude: reads `.claude/plans/add-to-cart.md` → finds 1 open question, 2 assumptions, 1 risk → asks one technical question per item (defaults marked *Recommended*) → folds each decision into Sections 4/5/7/10 → dispositions the risk (mitigate or accept) → empties Section 11 → sets Status `Resolved` → "Resolved 4 items… you're clear to run `/theshop.implement add-to-cart`."

**Example 2 — no feature name provided:**

> User: `/theshop.resolve`
>
> Claude: "Which plan should I resolve? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.claude/plans/{name}.md`."

**Example 3 — nothing to resolve:**

> User: `/theshop.resolve checkout`
>
> Claude: "`checkout` has no open items — Status is already `Resolved`. Nothing to resolve. Want me to re-review any section instead?"

**Example 4 — plan doesn't exist:**

> User: `/theshop.resolve wishlist`
>
> Claude: "I couldn't find a plan at `.claude/plans/wishlist.md`. I resolve existing plans — create one first with `/theshop.plan wishlist`, then re-invoke me."
