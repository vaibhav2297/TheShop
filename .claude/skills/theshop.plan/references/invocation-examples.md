## Example invocations

**Example 1 — Spec exists, name provided:**

> User: invokes `theshop-plan` with feature name `add-to-cart`
>
> Skill: reads `.specs/add-to-cart/spec.md` → invokes `theshop-constitution` skill (loads `SKILL.md` + relevant `references/rules/*.md`) → optionally consults MCPs → plans deliberately → writes `.specs/add-to-cart/plan.md`, updates `.specs/add-to-cart/status.md` → "Saved to `.specs/add-to-cart/plan.md`. One open question surfaced in Section 11."

**Example 2 — No name provided:**

> User: invokes `theshop-plan`
>
> Skill: "Which spec should I generate an implementation plan for? Please give me the spec file name (e.g., `add-to-cart`)."

**Example 3 — Spec doesn't exist:**

> User: invokes `theshop-plan` with feature name `nonexistent-feature`
>
> Skill: "I couldn't find a spec at `.specs/nonexistent-feature/spec.md`. I generate implementation plans from specs — please create the spec first (the `/theshop-spec` skill helps) and re-invoke me."

**Example 4 — Plan already exists:**

> User: invokes `theshop-plan` with feature name `add-to-cart`
>
> Skill: "A plan at `.specs/add-to-cart/plan.md` already exists. Should I overwrite it or cancel? (Overwriting marks the downstream pipeline rows stale — the previous version stays recoverable via git history.)"

**Example 5 — Figma URL provided via `--figma`:**

> User: `/theshop-plan user-authentication --figma https://www.figma.com/file/XXXXX/TheShop?node-id=42-100`
>
> Skill: extracts file key `XXXXX` and node ID `42:100` from the URL → calls `figma_get_component_for_development` on node `42:100` → records child node IDs and visual intent in Section 7 Step 5 (Web) → no prompting needed.

**Example 6 — No `--figma` supplied, spec implies UI:**

> User: `/theshop-plan user-authentication`
>
> Skill: detects a Web step (Section 7 Step 5) in the plan → asks: "This feature has a UI phase. Do you have a Figma link or node ID for it?" → user replies with URL or node ID → skill fetches and records IDs → continues.
>
> If user replies `skip` → Figma references section is omitted; open question logged in Section 11.

**Example 7 — Technical direction via `--desc`:**

> User: `/theshop-plan add-to-cart --desc reuse the existing CartState store; enforce a unique (cart_id, product_id) index to handle double-add --figma 123:456`
>
> Skill: parses feature name `add-to-cart`, description, and Figma node → folds the direction into the plan (CartState reuse lands in Section 7 Step 5, the unique index in Section 10 / the concurrency risk's mitigation) citing "per user direction" → anything in the description that would change product scope is flagged back to the spec instead of planned.
