## Example invocations

**Example 1 — feature with open assumptions:**

> User: `{{command:theshop-clarify}} add-to-cart`
>
> Claude: reads `.specs/add-to-cart/spec.md` → finds 3 open items → asks one product-level question per item (assumed value marked *Recommended*) → folds each answer into the body → empties the appendix → sets Status `Confirmed` → "Resolved 3 assumptions… you're clear to run `{{command:theshop-plan}} add-to-cart`."

**Example 2 — no feature name provided:**

> User: `{{command:theshop-clarify}}`
>
> Claude: "Which spec should I clarify? Please give me the feature name (e.g., `add-to-cart`). It must match an existing file at `.specs/{name}/spec.md`."

**Example 3 — nothing to clarify:**

> User: `{{command:theshop-clarify}} checkout`
>
> Claude: "`checkout` has no open assumptions — Status is already `Confirmed`. Nothing to clarify. Want me to review any section instead?"

**Example 4 — spec doesn't exist:**

> User: `{{command:theshop-clarify}} wishlist`
>
> Claude: "I couldn't find a spec at `.specs/wishlist/spec.md`. I clarify existing specs — create one first with `{{command:theshop-spec}} wishlist`, then re-invoke me."
