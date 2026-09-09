## Example invocations

**Example 1 — feature with open assumptions:**

> User: `/theshop-clarify add-to-cart`
>
> Claude: reads `.specs/add-to-cart/spec.md` → finds 3 open items → asks one product-level question per item (assumed value marked *Recommended*) → folds each answer into the body → empties the appendix → sets Status `Confirmed` → "Resolved 3 assumptions… you're clear to run `/theshop-plan add-to-cart`."

**Example 2 — no feature name provided:**

> User: `/theshop-clarify`
>
> Claude: "Which spec should I clarify? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.specs/{name}/spec.md`."

**Example 3 — nothing to clarify:**

> User: `/theshop-clarify checkout`
>
> Claude: "`checkout` has no open assumptions — Status is already `Confirmed`. Nothing to clarify. Want me to review any section instead?"

**Example 4 — spec doesn't exist:**

> User: `/theshop-clarify wishlist`
>
> Claude: "I couldn't find a spec at `.specs/wishlist/spec.md`. I clarify existing specs — create one first with `/theshop-spec wishlist`, then re-invoke me."
