## Example invocations

**Example 1 — Feature name provided:**

> User: `{{command:theshop-spec}} add-to-cart`
>
> Claude: [optionally asks one product-level question, e.g., "Does this need to support logged-out users, or only signed-in?"] → writes `.specs/add-to-cart/spec.md` and `.specs/add-to-cart/status.md` → "Saved to `.specs/add-to-cart/spec.md`. Want me to tighten any section?"

**Example 2 — Feature name with description:**

> User: `{{command:theshop-spec}} wishlist --desc signed-in customers can save products to a wishlist and move them to the cart later; no sharing with other users`
>
> Claude: [uses the description as product input — "signed-in only" and "no sharing" land directly in scope; asks only about uncertainties the description doesn't cover] → writes `.specs/wishlist/spec.md` and `.specs/wishlist/status.md` → "Saved to `.specs/wishlist/spec.md` — 1 open assumption logged. Run `{{command:theshop-clarify}} wishlist` to resolve it."

**Example 3 — No feature name provided:**

> User: `{{command:theshop-spec}}`
>
> Claude: "Which feature should I create the spec for? Please give me a short feature name (e.g., `add-to-cart`, `user-authentication`) — optionally followed by `--desc` and a description of what it should do."

**Example 4 — File already exists:**

> User: `{{command:theshop-spec}} add-to-cart`
>
> Claude: "A spec at `.specs/add-to-cart/spec.md` already exists. Should I overwrite it or cancel? (Overwriting marks the downstream pipeline rows stale — the previous version stays recoverable via git history.)"

