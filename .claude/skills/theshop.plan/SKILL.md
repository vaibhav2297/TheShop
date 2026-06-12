---
name: theshop.plan
description: Read a feature spec from `.specs/{feature_name}/spec.md` and generate a technical implementation plan (design doc) saved to `.specs/{feature_name}/plan.md`. The plan covers architecture, data model, design decisions, validators, error handling, RLS policies, and a phased development plan — focused on HOW the feature will be built. The companion spec stays non-technical (WHAT/WHY); this plan is fully technical. Optional `--desc <description>` lets the user supply technical direction up front; optional `--figma <url|nodeId>` pins the design frames. Manually invoked only.
argument-hint: <feature-name> [--desc <description>] [--figma <url|nodeId>]
disable-model-invocation: true
---

# Create Plan

Read a feature specification and produce a thorough technical implementation plan (a design doc) at `.specs/{feature_name}/plan.md`.

This skill is **manually invoked only** — it does not auto-trigger.

## Core principle: HOW, not WHAT/WHY

The spec document answers **what** the feature does and **why** it matters. It is product-level, non-technical, and reads cleanly to a product manager. This plan document answers **how** we will build it. It is technical and reads cleanly to a developer who needs to ship the feature.

Stay strictly in HOW territory:

| Stay in (✅) | Stay out of (❌) |
|---|---|
| Layer-by-layer architecture, MediatR commands and queries, repository interfaces | Restating user-facing behavior (the spec already covers that) |
| Database tables, columns, indexes, RLS policies | Marketing language, brand positioning |
| Validators, error keys, exception types, Result<T> shapes | "Why this feature matters to the business" |
| File-by-file change list, sequenced development steps | User personas, customer journeys |
| Concrete code shape (signatures, key methods), not full implementation | Full implementation code (the plan is the map, the code is the territory) |

If a section starts pulling toward "what the user sees," redirect — that belongs in the spec.

---

## Inputs

The skill takes one required input and two optional inputs:

```
/theshop.plan <feature-name> [--desc <description>] [--figma <url|nodeId>]
```

**Flag parsing:** everything before the first `--` flag is the feature name. Flags may appear in either order; each flag's value runs until the next `--` flag or the end of the input. A flag with nothing after it is treated as not provided.

### Required — spec file name

Must match `.specs/{file_name}/spec.md`.

- If the user provided a name, use it. Strip the `.md` extension if they included it.
- If the user did **not** provide a name, stop and ask:

  > "Which spec should I generate an implementation plan for? Please give me the spec file name (e.g., `add-to-cart`). It must match an existing file at `.specs/{name}/spec.md`."

  Wait for the reply before doing anything else.

- If the named spec file does not exist, stop and tell the user:

  > "I couldn't find a spec at `.specs/{name}/spec.md`. I generate implementation plans from specs — please create the spec first (the `/theshop.spec` skill helps) and re-invoke me."

  Do not proceed without a spec.

### Optional — technical direction (`--desc`)

The user may supply free-text technical direction alongside the feature name:

```
/theshop.plan add-to-cart --desc reuse the existing CartState store; cart rows need a unique (cart_id, product_id) index to handle double-add
```

**How the description is used (when present):**

- Treat it as **user-supplied technical input** — preferred approaches, components or services to reuse, schema/constraint requirements, things to avoid. Fold it into the relevant sections (Design Decisions, Data Model, Development Plan) and cite it where it settles a choice (e.g. "per user direction").
- It **steers HOW, never WHAT.** If the description introduces new product behavior or scope the spec doesn't cover, don't plan it — flag the mismatch and point the user at updating the spec (`/theshop.spec` / `/theshop.clarify`) first.
- It does **not** override the constitution. If the description asks for something the `theshop.constitution` rules forbid (e.g. a non-MudBlazor component, a layer violation), stop and raise it rather than planning the violation.
- If part of the description conflicts with the spec or with itself, surface it in Section 11 (or ask, if it's load-bearing) instead of silently picking a side.

### Optional — Figma link or node ID (`--figma`)

The user may supply a Figma URL or node ID alongside the feature name, in any of these forms:

```
/theshop.plan add-to-cart --figma https://www.figma.com/file/XXXXX/TheShop?node-id=123-456
/theshop.plan add-to-cart --figma 123:456
/theshop.plan add-to-cart --figma 123:456,789:012
```

- **Full URL** — extract the file key and any `node-id` query parameter from the URL. Use the file key to open the correct Figma file; use the node ID as the starting frame.
- **Node ID(s)** — one or more `node:id` values (comma-separated). Treat these as the explicit starting frames for this feature; you will still fetch them via Figma MCP to verify and extract child node IDs.
- **Not provided** — see Step 4 below for how to discover node IDs without an explicit hint.

When the user provides `--figma`, skip the "ask the user" prompt in Step 4 and go straight to fetching.

---

## Workflow

### 1. Engage extended thinking

Before reading anything, prepare for a deliberate, high-effort planning session. **Think hard** about this task — every choice in the plan compounds downstream into code, tests, and reviews, so the time invested here pays off many times over.

If the user has selected the highest-capability model (Opus) and enabled extended thinking, you're already in the right setup. If not, the plan will still get written — just less thoroughly. Don't compromise on rigor regardless.

### 2. Read the spec thoroughly

Open `.specs/{file_name}/spec.md` and read every section. Extract:

- **Problem Statement** → informs the "Objective" section of the plan.
- **Functional Requirements** → each FR maps to one or more concrete development tasks.
- **Functional Behaviors** → each behavior maps to a flow that runs through your layered architecture (UI → MediatR command → handler → repository → Supabase).
- **Constraints** → each constraint must be reflected in a design decision or a validation rule.
- **Edge Cases & Error Handling** → each item must have an implementation strategy (which validator catches it, which `Result.Fail(...)` key it produces, which exception is thrown).
- **Acceptance Criteria** → every AC must map to at least one task in the development plan. No AC is allowed to be unaddressed — flag it if you can't see how it will be met.

**Check the spec's Status before planning.** If the footer still reads `Draft — N open assumption(s)` — i.e., the Assumptions & Open Questions appendix has unresolved `📌` / `❓` items — warn the user rather than silently building on unconfirmed defaults:

> "Heads up: `add-to-cart` still has {N} unresolved assumption(s). Planning on top of unconfirmed defaults risks rework. Want to run `/theshop.clarify add-to-cart` first, or should I proceed and treat the logged assumptions as accepted?"

Proceed only on the user's go-ahead. If they say proceed, carry each still-open assumption forward into Section 11 (as a `📌 Assumption`) so it stays visible in the plan.

If any spec section is empty, vague, or contradicts another, **stop and ask the user** before planning. Do not paper over gaps with invented behavior.

### 3. Load the governing documents via the `theshop.constitution` skill

The architecture and design rules live inside the `theshop.constitution` skill. **Invoke the `theshop.constitution` skill** — it will surface the rule list in `SKILL.md` and the modular references under `references/rules/`. Do **not** try to read individual reference files as bare filenames, and do **not** Glob-search the project for them; let the `theshop.constitution` skill direct you. This is what prevents the slow project-wide file search.

The rules the skill defines are what your plan must satisfy:

- **`SKILL.md`** — canonical numbered rule list (Rules 1–30): four-layer Clean Architecture, MediatR/`Result<T>` patterns, `Shop*` theme classes, MudBlazor-only rule, color hierarchy, typography, busy state, routes, component forwarding, SCSS organisation, tests, docs.
- **`references/rules/architecture-core.md`** — layer-placement table, folder structure, coding standards. Always relevant.
- **`references/rules/architecture-patterns.md`** — MediatR, `Result<T>`, validators, AutoMapper, repositories. Relevant when planning Application + Infrastructure.
- **`references/rules/architecture-admin.md`** — admin routing, RLS as the only real security boundary, RLS examples. Relevant when planning admin features.
- **`references/rules/design-*.md`** — strings, theme, components, styles. Relevant when planning UI.

If `CLAUDE.md` is present, it's already in context (loaded automatically by Claude Code).

### 4. Consult MCPs when they sharpen the plan (not by default)

- **MudBlazor MCP** — call when the spec implies UI components and you need to pick the right one or verify a parameter. Use `mudblazor:search_components`, `mudblazor:get_component_detail`, or `mudblazor:get_component_parameters`. Don't enumerate all components "for completeness."
- **Figma MCP** — call when the spec describes a designed UI flow. The downstream `shop-ui-implementer` agent will **re-fetch these nodes at implementation time** to translate them with high fidelity, so the plan must capture three things explicitly:

  1. The **Figma file URL** (the canonical link).
  2. The **per-page/component node ID** for every page or component this feature introduces or modifies — node IDs are the contract the UI implementer reads.
  3. A **one-sentence "visual intent"** per node — what the node is and how it fits into the feature flow. Not a re-description of the design; a hook so a reader (or the implementer) can confirm "this is the sign-in OTP step, not the sign-up first step."

  **How to find node IDs — follow this sequence:**

  **a. If the user supplied `--figma`** — use the file key and/or node IDs directly from their input. Call `figma-console:figma_get_component_for_development` on each provided node ID to confirm it exists and get its children. Use those children as the per-component node IDs for the plan.

  **b. If no `--figma` was supplied and the spec implies UI** — ask the user once before fetching:

  > "This feature has a UI phase. Do you have a Figma link or node ID for it? If yes, provide it now (e.g., `https://figma.com/...` or `123:456`). If no, I'll search the open Figma file for a page matching this feature — reply `skip` to skip Figma entirely for this feature."

  Wait for the reply. Then:
  - **URL/node ID given** — proceed as in (a).
  - **`skip` or no Figma** — omit the "Figma references" subsection from Phase 4 and add an open question in Section 11: "Figma node IDs not provided — `shop-ui-implementer` will need them before building the UI."
  - **No file open in Figma** — if the Figma MCP returns no open file, note it and surface as an open question in Section 11.

  **c. Extracting per-component node IDs** — once you have a starting frame or page node:
  1. Call `figma-console:figma_get_component_for_development` on the top-level frame to get direct children.
  2. For each child that maps to a distinct page or major component in the feature (e.g., "Sign-in form", "OTP step", "Error state"), record its node ID and a one-sentence visual intent.
  3. Do not go deeper than one level of children unless a child is itself a complex nested component that warrants its own node ID entry.

  Capture all of this in Section 7 Phase 4 (Web) of the plan using the **Figma references** subsection (see the template). If a node ID is missing or ambiguous, surface it as an open question in Section 11 — do not paper over it. Don't call Figma for non-UI features.

Both are skippable for backend-only or domain-rule features.

### 5. Plan deeply before drafting

Before typing a single section, hold these questions in mind and resolve each:

- **Which architectural layers does this feature touch?** Domain only? Application + Domain? All four? List them.
- **What are the new MediatR commands and queries?** Name each. Give the signature shape.
- **What are the new domain entities, value objects, and exceptions?** Or which existing ones are extended?
- **What new database tables, columns, indexes, and RLS policies are needed?** Don't defer this — schema changes are the highest-friction part of any feature.
- **What error keys does this introduce?** Each one needs a new entry in `Strings.resx` (and a French placeholder in `Strings.fr.resx`).
- **What new resource strings are needed for UI?** Page titles, button labels, error messages, validation messages.
- **Which acceptance criteria are at risk?** If any AC is unclear how to verify, flag it as an open question rather than glossing.
- **What's already in the codebase that this builds on or duplicates?** A quick scan of the relevant folders prevents redundant additions.

Write a brief internal sketch (not in the final plan — your own scratch reasoning) of these answers before writing the document. The plan that comes out will be substantially better for it.

### 6. Write the plan using the template below

Use the exact 11-section structure. Stay technical, stay concrete. Replace placeholders. Don't pad.

### 7. Save the file

- Path: `.specs/{file_name}/plan.md` — same `{file_name}` as the input spec; the plan lives in the spec's own feature folder.
- The `.specs/{file_name}/` directory already exists (the spec created it); create it if somehow missing.
- If a `plan.md` already exists in that folder, ask the user whether to **overwrite or cancel**. Never save under a different name (no `plan-v2.md` or similar) — `plan.md` is the canonical path every downstream skill and sub-agent reads; git history is the version archive (`git log -- .specs/{file_name}/plan.md` recovers any prior revision).
- **On overwrite, downstream artifacts are stale.** A rewritten plan invalidates whatever was resolved, implemented, or tested from the old one. After saving, reset the downstream rows in `status.md` (Implement, Test, Verify, Review, Document) back to `—` with a note `stale: plan rewritten {date}` in the Gate cell of any row that previously had a result.
- **Run the plan gate (exit gate — mandatory).** After saving, run:

  ```bash
  pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 plan -Feature {file_name}
  ```

  The script deterministically verifies the 11-section structure, the footer, and — most importantly — **AC coverage**: every `AC-n` in the spec's Section 6 must appear in the plan's Section 8 mapping. An unmapped AC is exactly the gap this plan's quality guidelines forbid, now enforced. **Exit 1 → fix the plan and re-run the gate. Never report the plan as saved while this gate fails.**

### 8. Update the status tracker

In `.specs/{file_name}/status.md`, set the **Plan** row: State `Draft`, Gate `✅ plan-gate pass` (it must pass before you get here), Evidence one line (e.g. `12/12 ACs mapped · 2 ❓ · 3 📌 · 2 ⚠️ in Section 11`), today's date; refresh **Last updated**, and point **Next step** at `/theshop.resolve {file_name}` (or `/theshop.implement {file_name}` if Section 11 has no open questions). If the spec was still `Draft` and the user told you to proceed anyway, record `⚠️ waived: spec Draft, {N} open assumption(s)` in the Gate cell instead. If `status.md` is missing — a pre-tracker feature — create it from the template in `theshop.spec` first.

### 9. Confirm

Report the saved path in one short sentence and call out anything that needs human judgment before implementation can start. If Section 11 carries any `❓ Open question` (or unratified `📌 Assumption`), point the user at `/theshop.resolve` — that's the step that turns those into settled decisions and flips the plan's Status to `Resolved`. Examples:

> "Saved to `.specs/add-to-cart/plan.md` — 2 open questions in Section 11. Run `/theshop.resolve add-to-cart` to resolve them before implementing."

> "Saved to `.specs/add-to-cart/plan.md` — no open questions. You can resolve any logged assumptions with `/theshop.resolve add-to-cart`, or proceed straight to `/theshop.implement add-to-cart`."

---

## Plan template

Use this structure exactly. Replace placeholders. Sections 1–7 are the ones the user requested; sections 8–11 are additions justified by this project's specific architecture (RLS-as-security-boundary, Result<T> with localized keys, strict layer separation).

```markdown
# Implementation Plan — {Feature Title}

> Companion to `.specs/{file_name}/spec.md`. This plan is technical (HOW); the spec is non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

{2–4 sentences stating the engineering goal. What are we building, in technical terms? Reference the spec's problem statement briefly. Example: "Add a new use case that lets an authenticated customer add a product to a server-persisted cart, enforced by domain-level invariants (max 20 distinct items per cart) and gated by RLS on the `carts` table."}

## 2. Tech Stack

{Bullet list of every technology, library, or tool this feature relies on, with the layer it lives in and why it's chosen. Keep this scoped to what's actually used — don't list every library in the project.}

- **Domain:** C# 12 (no external deps).
- **Application:** MediatR 12, FluentValidation 11, AutoMapper 13, `Result<T>` (project-internal).
- **Infrastructure:** `supabase-csharp` 1.x for persistence.
- **Web:** MudBlazor, bUnit for component tests.
- **Persistence:** Supabase (PostgreSQL 15 + RLS).

## 3. High-level Architecture

{One short paragraph + a layered diagram or ordered list showing how a single user action propagates through the layers. Be specific to this feature.}

```
User clicks "Add to Cart" in ProductDetail.razor
   ↓
IMediator.Send(AddToCartCommand)
   ↓
AddToCartHandler (Application)
   ├── IProductRepository.GetByIdAsync(...)
   ├── ICartRepository.GetForUserAsync(...) ?? Cart.CreateFor(...)
   ├── cart.AddItem(product, qty)   // Domain invariants enforced
   └── ICartRepository.SaveAsync(...)
   ↓
SupabaseCartRepository (Infrastructure) → carts + cart_items tables (RLS-gated)
   ↓
Result<CartDto> returned to ProductDetail.razor → CartState updated → UI re-renders
```

## 4. Data Model

### Domain entities & value objects
{List of new or modified entities, their key methods, and the invariants they enforce.}

- **`Cart`** (entity) — new methods: `AddItem(Product, int)`, `RemoveItem(Guid)`, `TotalPrice()`. Invariants: max 20 distinct items; quantities must be positive; throws `InsufficientStockException` and `CartCapacityExceededException`.
- **`CartItem`** (entity) — new. Fields: `ProductId`, `Price` (frozen at add-time), `Quantity`. Method: `IncreaseQuantity(int)`.

### DTOs (Application → Web)
- **`CartDto`** — fields: `Id`, `Items: IReadOnlyList<CartItemDto>`, `Subtotal: decimal`.
- **`CartItemDto`** — fields: `ProductId`, `ProductName`, `UnitPrice`, `Quantity`, `Subtotal`.

### Database tables (new or modified)
| Table | Purpose | Key columns |
|---|---|---|
| `carts` (new) | One row per customer cart | `id`, `customer_id`, `created_at`, `updated_at` |
| `cart_items` (new) | Items in a cart | `id`, `cart_id`, `product_id`, `unit_price`, `quantity` |

### Indexes
- `cart_items (cart_id)` — for the cart-by-customer query.

## 5. Core Design Decisions

{Numbered list. Each decision has: what we chose, why we chose it, and what alternatives we rejected. Tie back to spec constraints and `theshop.constitution` rule numbers where relevant (e.g. "Rule 5 — `Result<T>` for expected failures").}

1. **Decision:** Cart is server-persisted (not browser-local).
   - **Why:** Spec constraint that cart persists across devices for signed-in users. Also enables RLS-based security.
   - **Rejected:** Local-storage cart with sync — adds conflict resolution complexity for no business benefit.

2. **Decision:** Price is frozen on `CartItem` at add-time.
   - **Why:** Prevents price-change surprise at checkout. Spec edge case: "what if the price changed since add?" — frozen-at-add is the answer.
   - **Rejected:** Recompute price at read-time — invites support tickets and arguably violates customer expectations.

3. *(more decisions as needed)*

## 6. Core Functional Flow

{Walkthrough of each significant user journey from spec's Section 3, mapped to the implementation. One subsection per behavior.}

### Flow 1: Add an item to the cart

1. `ProductDetail.razor` user clicks `MudButton` bound to `AddToCart()`.
2. Page calls `Mediator.Send(new AddToCartCommand(productId, quantity))`.
3. `ValidationBehavior` runs `AddToCartCommandValidator`. On failure → `Result.Fail(nameof(Strings.{...}))`.
4. `AddToCartHandler` loads product (`IProductRepository`). If null → `Result.Fail(nameof(Strings.ProductNotFound))`.
5. Loads or creates cart (`ICartRepository`).
6. Calls `cart.AddItem(product, quantity)`. On `DomainException` → handler converts to `Result.Fail(ex.MessageKey)`.
7. Saves cart. Returns `Result.Ok(_mapper.Map<CartDto>(cart))`.
8. Page receives result; updates `CartState`; shows `Snackbar` with `Strings.AddedToCart` on success or `Localizer[result.Error]` on failure.

### Flow 2: {next behavior from spec}
{...}

## 7. Development Plan

{Ordered, sequenced list of phases. Each phase is independently committable and testable. Don't fold all work into one phase.}

### Phase 1 — Domain foundations
- Create `Cart` and `CartItem` entities in `TheShop.Domain/Entities/`.
- Create `CartCapacityExceededException` in `TheShop.Domain/Exceptions/`.
- Write Domain unit tests for invariants (`CartTests.cs`).

### Phase 2 — Application use cases
- Create `AddToCartCommand` + `AddToCartHandler` + `AddToCartCommandValidator` in `TheShop.Application/Features/Cart/Commands/`.
- Define `ICartRepository` in `TheShop.Application/Common/Interfaces/`.
- Create `CartDto` + `CartItemDto` + `AutoMapper` profile.
- Add new keys to `Strings.resx` (`AddedToCart`, `ProductNotFound`, `CartCapacityExceeded`, validator messages). Mirror keys in `Strings.fr.resx` with `[TODO]` placeholders.
- Write Application unit tests (`AddToCartHandlerTests.cs`).

### Phase 3 — Infrastructure
- Create migration: `carts` + `cart_items` tables + indexes + RLS policies.
- Create `CartRecord` + `CartItemRecord` in `TheShop.Infrastructure/Persistence/Records/`.
- Create `SupabaseCartRepository : ICartRepository`.
- Register in `TheShop.Infrastructure/DependencyInjection.cs`.
- Write integration tests with Testcontainers.

### Phase 4 — Web

**Figma references** *(required when the phase touches UI — read by `shop-ui-implementer` at impl time)*

- **File:** {full Figma file URL}
- **Nodes:**
  - `123:456` — ProductDetail page (Add to Cart CTA, quantity stepper, success toast layout)
  - `123:457` — CartIcon header badge (count + animation on update)
- **Visual intent notes:** {one sentence per node — what it is, where it fits. Not a redescription; a hook for the implementer to confirm they're looking at the right node.}

**Tasks**

- Update `ProductDetail.razor` to wire the "Add to Cart" button (matches node `123:456`).
- Update `CartState` to hold the new `CartDto`; the header `CartIcon` re-renders against node `123:457`.
- Add `Routes.Products.Detail` if missing; add `BusyKeys.Cart.Add`.
- Write bUnit component tests.

### Phase 5 — End-to-end & polish
- Run `/theshop.test add-to-cart` to verify the full test suite.
- Run `/theshop.verify add-to-cart` to smoke-test the feature against the running app (user-facing features).
- Run `/theshop.review add-to-cart` for quality + security review.
- Run `/theshop.document` to add XML doc comments once the code is final.

## 8. Acceptance Criteria → Task Mapping

{Every AC in the spec must appear here, mapped to one or more tasks above. If any AC has no mapping, mark it `⛔ UNMAPPED` and surface it in Section 11.}

| AC from spec | Maps to |
|---|---|
| AC-1: User can add an item from the product page and see it in cart | Phase 2 (`AddToCartHandler` happy path), Phase 4 (`ProductDetail.razor` wiring) |
| AC-2: Cart cannot exceed 20 distinct items | Phase 1 (`Cart.AddItem` invariant), Phase 1 tests |
| AC-3: {...} | {...} |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)
- `AddToCartCommandValidator`:
  - `ProductId` is not empty → `Strings.ProductId_Required`
  - `Quantity` between 1 and 99 → `Strings.Quantity_OutOfRange`

### Domain exceptions
- `CartCapacityExceededException` — thrown when `Cart.AddItem` would push count over 20. `MessageKey = nameof(Strings.CartCapacityExceeded)`.
- `InsufficientStockException` — thrown when `quantity > product.Stock`. `MessageKey = nameof(Strings.InsufficientStock)`.

### Result.Fail error keys (new entries in `Strings.resx`)
| Key | English text |
|---|---|
| `Strings.ProductNotFound` | "Product not found." |
| `Strings.CartCapacityExceeded` | "Your cart is full. Remove an item to add another." |
| `Strings.InsufficientStock` | "Not enough stock available." |
| `Strings.Quantity_OutOfRange` | "Please choose a quantity between 1 and 99." |

All keys must be mirrored in `Strings.fr.resx` (placeholder `[TODO]` is acceptable for the first pass).

## 10. Database Schema & RLS Policies

### Schema
```sql
CREATE TABLE carts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES auth.users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE cart_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cart_id UUID NOT NULL REFERENCES carts(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    unit_price NUMERIC(10,2) NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0)
);

CREATE INDEX idx_cart_items_cart_id ON cart_items(cart_id);
```

### RLS policies (these are the only real security boundary — per `rules/architecture-admin.md`)
```sql
ALTER TABLE carts ENABLE ROW LEVEL SECURITY;
ALTER TABLE cart_items ENABLE ROW LEVEL SECURITY;

-- Customers can only access their own cart
CREATE POLICY "carts_customer_access" ON carts
    FOR ALL USING (customer_id = auth.uid());

CREATE POLICY "cart_items_customer_access" ON cart_items
    FOR ALL USING (
        cart_id IN (SELECT id FROM carts WHERE customer_id = auth.uid())
    );

-- Admin can read all (for support)
CREATE POLICY "carts_admin_select" ON carts
    FOR SELECT USING (auth.jwt() ->> 'role' = 'admin');
```

## 11. Open Questions, Risks & Assumptions

{List anything unresolved. Each item is one of three labels:}

- **❓ Open question** — the spec didn't say, and the answer materially affects the plan. Must be answered before implementation starts.
- **⚠️ Risk** — something that could go wrong even with a correct implementation (e.g., race condition under high concurrency, third-party dependency reliability).
- **📌 Assumption** — a judgment call you made to fill a spec gap. Surface it so the team can ratify or override.

{Examples:}

- **❓ Open question:** The spec doesn't say whether a cart expires for inactive customers. Do we want a TTL? If yes, what value?
- **⚠️ Risk:** Two browser tabs adding the same product concurrently could result in duplicate `cart_items` rows. Mitigation: handle as part of Phase 3 (use `INSERT ... ON CONFLICT` on a unique `(cart_id, product_id)` index).
- **📌 Assumption:** Cart capacity of 20 distinct items is exactly that — not 20 units. A single product with quantity 50 counts as one item.

---
**Status:** Draft · **Spec:** `.specs/{file_name}/spec.md` · **Created:** {YYYY-MM-DD}

<!-- Status lifecycle: "Draft" → "Resolved" once /theshop.resolve settles every ❓ open question and ratifies every 📌 assumption in Section 11 (accepted ⚠️ risks may remain, labeled). /theshop.implement warns while the plan is still Draft. -->
```

---

## Quality guidelines

- **Every acceptance criterion must appear in Section 8 (AC → Task Mapping).** If you can't map an AC, that's a finding in Section 11 — don't quietly skip it.
- **Every edge case in the spec must have a concrete handling strategy** in Section 9 (validator catches it, domain exception throws, or handler returns `Result.Fail`).
- **Every constraint in the spec must be reflected** in either a design decision (Section 5), a validator (Section 9), or a database constraint (Section 10).
- **Use the project's actual names.** `ShopColors`, `ShopIcons`, `Strings.{KeyName}`, `Result<T>`, `nameof(Strings.X)`, `MediatR`, `MudBlazor`. No invented terminology.
- **Stay testable.** Every phase in Section 7 should be independently committable and produce something that runs.
- **Surface gaps, don't paper over them.** Section 11 (Open Questions) is where you say "we don't know yet" — and that's a feature, not a failure of the plan.
- **Keep it tight.** A good plan for a typical feature is 3–6 pages. If you're past that, either the feature is too big and should be split, or the plan is over-specifying (e.g., reading like code).

---

## Example invocations

**Example 1 — Spec exists, name provided:**

> User: invokes `theshop.plan` with feature name `add-to-cart`
>
> Skill: reads `.specs/add-to-cart/spec.md` → invokes `theshop.constitution` skill (loads `SKILL.md` + relevant `references/rules/*.md`) → optionally consults MCPs → plans deliberately → writes `.specs/add-to-cart/plan.md`, updates `.specs/add-to-cart/status.md` → "Saved to `.specs/add-to-cart/plan.md`. One open question surfaced in Section 11."

**Example 2 — No name provided:**

> User: invokes `theshop.plan`
>
> Skill: "Which spec should I generate an implementation plan for? Please give me the spec file name (e.g., `add-to-cart`)."

**Example 3 — Spec doesn't exist:**

> User: invokes `theshop.plan` with feature name `nonexistent-feature`
>
> Skill: "I couldn't find a spec at `.specs/nonexistent-feature/spec.md`. I generate implementation plans from specs — please create the spec first (the `/theshop.spec` skill helps) and re-invoke me."

**Example 4 — Plan already exists:**

> User: invokes `theshop.plan` with feature name `add-to-cart`
>
> Skill: "A plan at `.specs/add-to-cart/plan.md` already exists. Should I overwrite it or cancel? (Overwriting marks the downstream pipeline rows stale — the previous version stays recoverable via git history.)"

**Example 5 — Figma URL provided via `--figma`:**

> User: `/theshop.plan user-authentication --figma https://www.figma.com/file/XXXXX/TheShop?node-id=42-100`
>
> Skill: extracts file key `XXXXX` and node ID `42:100` from the URL → calls `figma_get_component_for_development` on node `42:100` → records child node IDs and visual intent in Phase 4 → no prompting needed.

**Example 6 — No `--figma` supplied, spec implies UI:**

> User: `/theshop.plan user-authentication`
>
> Skill: detects Phase 4 (UI) in the plan → asks: "This feature has a UI phase. Do you have a Figma link or node ID for it?" → user replies with URL or node ID → skill fetches and records IDs → continues.
>
> If user replies `skip` → Figma references section is omitted; open question logged in Section 11.

**Example 7 — Technical direction via `--desc`:**

> User: `/theshop.plan add-to-cart --desc reuse the existing CartState store; enforce a unique (cart_id, product_id) index to handle double-add --figma 123:456`
>
> Skill: parses feature name `add-to-cart`, description, and Figma node → folds the direction into the plan (CartState reuse lands in Phase 4, the unique index in Section 10 / the concurrency risk's mitigation) citing "per user direction" → anything in the description that would change product scope is flagged back to the spec instead of planned.
