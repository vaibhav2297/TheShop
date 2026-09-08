## Example invocations

**Example 1 — plan with open items:**

> User: `/theshop-resolve add-to-cart`
>
> Claude: reads `.specs/add-to-cart/plan.md` → finds 1 open question, 2 assumptions, 1 risk → asks one technical question per item (defaults marked *Recommended*) → folds each decision into Sections 4/5/7/10 → dispositions the risk (mitigate or accept) → empties Section 11 → sets Status `Resolved` → "Resolved 4 items… you're clear to run `/theshop-implement add-to-cart`."

**Example 2 — no feature name provided:**

> User: `/theshop-resolve`
>
> Claude: "Which plan should I resolve? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.specs/{name}/plan.md`."

**Example 3 — nothing to resolve:**

> User: `/theshop-resolve checkout`
>
> Claude: "`checkout` has no open items — Status is already `Resolved`. Nothing to resolve. Want me to re-review any section instead?"

**Example 4 — plan doesn't exist:**

> User: `/theshop-resolve wishlist`
>
> Claude: "I couldn't find a plan at `.specs/wishlist/plan.md`. I resolve existing plans — create one first with `/theshop-plan wishlist`, then re-invoke me."
