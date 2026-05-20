---
name: create-plan
description: Read a feature spec from `.claude/specs/{feature_name}.md` and generate a technical implementation plan (design doc) saved to `.claude/plans/{feature_name}.md`. The plan covers architecture, data model, design decisions, validators, error handling, RLS policies, and a phased development plan — focused on HOW the feature will be built. The companion spec stays non-technical (WHAT/WHY); this plan is fully technical. Manually invoked only.
disable-model-invocation: true
---

# Create Plan

Read a feature specification and produce a thorough technical implementation plan (a design doc) at `.claude/plans/{feature_name}.md`.

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

The skill takes one required input: a **spec file name** (matching `.claude/specs/{file_name}.md`).

- If the user provided a name, use it. Strip the `.md` extension if they included it.
- If the user did **not** provide a name, stop and ask:

  > "Which spec should I generate an implementation plan for? Please give me the spec file name (e.g., `add-to-cart`). It must match an existing file at `.claude/specs/{name}.md`."

  Wait for the reply before doing anything else.

- If the named spec file does not exist, stop and tell the user:

  > "I couldn't find a spec at `.claude/specs/{name}.md`. I generate implementation plans from specs — please create the spec first (the `/create-spec` skill helps) and re-invoke me."

  Do not proceed without a spec.

---

## Workflow

### 1. Engage extended thinking

Before reading anything, prepare for a deliberate, high-effort planning session. **Think hard** about this task — every choice in the plan compounds downstream into code, tests, and reviews, so the time invested here pays off many times over.

If the user has selected the highest-capability model (Opus) and enabled extended thinking, you're already in the right setup. If not, the plan will still get written — just less thoroughly. Don't compromise on rigor regardless.

### 2. Read the spec thoroughly

Open `.claude/specs/{file_name}.md` and read every section. Extract:

- **Problem Statement** → informs the "Objective" section of the plan.
- **Functional Requirements** → each FR maps to one or more concrete development tasks.
- **Functional Behaviors** → each behavior maps to a flow that runs through your layered architecture (UI → MediatR command → handler → repository → Supabase).
- **Constraints** → each constraint must be reflected in a design decision or a validation rule.
- **Edge Cases & Error Handling** → each item must have an implementation strategy (which validator catches it, which `Result.Fail(...)` key it produces, which exception is thrown).
- **Acceptance Criteria** → every AC must map to at least one task in the development plan. No AC is allowed to be unaddressed — flag it if you can't see how it will be met.

If any spec section is empty, vague, or contradicts another, **stop and ask the user** before planning. Do not paper over gaps with invented behavior.

### 3. Load the governing documents via the `shop-guideline` skill

The architecture and design rules live inside the `shop-guideline` skill, which bundles `ARCHITECTURE.md` and `DESIGN.md` as its references. **Invoke the `shop-guideline` skill** — it will read the architecture and design guidelines for you. Do **not** try to read `ARCHITECTURE.md` or `DESIGN.md` as bare filenames, and do **not** Glob-search the project for them; let the `shop-guideline` skill surface them. This is what prevents the slow project-wide file search.

The rules these documents define are what your plan must satisfy:

- `ARCHITECTURE.md` — Clean Architecture layers, MediatR/Result<T> patterns, admin auth model, RLS rules, testing strategy, naming conventions.
- `DESIGN.md` — String resources, `Shop*` theme classes, MudBlazor-only rule, color hierarchy, typography rules.

If `CLAUDE.md` is present, it's already in context (loaded automatically by Claude Code).

### 4. Consult MCPs when they sharpen the plan (not by default)

- **MudBlazor MCP** — call when the spec implies UI components and you need to pick the right one or verify a parameter. Use `mudblazor:search_components`, `mudblazor:get_component_detail`, or `mudblazor:get_component_parameters`. Don't enumerate all components "for completeness."
- **Figma MCP** — call when the spec describes a designed UI flow and you need canonical tokens (colors, typography, spacing) or component specs. Use `figma-console:figma_get_design_system_kit` or `figma-console:figma_get_component`. For the current feature, a separate Figma page has been created for each feature — fetch and review the Figma page that corresponds to this feature, and plan the implementation to match that design exactly, because the feature must be built the same as it is designed in Figma. Plan accordingly. Don't call it for non-UI features.

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

- Path: `.claude/plans/{file_name}.md` — same `{file_name}` as the input spec.
- Create the `.claude/plans/` directory if it doesn't exist.
- If a plan at that path already exists, ask the user whether to overwrite, save with a version suffix (e.g., `add-to-cart-v2.md`), or cancel.
### 8. Confirm

Report the saved path in one short sentence and call out anything that needs human judgment before implementation can start. Example:

> "Saved to `.claude/plans/add-to-cart.md`. Two open questions surfaced in Section 11 — please review those before implementation."

---

## Plan template

Use this structure exactly. Replace placeholders. Sections 1–7 are the ones the user requested; sections 8–11 are additions justified by this project's specific architecture (RLS-as-security-boundary, Result<T> with localized keys, strict layer separation).

```markdown
# Implementation Plan — {Feature Title}

> Companion to `.claude/specs/{file_name}.md`. This plan is technical (HOW); the spec is non-technical (WHAT/WHY). Read the spec first.

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

{Numbered list. Each decision has: what we chose, why we chose it, and what alternatives we rejected. Tie back to spec constraints and ARCHITECTURE.md rules where relevant.}

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
- Update `ProductDetail.razor` to wire the "Add to Cart" button.
- Update `CartState` to hold the new `CartDto`.
- Write bUnit component tests.

### Phase 5 — End-to-end & polish
- Run `/shop-test-feature add-to-cart` to verify the full test suite.
- Run `/shop-code-review-feature add-to-cart` for quality + security review.

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

### RLS policies (these are the only real security boundary — per `ARCHITECTURE.md`)
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
**Status:** Draft · **Spec:** `.claude/specs/{file_name}.md` · **Created:** {YYYY-MM-DD}
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

> User: invokes `create-plan` with feature name `add-to-cart`
>
> Skill: reads `.claude/specs/add-to-cart.md` → reads ARCHITECTURE.md + DESIGN.md → optionally consults MCPs → plans deliberately → writes `.claude/plans/add-to-cart.md` → "Saved to `.claude/plans/add-to-cart.md`. One open question surfaced in Section 11."

**Example 2 — No name provided:**

> User: invokes `create-plan`
>
> Skill: "Which spec should I generate an implementation plan for? Please give me the spec file name (e.g., `add-to-cart`)."

**Example 3 — Spec doesn't exist:**

> User: invokes `create-plan` with feature name `nonexistent-feature`
>
> Skill: "I couldn't find a spec at `.claude/specs/nonexistent-feature.md`. I generate implementation plans from specs — please create the spec first (the `/create-spec` skill helps) and re-invoke me."

**Example 4 — Plan already exists:**

> User: invokes `create-plan` with feature name `add-to-cart`
>
> Skill: "A plan at `.claude/plans/add-to-cart.md` already exists. Should I overwrite, save as `add-to-cart-v2.md`, or cancel?"
