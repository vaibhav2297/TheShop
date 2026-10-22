# Implementation Plan — Variant Filters

**Feature:** `002_variant-filters`

Companion: `.specs/002_variant-filters/spec.md` (Confirmed).

## 1. Objective

Extend existing catalogue and management filtering with configured variant options. Aggregate choices independently of current criteria; evaluate every selected group against one eligible variant before counting and paging. Public eligibility uses `product_variants.is_available`; management includes both values. Preserve existing price, stock, publication, and permission behavior.

## 2. Tech Stack

- **Domain:** Existing C# `Product`, `ProductVariant`, and read-model fields; no changes.
- **Application:** Existing MediatR, FluentValidation, immutable DTOs, `Result<T>`, authorization/exception behaviors.
- **Infrastructure:** Installed Supabase 1.1.1 / Postgrest 4.0.3; Newtonsoft.Json RPC records.
- **Web:** Existing Blazor pages, semantic HTML, `ShopFilterPanel`, `ShopCheckbox`, `ShopDrawer`, `ShopButton`, project SCSS.
- **Persistence/tests:** PostgreSQL/RLS; xUnit, NSubstitute, Testcontainers/Npgsql, bUnit, existing Playwright journeys. No dependency changes.

## 3. High-level Architecture

Existing pages own URL/navigation and presentation. Application reconciles requested variant selections against authorized metadata. Infrastructure performs relational matching and paging. Existing aggregate-to-summary mapping remains authoritative for public cards.

```text
ProductCatalogue / ManageProducts: FilterToggled
  PushStateAsync -> CatalogueQueryState / ProductQueryState
  IMediator.Send(GetProductCataloguePageQuery / GetAdminProductsPageQuery)
  authorization -> validation -> handler
  IProductRepository metadata read -> VariantFilterSelection reconciliation
  IProductRepository.GetPageAsync / GetAdminPageAsync
  SupabaseProductRepository -> catalogue_products_page / admin_products_page
  product_matches_variant_filters -> one variant satisfies every selected group
  Result<ProductListingPageDto<TItem>> -> Paginator + effective selections + notice
```

Reuse `GetCatalogueFiltersQuery` / `GetAdminProductFiltersQuery` for initial panel metadata. Page handlers refresh variant metadata when applying criteria; returned groups keep controls aligned with reconciliation. No page injects repositories. Existing `RequiresPermission("products.view")` and route gates remain.

## 4. Data Model

### Domain entities & value objects

Reuse `ProductVariant.IsAvailable`, `Product.Rehydrate`, `ProductPricing`, `Money`, and existing product rollups. Active means `is_available = true`, as existing `ProductVariantsCard` status control already defines. Deletion is physical; no soft-delete or stock columns added.

### DTOs (Application → Web)

| Contract | Planned change |
|---|---|
| `Common/Filtering/FilterGroupDto` | Append optional `string? Label = null`; merchant labels bypass resource lookup. Existing constructors retain behavior. |
| `AdminProductFiltersDto` | Add `IReadOnlyList<FilterGroupDto> VariantGroups`; update explicit construction sites. Keep Brands, Categories, PriceRange. |
| `GetAdminProductsPageQuery`, `AdminProductCriteria` | Append optional `IReadOnlyList<AppliedFilterDto>? VariantFilters = null`; null means no variant restriction. |
| `ProductCatalogueCriteria` | Keep existing shape; `SelectedFilters` carries fixed and `variant:` groups. |
| `ProductListingPageDto<TItem>` (new, Products/DTOs) | `PagedResult<TItem> Page`, `IReadOnlyList<FilterGroupDto> VariantGroups`, `IReadOnlyList<AppliedFilterDto> EffectiveVariantFilters`, `bool RemovedVariantSelections`. |
| New `GetProductCatalogueListingQuery` / `GetAdminProductListingQuery` | Wrap existing page queries; return `Result<ProductListingPageDto<ProductSummaryDto>>` / `Result<ProductListingPageDto<ProductListItemDto>>`. Existing page-query responses and repository signatures remain unchanged. |

New `Features/Products/Filtering/VariantFilterSelection` reconciles dynamic keys/values, merges duplicate groups, removes unknown/empty selections, and reports removal. Keep fixed filters separate and unchanged. New small result record holds effective variant filters and removal flag; no new service/DI registration.

### Database tables (new or modified)

| Table | Use | Relevant columns |
|---|---|---|
| `products` (unchanged) | Visibility, existing criteria/prices | `id`, `is_published`, `brand_id`, `category_id`, `display_price` |
| `product_variants` (unchanged) | Eligible variant | `id`, `product_id`, `is_available` |
| `product_option_types`, `product_option_values` (unchanged) | Configured names/values | `product_id`, `option_type_id`, `name`, `value` |
| `product_variant_option_values` (unchanged) | Actual variant assignments | `variant_id`, `option_value_id` |

### Indexes

Reuse variant product index, option-type/value normalized unique indexes, assignment primary key, and existing reverse assignment index from 0025. No speculative index or schema column.

## 5. Core Design Decisions

1. **Dynamic identity:** Use database-produced key `variant:` plus `lower(btrim(option type name))`; choice token is `lower(btrim(value))`. Ignore capitalization and surrounding spaces when merging group names and values across products. Different spellings, including `Colour` and `Color`, remain separate; no synonym mapping. Preserve deterministic configured display spelling. URL helpers encode keys/values once. **Why:** Option IDs belong to individual products; existing uniqueness rules already normalize names. **Rejected:** Per-product IDs or hardcoded Colour/Size keys; neither provides reusable catalogue groups.
2. **Shared presentation:** Add raw `Label` to existing `FilterGroupDto`; use `Label ?? Localizer[LabelKey]` in panel heading and range accessible name. Dynamic groups use `ProductFilterKeys.VariantLabelKey = "Filter_Variant"` as fallback. Encode merchant content normally. **Why:** Existing checkbox/drawer behavior already supports multi-select. **Rejected:** New filter component or treating merchant names as resource keys. Constitution Rules 11, 14, 17, 25.
3. **Server matching:** Use correlated `EXISTS` with nested `NOT EXISTS` over selected groups. No matching join multiplies product rows. Keep price predicates separate: matching option variant need not be price-matching variant; existing price semantics remain. **Why:** RULE-3/4/8 and unchanged price behavior. **Rejected:** Filtering downloaded pages or combining matches from separate variants.
4. **Explicit scope:** Public functions always require published products and available variants, even for signed-in staff. Admin metadata/page functions independently require live `products.view`. Helpers retain invoker RLS; management page retains its guarded definer boundary. **Why:** Client claims/scope flags cannot grant access. **Rejected:** Using caller privilege to broaden public results. Constitution Rules 1–5; RULE-1/8.
5. **Reconciliation before paging:** Handlers obtain eligible groups, reconcile variant selections, then query effective criteria. Successful response carries effective selections, current variant groups, and removal flag. Web replaces stale URL entry once, retains other criteria, and shows generic notice without naming hidden options. **Why:** RULE-7 and FR-10 require observable cleanup. **Rejected:** Silent dropping, zero-match dead ends, or new fields on shared `Result`/`PagedResult`.
6. **Compatibility:** Keep fixed category/brand query keys, enum sorts, page sizes, routes, and price fields. Dynamic keys use reserved `variant:` prefix; query-state parsers retain that prefix for reconciliation. Admin RPC gains final defaulted JSONB argument after dropping old SQL signature in one transaction; old named HTTP calls still resolve. **Rejected:** Competing RPC overloads with ambiguous optional parameters or rewriting existing migrations.
7. **Failure presentation:** Add page-owned error keys, not busy flags. Failed metadata/results render retry before empty/results/count UI; success clears error. Reuse `BusyKeys.Products.Filters`, `.Catalogue`, `.ManageList`, and existing notification service. **Why:** Existing fetch delegates return empty pages on failure; without error precedence this falsely appears as no matches. **Rejected:** Changing shared paginator behavior or adding a new global loading framework.

Supabase RPC signatures installed locally lack `CancellationToken`. Keep cancellation on every boundary; check token before RPC, await through `.WaitAsync(ct)`, and recheck before adopting results. Accept that superseded requests may finish at transport level; only latest selections may update results, groups, selections, notices, or URL. `QueryStatePageBase`/`Paginator` must discard superseded results. Cover selection cleared before response and page disposal. No transport cancellation adapter or SDK upgrade in this feature.

## 6. Core Functional Flow

### Flow 1: Discover available options

1. Existing metadata query reaches repository and existing filter RPC.
2. RPC includes `variant_filter_options(false/true)` under `option_types`, preserving existing brand/category/price fields and legacy option `name`/`values` fields.
3. Infrastructure projects nonempty dynamic groups with `FilterKind.MultiSelect`, raw configured labels, and canonical tokens.
4. Both sidebar and drawer consume same groups/selections. Fetch metadata before first state application; failed metadata blocks successful-results presentation and enables retry.

Metadata and results may briefly reflect different moments during another user's edit. A choice loaded before deactivation may remain offered while the subsequent public result query excludes the inactive variant. Accept this timing window; next listing load refreshes choices and reconciles invalid selections. No consistent cross-read snapshot or live synchronization required.

### Flow 2: Combine selections

1. Existing atomic `FilterToggle` merges into current state and pushes URL with page 1.
2. Page query passes through existing authorization/validation behaviors. Validator accepts fixed selectable keys and `variant:` namespace; existing pagination/price/sort rules remain.
3. Handler reconciles dynamic selections against scoped metadata; repository serializes effective dynamic groups as JSON object mapping group keys to string arrays.
4. SQL requires one eligible variant with one chosen value from every selected group, alongside unchanged fixed restrictions. Count/order/page only matching products; map full prices/counts independently.

### Flow 3: Refine or clear

Existing toggle/removal/clear handlers retain their semantics. Empty variant groups disappear from request only; metadata choices stay constant across search/filter/page changes. Management state application clears row selection/references as before.

### Flow 4: Navigate filtered results

`CatalogueQueryState` and `ProductQueryState` preserve repeated dynamic query keys and canonical tokens using existing URI helpers. Keep `variant:colour=red&variant:colour=blue` as repeated-key semantics, with URI escaping owned by helpers. Browser navigation follows existing cancellation path. Paging preserves filters/sort; only criteria changes reset page.

### Flow 5: Recover from missing choices or loading failure

On reconciliation, replace dynamic selections with effective values and current groups, notify once via `Strings.VariantFilters_SelectionsRemoved`, and replace stale URL entry without discarding fixed criteria. Do not reset page solely for URL cleanup; removing restrictions broadens matches. Replacement re-entry must be idempotent and cancellation-safe. Metadata/results failure shows error and `Strings.VariantFilters_Retry`; retry reloads metadata and current criteria through same busy/state path. Never clear selections or claim zero matches because metadata failed.

## 7. Development Plan

### Step 1 — Domain

No changes. Existing variant availability and read-model contracts suffice; no new entity behavior or Domain tests.

### Step 2 — Application

**Depends on:** Resolved plan; existing Domain API.

- [ ] **TASK-001** — Add compatible group-label contract, dynamic key helpers, admin filter/query/criteria fields, `ProductListingPageDto<TItem>`, and pure selection reconciliation under Products. Cover prefix separation, duplicate groups, empty/unknown values, preserved fixed criteria, and removal flag in Application tests. Preserve database-issued canonical keys/tokens without applying a second normalization convention.
- [ ] **TASK-002** — Update both page handlers/validators and their response consumers/mocks. Load scoped metadata before page read, pass effective filters only, return reconciliation metadata, preserve authorization/cancellation. Extend existing handler/validator/filter-handler tests, including failed metadata never calling page repository and authorized admin/public scope differences.

**Completion gate:** Application builds; focused tests pass; DTOs/query/repository signatures compile at consumers; input rules remain Application-owned. Record changed contracts before later layers.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `FilterGroupDto.Label`, `ProductFilterKeys` dynamic namespace | Application | Infrastructure, Web | Stable |
| Admin query/criteria/filter DTO additions | Application | Infrastructure, Web | Stable |
| Listing query responses / reconciliation result | Application | Web | Stable |
| Existing `IProductRepository` method signatures | Application | Infrastructure | Stable; metadata semantics/documentation extended |

Mark planned contracts Stable after Application completion. No Infrastructure/Web work before required rows are Stable.

Execution adjustment (2026-10-22): New listing orchestration queries reconcile choices, then dispatch existing validated page queries through MediatR. This preserves existing response contracts and avoids changing test plumbing during Execute. `AdminProductFiltersDto.VariantGroups` is an initialized property, preserving existing constructors. Application build/scope gates pass; contracts frozen. Canonical Execute skill prohibits writing/running feature tests here; all planned unit/integration/browser test work remains for Test/E2E stages. Scope script names Infrastructure phase `infra`; use that supported key.

Implementation detail: Public fixed-only reads retain existing PostgREST paging; requests with variant restrictions use new catalogue RPC. RPC rows adapt into existing `ProductRecord` mapper to preserve pricing, placeholder/image, and read-model behavior. Web adds protected query reload/cancellation hooks for retry and mutation refresh. Cache cancellation token before disposing superseded sources so cancellation handlers never access a disposed source. Source/build portions of TASK-001 through TASK-008 are complete; combined task checkboxes remain unchecked until their deferred test portions pass. No live database migration applied during Execute.

### Step 4 — Infrastructure

**Depends on:** Stable Application contracts.

- [ ] **TASK-003** — Create `supabase/migrations/0033_variant_filters.sql` from Section 10; verify number remains free. Add `Persistence/VariantFilterSchemaTests.cs` using actual migration SQL, `RbacTestSchema`, and Testcontainers. Cover scoped facets, actual assignments, same-variant matching, deduplication, count-before-page, public-as-staff restrictions, denied admin helper/page access, deletion/activation changes, and unchanged price behavior. Prove `Colour` / ` colour ` and `Red` / `RED` merge across products, while `Colour` / `Color` remain separate. Do not copy function bodies into tests.
- [ ] **TASK-004** — Extend `CatalogueFiltersRecord`/`OptionTypeLookupRecord` and `AdminProductFiltersRecord`, add option-choice/page/row RPC records and mapper under existing Records/Mappers folders, then update `SupabaseProductRepository`. Public page RPC maps complete scalar rows into `Product.Rehydrate` with existing pricing/image/read-model rules; keep `ProductDtoMapper` authoritative. Preserve admin row mapper. Add serialization/mapping/repository contract tests; retain empty/default behavior for unrelated fields.

**Completion gate:** New migration applies over current schema; direct SQL as anon/customer/staff proves RLS/guards; RPC records match JSON; tests preserve old admin calls, price filters/sorts/counts, images, publication, and no-filter catalogue behavior. No SDK types leak inward.

### Step 5 — Web

**Depends on:** Stable Application contracts and completed Infrastructure.

- [ ] **TASK-005** — Extend `ShopFilterPanel.razor`/code-behind for raw group labels and accessible group association. Preserve heading hierarchy, keyboard controls, focus, selection counts, and resource labels for fixed groups. Add `Filter_Variant`, `VariantFilters_SelectionsRemoved`, `VariantFilters_LoadFailed`, `VariantFilters_Retry` to `Resources/Strings.resx`. Extend existing panel bUnit tests for encoded merchant labels and fixed-label regression.
- [ ] **TASK-006** — Extend public `CatalogueQueryState` and `ProductCatalogue.razor`/code-behind: dynamic repeated keys, wrapper response, effective-selection URL replacement/notice, refreshed variant groups, error/retry precedence, same sidebar/drawer state. Extend existing query-state/page bUnit tests for OR/AND, stale URLs, rapid toggles/cancellation, no matches, and metadata/result failures. Keep `Routes.Products`, existing busy keys, product cards, and sort defaults.
- [ ] **TASK-007** — Extend admin `ProductQueryState` and `ManageProducts.razor`/code-behind similarly; append dynamic groups to existing status/brand/category/price panel and pass variant filters separately from brand/category IDs. Extend existing page/state tests for inactive matching, all existing criteria, selection resets, permission denial, stale URLs, retry, and mobile/desktop parity. Keep `Routes.Admin.ManageProducts` and existing mutations.

**Completion gate:** Focused bUnit tests/build/design rules pass; static labels use typed resources, dynamic labels remain encoded content; both listings consume only frozen contracts. No new styling system or route. Use existing owned SCSS; do not add geometry unless required by actual label rendering.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-008** — Verify solution build and focused tests; extend existing catalogue/management Playwright page objects and add feature journeys with mixed active/inactive variants, later-page-only values, Red/Small + Blue/Large mismatch, stale links, and recoverable failures. Include keyboard, drawer, refresh, Back/Forward, and access-denied paths. Keep feature trait `002_variant-filters` and acceptance mapping for later Test stage. Run `graphify update .` after code changes; update Execute evidence only. Leave later workflow stages for their explicit invocations.

Useful Execute checks:

```powershell
dotnet build TheShop.slnx --nologo -m:1 -p:UseSharedCompilation=false
dotnet test tests/TheShop.Application.Tests/TheShop.Application.Tests.csproj --filter "Feature=002_variant-filters"
dotnet test tests/TheShop.Infrastructure.Tests/TheShop.Infrastructure.Tests.csproj --filter "Feature=002_variant-filters"
dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --filter "Feature=002_variant-filters"
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-design-rules.ps1 -Changed
```

Also run existing changed catalogue/management query, mapper, panel, and page regression classes by fully qualified name; their legacy feature traits do not select through new feature filter. Test stage owns complete acceptance manifest and required browser command/environment.

### Deviation procedure

Accept only scope-preserving changes consistent with existing contracts, architecture, access, and data integrity. Reject product changes, unrelated cleanup, weaker checks, or silent contract changes. For contract change: stop dependent work, amend affected contract rows/tasks here, rerun Application gate, mark Stable, then resume. Keep task IDs stable.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: Dynamic multi-select groups | TASK-001, TASK-003, TASK-004, TASK-005, TASK-006, TASK-007 |
| AC-2: All-page choices remain stable | TASK-003, TASK-006, TASK-007, TASK-008 |
| AC-3: Saved configuration refresh | TASK-003, TASK-004, TASK-008 |
| AC-4: OR within group | TASK-002, TASK-003, TASK-006, TASK-007 |
| AC-5: One variant matches every group | TASK-003, TASK-004, TASK-008 |
| AC-6: Deduplication/no-variant products | TASK-003, TASK-004, TASK-006, TASK-007 |
| AC-7: Public/admin/deleted eligibility | TASK-003, TASK-004, TASK-007, TASK-008 |
| AC-8: Public fixed criteria/prices/order/page | TASK-002, TASK-003, TASK-004, TASK-006 |
| AC-9: Admin criteria and unchanged rollups | TASK-002, TASK-003, TASK-004, TASK-007 |
| AC-10: Page and selection resets | TASK-006, TASK-007, TASK-008 |
| AC-11: Individual removal | TASK-001, TASK-006, TASK-007 |
| AC-12: No-match clearing | TASK-006, TASK-007, TASK-008 |
| AC-13: URL/refresh/Back/Forward | TASK-006, TASK-007, TASK-008 |
| AC-14: No eligible groups | TASK-003, TASK-004, TASK-006, TASK-007 |
| AC-15: Product visibility and management access | TASK-002, TASK-003, TASK-006, TASK-007, TASK-008 |
| AC-16: Invalid selection cleanup | TASK-001, TASK-002, TASK-006, TASK-007, TASK-008 |
| AC-17: Recoverable loading failure | TASK-002, TASK-006, TASK-007, TASK-008 |
| AC-18: Accessible desktop/mobile English UI | TASK-005, TASK-006, TASK-007, TASK-008 |
| AC-19: Inactive-only match excluded publicly | TASK-003, TASK-004, TASK-006, TASK-007, TASK-008 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

Extend existing page query validators rather than invoking validators manually. Retain price/page/sort rules. Accept existing fixed keys plus dynamic namespace; validate non-null filter collections/items/value collections and non-null strings. Use existing `ProductErrorKeys.CatalogueFilterInvalid` for malformed structure. Unknown dynamic names/values and empty selections are reconciliation outcomes, not validation failures. Admin authorization runs first. No request-rule duplication in Web or Domain.

### Rules and edge cases

| Spec coverage | Enforcement / observable result |
|---|---|
| RULE-1, RULE-8; inactive-only values/matches | Explicit public predicates, admin permission guard, invoker RLS; integration tests include privileged staff on public RPCs. No product/variant writes. |
| RULE-2; no eligible variants; later-page choices | Facet helper starts from actual eligible variant assignments across full dataset. Empty helper array adds no groups; choices never depend on current query. |
| RULE-3; Red/Small + Blue/Large | SQL nested `EXISTS` checks all groups against same variant. OR values use JSON string-array membership. |
| RULE-4; duplicate matches; product without variants | Product-level predicate preserves single row and filters before count/page. Empty variant object imposes no restriction. Existing price rollups remain complete. |
| RULE-5 | Existing query-state cancellation/page reset and management row-selection clearing. Test rapid changes, page changes, and latest-state adoption. |
| RULE-6; no matches | Existing clear/toggle state removes restrictions; selections remain removable in no-match UI. Failure UI takes precedence over empty state. |
| RULE-7; selected value removed/unknown | Application removes invalid dynamic selections; returns notice flag and effective state. Replace URL once; retain valid/fixed criteria; generic notice avoids hidden-value disclosure. |
| Metadata/results cannot load | Existing exception behavior returns failure; page stores error key, shows retry, suppresses no-match/count/success UI. Cancellation remains cancellation, without error toast. |

### Domain exceptions

None added. This feature reads existing state; variant creation/invariants remain untouched.

### Result.Fail error keys and interface text

| Key | English text / use |
|---|---|
| Existing `Catalogue_Filter_Invalid` | Existing malformed-filter message; referenced through `ProductErrorKeys.CatalogueFilterInvalid`. |
| Existing `RbacErrorKeys.AccessDenied` | Existing permission failure; preserve current sign-in/access-denied experience. |
| Existing `Auth_Network`, `Auth_Unexpected` | Existing exception behavior translates provider failures; no behavior/global error refactor. |
| New `Filter_Variant` | `Variant` — resource fallback; configured group label takes precedence. |
| New `VariantFilters_SelectionsRemoved` | `Some variant filters were removed because those options are no longer available.` |
| New `VariantFilters_LoadFailed` | `Products or filters could not be loaded. Please try again.` |
| New `VariantFilters_Retry` | `Retry` |

New keys are UI text/notice, not business-failure results. Add only English `Strings.resx` entries; build generates typed accessors. Merchant labels are data, never HTML/resource keys.

## 10. Database Schema & RLS Policies

New migration `0033_variant_filters.sql`. SQL below is migration body, executed transactionally by migration runner. No tables, columns, stock fields, or policies added. Existing product/child RLS from 0022/0023 remains; public functions add explicit publication/activity predicates. Management page retains guarded definer semantics from 0028.

### Schema

```sql
CREATE OR REPLACE FUNCTION public.variant_filter_options(p_admin BOOLEAN DEFAULT FALSE)
RETURNS JSONB LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = '' AS $$
DECLARE result JSONB;
BEGIN
    IF p_admin AND NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
        'key', 'variant:' || g.type_key, 'name', g.name,
        'values', g.values, 'options', g.options) ORDER BY g.type_key), '[]'::jsonb)
    INTO result
    FROM (
        SELECT d.type_key, min(d.type_label COLLATE "C") AS name,
            jsonb_agg(d.value_label ORDER BY d.value_key) AS values,
            jsonb_agg(jsonb_build_object('value', d.value_key, 'label', d.value_label)
                ORDER BY d.value_key) AS options
        FROM (
            SELECT lower(btrim(t.name)) AS type_key, lower(btrim(o.value)) AS value_key,
                min(btrim(t.name) COLLATE "C") AS type_label,
                min(btrim(o.value) COLLATE "C") AS value_label
            FROM public.products p
            JOIN public.product_variants v ON v.product_id = p.id
            JOIN public.product_variant_option_values a ON a.variant_id = v.id
            JOIN public.product_option_values o ON o.id = a.option_value_id
            JOIN public.product_option_types t ON t.id = o.option_type_id AND t.product_id = p.id
            WHERE p_admin OR (p.is_published AND v.is_available)
            GROUP BY lower(btrim(t.name)), lower(btrim(o.value))
        ) d GROUP BY d.type_key
    ) g;
    RETURN result;
END;
$$;

CREATE OR REPLACE FUNCTION public.product_matches_variant_filters(
    p_product_id UUID, p_filters JSONB, p_admin BOOLEAN DEFAULT FALSE)
RETURNS BOOLEAN LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = '' AS $$
BEGIN
    IF p_admin AND NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    IF jsonb_typeof(COALESCE(p_filters, '{}'::jsonb)) <> 'object' THEN
        RAISE EXCEPTION 'invalid variant filters' USING ERRCODE = '22023';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_each(COALESCE(p_filters, '{}'::jsonb)) f
               WHERE jsonb_typeof(f.value) <> 'array') THEN
        RAISE EXCEPTION 'invalid variant filters' USING ERRCODE = '22023';
    END IF;
    RETURN COALESCE(p_filters, '{}'::jsonb) = '{}'::jsonb OR EXISTS (
        SELECT 1 FROM public.product_variants v
        JOIN public.products p ON p.id = v.product_id
        WHERE v.product_id = p_product_id
          AND (p_admin OR (p.is_published AND v.is_available))
          AND NOT EXISTS (
              SELECT 1 FROM jsonb_each(p_filters) f
              WHERE NOT EXISTS (
                  SELECT 1 FROM public.product_variant_option_values a
                  JOIN public.product_option_values o ON o.id = a.option_value_id
                  JOIN public.product_option_types t ON t.id = o.option_type_id
                  WHERE a.variant_id = v.id AND t.product_id = p.id
                    AND f.key = 'variant:' || lower(btrim(t.name))
                    AND f.value ? lower(btrim(o.value))
              )
          )
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.get_catalogue_filters()
RETURNS JSONB LANGUAGE sql STABLE SECURITY INVOKER SET search_path = '' AS $$
    SELECT jsonb_build_object(
        'categories', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name)
            ORDER BY c.name) FROM public.categories c WHERE c.is_active), '[]'::jsonb),
        'brands', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name)
            ORDER BY b.name) FROM public.brands b WHERE b.is_active), '[]'::jsonb),
        'option_types', public.variant_filter_options(FALSE),
        'price_min', COALESCE((SELECT min(display_price) FROM public.products WHERE is_published), 0),
        'price_max', COALESCE((SELECT max(display_price) FROM public.products WHERE is_published), 0));
$$;

CREATE OR REPLACE FUNCTION public.get_admin_product_filters()
RETURNS JSONB LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = '' AS $$
BEGIN
    IF NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN jsonb_build_object(
        'brands', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name)
            ORDER BY b.name) FROM public.brands b
            WHERE EXISTS (SELECT 1 FROM public.products p WHERE p.brand_id = b.id)), '[]'::jsonb),
        'categories', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name)
            ORDER BY c.name) FROM public.categories c
            WHERE EXISTS (SELECT 1 FROM public.products p WHERE p.category_id = c.id)), '[]'::jsonb),
        'option_types', public.variant_filter_options(TRUE),
        'price_min', COALESCE((SELECT min(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM public.product_variants v
            UNION ALL SELECT COALESCE(p.sale_price, p.original_price) FROM public.products p
            WHERE NOT EXISTS (SELECT 1 FROM public.product_variants v WHERE v.product_id = p.id)
        ) prices), 0),
        'price_max', COALESCE((SELECT max(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM public.product_variants v
            UNION ALL SELECT COALESCE(p.sale_price, p.original_price) FROM public.products p
            WHERE NOT EXISTS (SELECT 1 FROM public.product_variants v WHERE v.product_id = p.id)
        ) prices), 0));
END;
$$;

CREATE OR REPLACE FUNCTION public.catalogue_products_page(
    p_brand_ids UUID[], p_category_ids UUID[], p_price_min NUMERIC, p_price_max NUMERIC,
    p_sort TEXT, p_limit INT, p_offset INT, p_variant_filters JSONB DEFAULT '{}'::jsonb)
RETURNS JSONB LANGUAGE sql STABLE SECURITY INVOKER SET search_path = '' AS $$
    WITH matched AS MATERIALIZED (
        SELECT p.*, b.name AS brand_name, c.name AS category_name,
            COALESCE((SELECT i.object_key FROM public.product_images i WHERE i.product_id = p.id
                ORDER BY i.is_primary DESC, i.position, i.id LIMIT 1), p.image_path) AS primary_image_key
        FROM public.products p
        JOIN public.brands b ON b.id = p.brand_id
        JOIN public.categories c ON c.id = p.category_id
        WHERE p.is_published
          AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
          AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
          AND (p_price_min IS NULL OR p.display_price >= p_price_min)
          AND (p_price_max IS NULL OR p.display_price <= p_price_max)
          AND public.product_matches_variant_filters(p.id, p_variant_filters, FALSE)
    ), ordered AS (
        SELECT m.*, row_number() OVER (ORDER BY
            CASE WHEN p_sort = 'price-asc' THEN m.display_price END ASC,
            CASE WHEN p_sort = 'price-desc' THEN m.display_price END DESC,
            CASE WHEN p_sort = 'name-asc' THEN m.name END ASC,
            CASE WHEN p_sort = 'name-desc' THEN m.name END DESC,
            CASE WHEN p_sort NOT IN ('price-asc','price-desc','name-asc','name-desc')
                OR p_sort IS NULL THEN m.created_at END DESC,
            m.id) AS listing_order
        FROM matched m
    ), slice AS (
        SELECT * FROM ordered ORDER BY listing_order LIMIT p_limit OFFSET p_offset
    )
    SELECT jsonb_build_object(
        'items', COALESCE((SELECT jsonb_agg(to_jsonb(s) - 'listing_order' ORDER BY s.listing_order)
            FROM slice s), '[]'::jsonb),
        'total_count', (SELECT count(*) FROM matched));
$$;

DROP FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT);
CREATE FUNCTION public.admin_products_page(
    p_search TEXT, p_status TEXT, p_brand_ids UUID[], p_category_ids UUID[],
    p_price_min NUMERIC, p_price_max NUMERIC, p_sort TEXT, p_limit INT, p_offset INT,
    p_variant_filters JSONB DEFAULT '{}'::jsonb)
RETURNS TABLE (
    id UUID, name TEXT, sku TEXT, brand_id UUID, brand_name TEXT, category_id UUID,
    category_name TEXT, currency TEXT, is_published BOOLEAN, primary_image_key TEXT,
    variant_count INT, min_price NUMERIC, max_price NUMERIC, total_count BIGINT)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = '' AS $$
DECLARE v_search TEXT := NULLIF(btrim(COALESCE(p_search, '')), '');
BEGIN
    IF NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN QUERY
    WITH variant_price AS (
        SELECT v.product_id, COALESCE(v.sale_price, v.original_price) AS price
        FROM public.product_variants v
    ), rollup AS (
        SELECT p.id AS product_id,
            (SELECT count(*)::int FROM public.product_variants v WHERE v.product_id = p.id) AS variant_count,
            COALESCE((SELECT min(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                COALESCE(p.sale_price, p.original_price)) AS min_price,
            COALESCE((SELECT max(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                COALESCE(p.sale_price, p.original_price)) AS max_price
        FROM public.products p
    ), matched AS (
        SELECT p.id, p.name, p.sku, p.brand_id, b.name AS brand_name,
            p.category_id, c.name AS category_name, p.currency, p.is_published,
            (SELECT i.object_key FROM public.product_images i
                WHERE i.product_id = p.id AND i.is_primary LIMIT 1) AS primary_image_key,
            r.variant_count, r.min_price, r.max_price, p.created_at, count(*) OVER () AS total_count
        FROM public.products p
        JOIN public.brands b ON b.id = p.brand_id
        JOIN public.categories c ON c.id = p.category_id
        JOIN rollup r ON r.product_id = p.id
        WHERE (v_search IS NULL OR p.name ILIKE '%' || v_search || '%')
          AND (p_status IS NULL OR p.is_published = (p_status = 'active'))
          AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
          AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
          AND public.product_matches_variant_filters(p.id, p_variant_filters, TRUE)
          AND ((p_price_min IS NULL AND p_price_max IS NULL)
              OR EXISTS (SELECT 1 FROM variant_price vp WHERE vp.product_id = p.id
                  AND vp.price IS NOT NULL
                  AND (p_price_min IS NULL OR vp.price >= p_price_min)
                  AND (p_price_max IS NULL OR vp.price <= p_price_max))
              OR (r.variant_count = 0 AND COALESCE(p.sale_price, p.original_price) IS NOT NULL
                  AND (p_price_min IS NULL OR COALESCE(p.sale_price, p.original_price) >= p_price_min)
                  AND (p_price_max IS NULL OR COALESCE(p.sale_price, p.original_price) <= p_price_max)))
    )
    SELECT m.id, m.name, m.sku, m.brand_id, m.brand_name, m.category_id, m.category_name,
        m.currency, m.is_published, m.primary_image_key, m.variant_count, m.min_price, m.max_price, m.total_count
    FROM matched m
    ORDER BY
        CASE WHEN p_sort = 'name-desc' THEN m.name END DESC,
        CASE WHEN p_sort = 'newest' THEN m.created_at END DESC,
        CASE WHEN p_sort = 'oldest' THEN m.created_at END ASC,
        CASE WHEN p_sort = 'price-desc' THEN m.min_price END DESC,
        CASE WHEN p_sort = 'price-asc' THEN m.min_price END ASC,
        CASE WHEN p_sort NOT IN ('name-desc','newest','oldest','price-asc','price-desc')
            THEN m.name END ASC, m.id
    LIMIT p_limit OFFSET p_offset;
END;
$$;
```

### RLS policies and execution grants

No table-policy changes. Keep current direct-read child policies for product detail/admin editing; filtering adds stricter public eligibility without altering those flows. Helpers verify admin scope even when invoked directly; definer functions check live permission before reading.

```sql
REVOKE ALL ON FUNCTION public.variant_filter_options(BOOLEAN) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.variant_filter_options(BOOLEAN) TO anon, authenticated;
REVOKE ALL ON FUNCTION public.product_matches_variant_filters(UUID, JSONB, BOOLEAN) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.product_matches_variant_filters(UUID, JSONB, BOOLEAN) TO anon, authenticated;
REVOKE ALL ON FUNCTION public.get_catalogue_filters() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.get_catalogue_filters() TO anon, authenticated;
REVOKE ALL ON FUNCTION public.get_admin_product_filters() FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_admin_product_filters() TO authenticated;
REVOKE ALL ON FUNCTION public.catalogue_products_page(UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.catalogue_products_page(UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) TO anon, authenticated;
REVOKE ALL ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) TO authenticated;
NOTIFY pgrst, 'reload schema';
```

Repository DTO serialization sends a JSON object, not a quoted JSON string, for `p_variant_filters`. Convert fixed brand/category GUID selections separately; map `ProductSortOption` to existing sort slugs within Infrastructure without importing Web. Public page response has `items` and `total_count` even when slice is empty. New `CatalogueProductPageRecord` and `CatalogueProductRowRecord` explicitly map SQL scalar names, including description, pricing, brand/category IDs/names, image key, creation timestamp, and read-model fields. Build `Product` through existing rehydration contract; do not deserialize SQL JSON into Domain.

Apply migration before deploying new client. Existing fixed-only public clients still use unchanged products endpoint; old metadata consumers ignore additive fields; old admin named calls omit defaulted final argument. SQL signature dependencies must be checked before migration. Rollback deploys prior client first, restores only original `get_catalogue_filters`/`get_admin_product_filters`/nine-argument `admin_products_page` definitions and grants from 0027/0028, then removes new public page/helpers after dependencies are restored. Do not rerun entire historical migrations or delete product data.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** Metadata and page reads may straddle another user's variant edit, briefly leaving an outdated choice visible. Accept bounded mismatch because next listing load refreshes choices and reconciles selections; result queries still enforce variant eligibility when they run. Test deactivation/removal and cancellation; no live synchronization required. TASK-002/003/006/007.
- **⚠️ Risk — ✅ Accepted:** Superseded RPC requests may finish at transport level. Accept residual network work because `.WaitAsync(ct)` and latest-state guards prevent stale results, groups, selections, notices, or URL updates. Cover cleared selections and disposed pages; cancellation produces no error notification. TASK-004/006/007.

---
**Status:** Resolved · **Spec:** `.specs/002_variant-filters/spec.md` · **Created:** 2026-10-22 · **Resolved:** 2026-10-22
