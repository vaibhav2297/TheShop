# Implementation Plan — Product Catalogue

> Companion to `.specs/product-catalogue/spec.md`. This plan is technical (HOW); the spec is non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Build the storefront **product catalogue** — a public, paginated, filterable, sortable grid of product cards for *The Vape Shop Sarnia*. This feature owns the **product read-side end to end**: a new `Product` domain aggregate (with pricing and stock behaviour), the `products`/`categories`/`brands` schema (RLS: public read of published products), a MediatR query that returns a filtered/sorted page plus the available filter options, and the Blazor page + `ProductCard` component matching the Figma design.

The card's **Add-to-Cart**, **Wishlist**, and **product-detail** interactions are **displayed but their callbacks are intentionally empty** in this feature — cart, wishlist, and the product-detail page each become their own dedicated features (per user direction). The spec was updated to match (FR-4/FR-5/FR-9/FR-10 and AC-3/AC-4/AC-5 now describe display-only buttons), so plan and spec are aligned. Filters are **dynamic and backend-driven**, and pagination is delivered as a **reusable service** usable by any page.

## 2. Tech Stack

- **Domain:** C# (net10.0), no external deps — `Product`, `Category`, `Brand` entities; `Money`, `ProductPricing` value objects.
- **Application:** MediatR (queries + `IPipelineBehavior`), FluentValidation, `Result<T>` (project-internal). AutoMapper is registered (`AddApplication`) but the existing code hand-builds DTOs; this feature maps `Product → ProductSummaryDto` with a small static mapper for clarity (no profile needed).
- **Infrastructure:** `supabase-csharp` Postgrest (`.From<T>()`, `.Filter`, `.Order`, `.Range`, count) for paged/filtered reads.
- **Web:** MudBlazor only (Rule 14) — `MudCard`, `MudImage`, `MudText`, `MudButton`, `MudIconButton`, `MudCheckBox`, `MudExpansionPanels`, `MudSelect`, `MudPagination`, `MudChip`, `MudSkeleton`. `IStringLocalizer<Strings>` + typed `Strings` accessor, `BusyState`.
- **Persistence:** Supabase (PostgreSQL + RLS). New migration `0002_create_product_catalogue.sql` (schema + policies + seed data).

## 3. High-level Architecture

A single "browse the catalogue" interaction propagates inward-only across the four layers (Rule 1). Filters/sort/page are view state held in the page partial and sent as one query.

```
ProductCatalogue.razor (filter panel, sort control, MudPagination)
   ↓  builds ProductCatalogueQuery(filters, sort, page, pageSize=12)
IMediator.Send(GetProductCataloguePageQuery)      + IMediator.Send(GetCatalogueFiltersQuery)
   ↓
GetProductCataloguePageHandler (Application)
   ├── IProductRepository.GetPageAsync(criteria, ct)  →  (IReadOnlyList<Product>, int totalCount)
   └── maps Product → ProductSummaryDto, wraps in PagedResult<ProductSummaryDto>
   ↓
SupabaseProductRepository (Infrastructure) → products ⋈ categories ⋈ brands  (RLS: is_published = true)
   ↓
Result<PagedResult<ProductSummaryDto>> → grid of <ProductCard> (node 2263:5552) + reusable <ShopPagination>
   • Add-to-Cart / Wishlist buttons render with EMPTY callbacks (display-only; spec aligned)
   • clicking the card body → EMPTY callback for now (product-detail page = separate feature)
```

## 4. Data Model

### Domain entities & value objects
- **`Product`** (entity, aggregate root) — `Id`, `Name`, `Description`, `ImageUrl` (nullable), `Pricing` (`ProductPricing`), `StockQuantity` (int), `IsPublished` (bool), `Category` (`Category`), `Brand` (`Brand`), `Flavour` (string?, nullable), `NicotineStrengthMg` (int?, nullable), `CreatedAt`. Behaviour: `IsDiscounted => Pricing.IsDiscounted`, `EffectivePrice => Pricing.Effective`, `IsInStock => StockQuantity > 0`. Factory `Create(...)` (invariants) + `Rehydrate(...)` (from mapper, no invariants) — mirrors `Customer`.
- **`Category`** (entity) — `Id`, `Name`, `Slug`. Reference data; drives the Category filter.
- **`Brand`** (entity) — `Id`, `Name`, `Slug`. Reference data; drives the Brand filter.
- **`Money`** (VO) — `Amount` (decimal), `Currency` (string, default `"CAD"`). Invariant: `Amount >= 0`. Equality by value.
- **`ProductPricing`** (VO) — `OriginalPrice` (`Money`, the MRP), `SalePrice` (`Money?`). Invariant: when present, `SalePrice <= OriginalPrice` (else `DomainException("Product_Pricing_SaleAboveOriginal")`). `IsDiscounted => SalePrice is not null`; `Effective => SalePrice ?? OriginalPrice`.

### Enums (Application)
- **`ProductSortOption`** — `NewestFirst` (default), `PriceLowToHigh`, `PriceHighToLow`, `NameAToZ`, `NameZToA`. Lives in `Application/Features/Products` (a use-case concept, not a domain rule).

### DTOs (Application → Web, immutable `record`s — Rule 7)
- **`ProductSummaryDto`** — `Id`, `Name`, `ImageUrl` (string?), `OriginalPrice` (decimal), `SalePrice` (decimal?), `IsDiscounted` (bool), `Currency` (string), `IsInStock` (bool), `BrandName` (string), `Flavour` (string?), `NicotineStrengthMg` (int?).
- **`PagedResult<T>`** (reusable — `Application/Common/Models`) — `Items` (`IReadOnlyList<T>`), `Page`, `PageSize`, `TotalCount`; computed `TotalPages`, `HasPrevious`, `HasNext`. The page query returns `PagedResult<ProductSummaryDto>` — no bespoke page DTO.
- **`PaginationRequest`** (reusable — `Application/Common/Models`) — `Page` (default 1), `PageSize` (default 12); `Normalized(maxPageSize)` clamps into range. Any paged query composes it.
- **Dynamic filters (backend-driven):**
  - **`CatalogueFiltersDto`** — `Groups` (`IReadOnlyList<FilterGroupDto>`). The backend decides which filters exist; the UI renders whatever it returns.
  - **`FilterGroupDto`** — `Key` (`"category"`/`"brand"`/`"flavour"`/`"nicotine"`/`"price"`), `LabelKey` (resource key), `Kind` (`FilterKind.MultiSelect | Range`), `Options` (`IReadOnlyList<FilterOptionDto>`), `Range` (`PriceRangeDto? { Min, Max }`).
  - **`FilterOptionDto`** — `Value` (string token echoed back), `Label` (string), `Count` (int?, optional facet count).
  - **`AppliedFilterDto`** — `Key` (string), `Values` (`IReadOnlyList<string>`). Applied filters travel back on the page query as `SelectedFilters` + explicit `PriceMin?`/`PriceMax?`.

### Database tables (all new)
| Table | Purpose | Key columns |
|---|---|---|
| `categories` | Category lookup for filter + FK | `id`, `name`, `slug` |
| `brands` | Brand lookup for filter + FK | `id`, `name`, `slug` |
| `products` | Catalogue rows | `id`, `name`, `description`, `image_url`, `original_price`, `sale_price` (null), `currency`, `category_id`→categories, `brand_id`→brands, `flavour` (null), `nicotine_strength_mg` (null), `stock_quantity`, `is_published`, `created_at` |

### Indexes
- `products (is_published)` — every catalogue query filters on it.
- `products (category_id)`, `products (brand_id)` — filter joins.
- `products (created_at desc)` — default `NewestFirst` sort.
- `products (original_price)` — price sort + range filter.

## 5. Core Design Decisions

1. **Decision:** This feature is the **product read-side only**; Add-to-Cart, Wishlist, and product-detail callbacks are empty stubs.
   - **Why:** User direction — cart, wishlist, and the product-detail page each become their own features. The spec was updated so FR-4/FR-5/FR-9/FR-10 and AC-3/AC-4/AC-5 now describe **display-only** buttons; plan and spec are aligned.
   - **Consequence:** No `carts`/`wishlist` tables, no cart/wishlist commands, no `CartState` change. `ProductCard` exposes `OnAddToCart`/`OnToggleWishlist`/card-body-click as empty handlers (`// TODO: wired by the Cart / Wishlist / Product-detail features`).
   - **Rejected:** Building minimal cart/wishlist backends now — duplicates the future features.

2. **Decision:** `Product` is a rich aggregate with a `ProductPricing` VO, not an anemic row.
   - **Why:** Discount ("sale vs. struck MRP") and stock/availability are genuine business rules (Rule 6). Freezing them on the entity keeps the handler and card dumb.
   - **Rejected:** Read-model projection straight from the repository — loses the `SalePrice <= OriginalPrice` invariant and scatters discount logic into the UI.

3. **Decision:** One query for the **page** (`GetProductCataloguePageQuery`) and a separate query for the **filters** (`GetCatalogueFiltersQuery`).
   - **Why:** Filter groups are stable across pagination; recomputing them on every page turn is wasteful.
   - **Rejected:** Bundling both — couples pagination to filter recomputation.

4. **Decision:** Filters are **dynamic and backend-driven** (per user direction). `GetCatalogueFiltersQuery` returns `CatalogueFiltersDto` = a list of `FilterGroupDto` (Category/Brand/Flavour/Nicotine as `MultiSelect`, Price as `Range`). The **UI renders whatever groups the backend returns** — it hard-codes no filter set. Option values come from: `categories`/`brands` lookup tables; `flavour` and `nicotine_strength_mg` from distinct values of published products in the `products` table (the catalogue's source of truth, seeded initially via `0002` — admin CRUD is a separate feature); price bounds from min/max effective price.
   - **Why:** Extensible — adding a filter dimension is a backend change; the UI needs no edit. Dissolves the design-vs-spec filter-label mismatch (the Figma Color/Size groups are visual reference only).

5. **Decision:** A **server-side filter-definition registry** maps each filter `Key` → its column/predicate and how to enumerate its options (a `ProductFilterDefinition` list in Infrastructure). Adding a new filter = add a column + register one definition.
   - **Why:** Keeps the "plug-and-play" filter promise honest and the repository query generic (no per-filter `if` ladder).

6. **Decision:** A **reusable, generic pagination service** (per user direction). `PagedResult<T>` + `PaginationRequest` live in `Application/Common/Models` (any feature reuses them); a `ShopPagination` MudBlazor component (`Components/Common/`) wraps `MudPagination`, takes pagination metadata + `EventCallback<int> PageChanged`, and is drop-in on any page — `<ShopPagination Metadata="@page" PageChanged="OnPageChanged" />`.
   - **Why:** "Simple yet flexible" — one shared model + one component; each page owns only its page state and re-query. Inherits `MudComponentBase`, forwards `Class`/`Style` (Rules 23–24).
   - **Rejected:** Inlining `MudPagination` per page — re-implements paging math/metadata everywhere.

7. **Decision:** CAD prices render correctly in **EN and FR** via a culture-aware currency formatter (a Web helper honouring the active culture — `en-CA` → `$12.99`, `fr-CA` → `12,99 $`).
   - **Why:** Spec constraint, confirmed by user. No format literals in `.razor`.

8. **Decision:** MudBlazor-only (Rules 14–15). `ProductCard` uses `MudCard`; struck MRP via a MudBlazor text-decoration utility class — **no hex** in `.razor`. Out-of-stock **hides** the Add-to-Cart button (spec AC-11) and shows an out-of-stock indicator; the Wishlist button stays.

9. **Decision (accepted deviation):** The card-body **product-detail navigation is an empty callback for now** (detail page is a separate feature). Spec FR-11/AC-9 still describe navigation — reconcile via `/theshop.clarify` when the detail feature is scheduled (§11).

## 6. Core Functional Flow

### Flow 1: Browse & paginate (Behaviors 1 & 4)
1. `ProductCatalogue` (`[Route(Routes.Products)]`, `[AllowAnonymous]`) `OnInitializedAsync` runs both queries inside `BusyState.RunAsync(BusyKeys.Products.Catalogue, …)`; `MudSkeleton` tiles show while busy (spec loading edge case).
2. `GetProductCataloguePageHandler` calls `IProductRepository.GetPageAsync(criteria, ct)` → `(items, totalCount)`; maps to `ProductSummaryDto`; returns `PagedResult<ProductSummaryDto>` (computes `TotalPages`).
3. Grid renders 12 `<ProductCard>` (node 2263:5552). The reusable `<ShopPagination>` (node 2263:5564) is bound to the `PagedResult` metadata; its `PageChanged` re-sends the query with the **same filters/sort** (AC-8).

### Flow 2: Filter — dynamic (Behavior 3)
1. On load, `GetCatalogueFiltersQuery` returns `CatalogueFiltersDto.Groups`; `ProductFilterPanel` (node 2263:5565) **renders one control per group** by `Kind` (checkbox list for `MultiSelect`, range for `Price`) — no hard-coded filter set.
2. A change raises a filter-changed callback carrying `SelectedFilters` (`AppliedFilterDto` list) + price range; page resets `Page = 1` (spec constraint) and re-sends `GetProductCataloguePageQuery`.
3. Empty result → grid replaced by `Empty_NoResults` + a **Clear filters** action (node 2263:5611) that resets criteria (AC-10).

### Flow 3: Sort (Behavior 3)
1. `ProductSortControl` (node 2263:5544, `MudSelect<ProductSortOption>`) raises sort-changed.
2. Page resets `Page = 1`, re-sends query with the new `ProductSortOption`; repository maps it to `.Order(...)` (AC-7).

### Flow 4: Card actions — display only (Behavior 2)
1. `ProductCard` renders **Add-to-Cart** (`MudButton`, `ShopIcons.Outlined.Shopping_Cart_01`) — **hidden** when `!IsInStock`, alongside an out-of-stock indicator (AC-11); `OnAddToCart` is an **empty callback** (delivered by the Cart feature).
2. **Wishlist** (`MudIconButton`, `ShopIcons.Outlined.Heart_01`, accessible label) — `OnToggleWishlist` is an **empty callback** (delivered by the Wishlist feature).
3. Clicking the card body → **empty callback** for now (AC-9 navigation delivered by the product-detail feature).

## 7. Development Plan

### Phase 1 — Domain foundations
- `TheShop.Domain/ValueObjects/Money.cs`, `ProductPricing.cs`.
- `TheShop.Domain/Entities/Product.cs`, `Category.cs`, `Brand.cs` (`Create` + `Rehydrate`).
- Reuse `DomainException` for the pricing invariant (key `Product_Pricing_SaleAboveOriginal`).
- Domain unit tests: `MoneyTests`, `ProductPricingTests` (discount/effective/invariant), `ProductTests` (`IsDiscounted`/`EffectivePrice`/`IsInStock`).

### Phase 2 — Application use cases
- `Common/Models/PagedResult.cs` + `PaginationRequest.cs` — **reusable** paging primitives (not catalogue-specific).
- `Features/Products/ProductSortOption.cs`; `Features/Products/FilterKind.cs`.
- `Features/Products/Queries/GetProductCataloguePage/` — `GetProductCataloguePageQuery` (`SelectedFilters`, `PriceMin?`, `PriceMax?`, `Sort`, `PaginationRequest`) + `Handler` (→ `Result<PagedResult<ProductSummaryDto>>`) + `Validator`.
- `Features/Products/Queries/GetCatalogueFilters/` — `GetCatalogueFiltersQuery` + `Handler` (→ `Result<CatalogueFiltersDto>`).
- `Features/Products/DTOs/` — `ProductSummaryDto`, `CatalogueFiltersDto`, `FilterGroupDto`, `FilterOptionDto`, `AppliedFilterDto`, `PriceRangeDto`; `ProductDtoMapper` (static, `Product → ProductSummaryDto`).
- `Common/Interfaces/IProductRepository.cs` — `GetPageAsync(ProductCatalogueCriteria, ct)` → `(items, totalCount)`, `GetFilterGroupsAsync(ct)`. (`ProductCatalogueCriteria` = Application input record.)
- `Features/Products/ProductErrorKeys.cs` (mirrors resx). Add error/query keys to `Strings.resx` + `Strings.fr.resx` (Application may edit resx — scope rule).
- Application unit tests: both handlers (mapping, `PagedResult` math, empty result), validator (page/pageSize/price range/sort/unknown filter key).

### Phase 3 — Infrastructure
- Migration `supabase/migrations/0002_create_product_catalogue.sql` — `categories`, `brands`, `products` + indexes + RLS (Section 10) + seed. Applied via Supabase MCP `apply_migration`.
- `Persistence/Records/` — `ProductRecord`, `CategoryRecord`, `BrandRecord` (`[Table]`/`[Column]` like `CustomerRecord`).
- `Persistence/Mappers/ProductMapper.cs` (record⋈join → `Product` via `Rehydrate`).
- `Persistence/Products/ProductFilterDefinitions.cs` — the **filter-definition registry**: each entry = `Key` + option-source (lookup table vs distinct column) + predicate builder. Both `GetFilterGroupsAsync` and the `GetPageAsync` predicate loop read it, so a new filter is one new definition.
- `Persistence/Repositories/SupabaseProductRepository.cs : IProductRepository` — Postgrest `.Filter`/`.Order`/`.Range` + count for `GetPageAsync` (applied filters resolved through the registry); definitions + min/max for `GetFilterGroupsAsync`.
- Register in `Infrastructure/DependencyInjection.cs`.
- Repository integration test(s) as feasible against Supabase test schema.

### Phase 4 — Web

**Figma references** *(read by `shop-ui-implementer` at impl time — both nodes fetched via figma-console: `2263:5531` = populated page, `2380:2571` = empty-state page)*

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Nodes:**
  - `2263:5531` — **Product Catalog page** (desktop, 1440w): left filters sidebar + right product section, 3×4 grid, pagination, breadcrumb + appbar + footer already exist as components.
  - `2263:5537` — Title block ("Products").
  - `2263:5544` — **Sort control** (label + chips; map to a `MudSelect<ProductSortOption>` with our 5 options).
  - `2263:5551` — Products grid container (holds 12 tiles → confirms page size 12).
  - `2263:5552` — **Product Tile / `ProductCard`** — image, name, price (sale + struck MRP), Add-to-Cart + Wishlist buttons.
  - `2263:5564` — **Pagination** → `MudPagination`.
  - `2263:5565` — **Filters sidebar**: `2263:5566` Category, `2263:5577` Price, plus two generic groups (`2263:5588`, `2263:5599`) and Clear-filters button `2263:5611`.
  - `2380:2571` — **Product Catalog – Empty** (the page's empty-state variant): same chrome as `2263:5531`, with an **empty Product Section** (`2380:3069` = empty content region) beside the Filters sidebar (`2380:2605`). Use for the AC-10 / no-products empty-state layout. (The `ProductCard` master is fetched from a tile instance on `2263:5531`, e.g. `2263:5552`.)
- **Filters are backend-driven:** `ProductFilterPanel` renders one control per `FilterGroupDto` returned by `GetCatalogueFiltersQuery` — it does **not** hard-code the Figma's Color/Size groups. Treat the Figma filter sidebar as visual/styling reference for the group + checkbox + range controls. Sort shows chips in design → `MudSelect<ProductSortOption>` with our 5 options.

**Tasks**
- `Components/Common/ShopPagination.razor` (+`.razor.cs`) — **reusable** pagination service component: inherits `MudComponentBase`, forwards `Class`/`Style`, takes pagination metadata (`Page`/`TotalPages` or a `PagedResult<T>`) + `EventCallback<int> PageChanged`, wraps `MudPagination`. Drop-in on any page.
- `Common/CurrencyFormatter.cs` — culture-aware CAD formatter (`en-CA`/`fr-CA`) used by `ProductCard`; no format literals in markup.
- `Common/BusyKeys.cs` — add `public static class Products { public const string Catalogue = "products.catalogue"; }`. (Product-detail route deferred with its feature — the card-body click is an empty callback for now.)
- `Pages/Products/ProductCatalogue.razor` + `.razor.cs` — `[Route(Routes.Products)]`, `[AllowAnonymous]`, `<PageTitle>@Strings.Products_PageTitle</PageTitle>`; holds filter/sort/page state, dispatches both queries via `IMediator`, drives `BusyState`. Keep the partial lean (Rule 10) — the criteria record does the heavy lifting.
- `Components/Products/ProductCard.razor` (+`.razor.cs`) — inherits `MudComponentBase`, forwards `Class`/`Style` (Rules 23–24); `MudImage` with placeholder fallback (AC-12) + `Product_ImageAlt`; name via `MudText` (truncated); price block (sale prominent + struck MRP via text-decoration utility, or single price) formatted through `CurrencyFormatter`; Add-to-Cart + Wishlist buttons + card-body click all with **empty** callbacks; Add-to-Cart **hidden** + `OutOfStock` indicator when `!IsInStock`.
- `Components/Products/ProductFilterPanel.razor`, `ProductSortControl.razor` — `ProductFilterPanel` renders **one control per backend `FilterGroupDto`** by `Kind` (MudBlazor checkbox list / range); `ProductSortControl` is a `MudSelect<ProductSortOption>`.
- Grid + `<ShopPagination>` + empty state (`Empty_NoResults` + clear-filters) in the page.
- Strings: reuse `Products_PageTitle`, `AddToCart`, `Product_ImageAlt`, `Product_StockWarning`, `Empty_NoResults`, `ProductNotFound`; **add** filter/sort/wishlist/pagination/currency labels + ARIA (Section 9 list) to `Strings.resx` **and French** in `Strings.fr.resx` (review has a FR-completeness gate).
- bUnit tests: `ShopPagination` (page-change callback, boundary states); `ProductCard` (discount vs single price, placeholder, out-of-stock hides Add-to-Cart); page (filter resets page, sort re-queries, pagination preserves filters, empty state).

### Phase 5 — End-to-end & polish
- `/theshop.test product-catalogue` (writer + runner).
- `/theshop.verify product-catalogue` (user-facing — smoke the running grid/filter/sort/pagination).
- `/theshop.review product-catalogue` (security + quality + FR-localization gate).
- `/theshop.document` (XML docs on the final diff).

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| **AC-1** grid: image, name, price | Ph1 `Product`/`ProductPricing`; Ph2 query/handler + `ProductSummaryDto`; Ph4 `ProductCatalogue` grid + `ProductCard` (node 2263:5552) |
| **AC-2** discounted → sale + struck MRP; else single price | Ph1 `ProductPricing.IsDiscounted`/`Effective`; Ph4 `ProductCard` price block |
| **AC-3** card displays Add-to-Cart button; anyone can browse | Ph4 Add-to-Cart button rendered on `ProductCard` (empty callback — action delivered by the Cart feature; spec aligned) |
| **AC-4** card displays Wishlist button | Ph4 Wishlist button rendered (empty callback — action delivered by the Wishlist feature; spec aligned) |
| **AC-5** activating a card button does nothing here | Ph4 `OnAddToCart`/`OnToggleWishlist` empty callbacks — no navigation, no state change (spec aligned) |
| **AC-6** filter narrows + resets to page 1 | Ph2 query filters + validator; Ph3 repo `.Filter`; Ph4 `ProductFilterPanel` + `Page=1` reset |
| **AC-7** sort reorders | Ph2 `ProductSortOption` + query; Ph3 repo `.Order`; Ph4 `ProductSortControl` |
| **AC-8** pagination keeps filter/sort | Ph2 paged query; Ph3 `.Range` + count; Ph4 `MudPagination` preserving criteria |
| **AC-9** select card → detail page | Ph4 `ProductCard` body click = empty callback for now — navigation delivered by the product-detail feature (accepted deviation, §11) |
| **AC-10** no match → empty state + clear filters | Ph2 handler returns empty page; Ph4 `Empty_NoResults` + clear-filters (node 2263:5611); empty-state layout per node `2380:2571` |
| **AC-11** out-of-stock → Add-to-Cart hidden + indicator, wishlist shown | Ph1 `Product.IsInStock`; Ph4 `ProductCard` hides Add-to-Cart + shows `OutOfStock`, Wishlist stays |
| **AC-12** no image → placeholder | Ph4 `ProductCard` `MudImage` fallback placeholder |
| **AC-13** all text EN + FR | Ph2 error/label keys; Ph4 UI strings — every key in `Strings.resx` + `Strings.fr.resx` |
| **AC-14** keyboard-operable, visible focus, accessible labels | Ph4 MudBlazor semantics + visible focus; Add-to-Cart/Wishlist buttons carry `aria-label`s (`Wishlist_Add`/`AddToCart`) |

## 9. Validation & Error Handling Strategy

### Validators (Application)
- `GetProductCataloguePageQueryValidator`:
  - `Page >= 1` → `Catalogue_Page_Invalid`
  - `PageSize` in `1..48` (default 12) → `Catalogue_PageSize_Invalid`
  - `PriceMin <= PriceMax` when both present → `Catalogue_PriceRange_Invalid`
  - `Sort` is a defined `ProductSortOption` → `Catalogue_Sort_Invalid`
  - each `SelectedFilters` key is a known filter key → `Catalogue_Filter_Invalid`

### Domain invariants (throw → unexpected only)
- `Money`: `Amount >= 0` → `DomainException("Money_Negative")`.
- `ProductPricing`: `SalePrice <= OriginalPrice` → `DomainException("Product_Pricing_SaleAboveOriginal")`.
- These guard seed/rehydration correctness; they are not expected user failures.

### Result.Fail / resource keys (new entries — `Strings.resx` + `Strings.fr.resx`)
| Key | English text |
|---|---|
| `Catalogue_Page_Invalid` | "Invalid page number." |
| `Catalogue_PageSize_Invalid` | "Invalid page size." |
| `Catalogue_PriceRange_Invalid` | "The minimum price must be less than the maximum." |
| `Catalogue_Sort_Invalid` | "Unknown sort option." |
| `Catalogue_Filter_Invalid` | "Unknown filter." |
| `Filter_Category` / `Filter_Price` / `Filter_Brand` / `Filter_Flavour` / `Filter_NicotineStrength` | "Category" / "Price" / "Brand" / "Flavour" / "Nicotine strength" |
| `Filter_Clear` | "Clear filters" |
| `Sort_Label` / `Sort_Newest` / `Sort_PriceLowHigh` / `Sort_PriceHighLow` / `Sort_NameAZ` / `Sort_NameZA` | "Sort by" / "Newest" / "Price: low to high" / "Price: high to low" / "Name: A–Z" / "Name: Z–A" |
| `Wishlist_Add` / `Wishlist_Saved` | "Add to wishlist" / "Saved to wishlist" (ARIA) |
| `OutOfStock` | "Out of stock" |
| `Catalogue_ResultCount` | "Showing {0} products" |
| `Product_Price_Was` | "Was {0}" (ARIA for struck MRP) |

Reused existing keys: `Products_PageTitle`, `AddToCart`, `AddedToCart`, `Product_ImageAlt`, `Product_StockWarning`, `Empty_NoResults`, `ProductNotFound`.

### Edge cases → handling
| Spec edge case | Handling |
|---|---|
| No image | `ProductCard` `MudImage` placeholder fallback (AC-12) |
| Out of stock | `Product.IsInStock == false` → Add-to-Cart **hidden** + `OutOfStock` indicator; Wishlist shown |
| Filter/sort → no match | handler returns empty `Items`; UI shows `Empty_NoResults` + clear-filters |
| Catalogue empty | same empty-state path |
| Long product name | `MudText` truncation utility (no overflow) |
| Loading | `MudSkeleton` tiles under `BusyFor Key="BusyKeys.Products.Catalogue"` |
| CAD in EN/FR | `CurrencyFormatter` honours the active culture (`$12.99` / `12,99 $`) |

## 10. Database Schema & RLS Policies

### Schema (migration `0002_create_product_catalogue.sql`)
```sql
CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE
);

CREATE TABLE brands (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE
);

CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    image_url TEXT,
    original_price NUMERIC(10,2) NOT NULL CHECK (original_price >= 0),
    sale_price NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price >= 0),
    currency TEXT NOT NULL DEFAULT 'CAD',
    category_id UUID NOT NULL REFERENCES categories(id),
    brand_id UUID NOT NULL REFERENCES brands(id),
    flavour TEXT,
    nicotine_strength_mg INTEGER CHECK (nicotine_strength_mg IS NULL OR nicotine_strength_mg >= 0),
    stock_quantity INTEGER NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
    is_published BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT sale_not_above_original CHECK (sale_price IS NULL OR sale_price <= original_price)
);

CREATE INDEX idx_products_published    ON products(is_published);
CREATE INDEX idx_products_category     ON products(category_id);
CREATE INDEX idx_products_brand        ON products(brand_id);
CREATE INDEX idx_products_created_at   ON products(created_at DESC);
CREATE INDEX idx_products_price        ON products(original_price);
-- + seed ~15–20 vape products across categories/brands/flavours/strengths,
--   including discounted, out-of-stock, and one image-less row.
```

### RLS policies (the only real security boundary — per `architecture-admin.md`)
```sql
ALTER TABLE categories ENABLE ROW LEVEL SECURITY;
ALTER TABLE brands     ENABLE ROW LEVEL SECURITY;
ALTER TABLE products   ENABLE ROW LEVEL SECURITY;

-- Public storefront read: anyone (anon or authenticated) may read reference data
CREATE POLICY "categories_public_read" ON categories FOR SELECT USING (true);
CREATE POLICY "brands_public_read"     ON brands     FOR SELECT USING (true);

-- Public storefront read: only PUBLISHED products are visible to the storefront
CREATE POLICY "products_public_read" ON products
    FOR SELECT USING (is_published = true);

-- No INSERT/UPDATE/DELETE policies → writes are denied to storefront roles.
-- Admin product management (and any write policy) is a separate future feature.
```

## 11. Open Questions, Risks & Assumptions

All open questions are answered and all assumptions ratified — folded into Sections 1–10. One risk is knowingly accepted:

- **⚠️ Risk — ✅ Accepted:** The card-body **product-detail navigation is an empty callback** (detail page is a separate feature). Spec FR-11/AC-9 still describe navigation, so `/theshop.test` may assert it — reconcile the spec via `/theshop.clarify` when the product-detail feature is scheduled. Rationale: don't wire a link to a page that doesn't exist yet.

Both Figma nodes are now fetched and pinned in §7 Phase 4 — `2263:5531` (populated page) and `2380:2571` (empty-state page). Ratified decisions now living in the plan body: spec aligned to display-only card actions (§1, §5.1); products read from the `products` table, seeded via `0002` (§5.4); filters dynamic & backend-driven with a filter-definition registry (§4, §5.4–5.5); `flavour`/`nicotine_strength_mg` nullable (§4, §10); reusable `PagedResult<T>` + `ShopPagination` pagination service (§4, §5.6, §7); culture-aware CAD formatting in EN/FR (§5.7, §9).

---
**Status:** Resolved · **Spec:** `.specs/product-catalogue/spec.md` · **Created:** 2026-07-01 · **Resolved:** 2026-07-02

<!-- Status lifecycle: "Draft" -> "Resolved" once /theshop.resolve settles every open question and ratifies every assumption in Section 11 (accepted risks may remain, labeled). /theshop.implement warns while the plan is still Draft. -->
