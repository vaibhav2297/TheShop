# Implementation Plan — Manage Fresh Drops

**Feature:** `004_fresh-drops`

## 1. Objective

Add one RLS-protected Fresh Drops configuration and an admin editor for it. Persist content, visibility, selection mode, and manual product order atomically; resolve current product cards from published products. This feature ends at administration; public homepage rendering is outside the spec.

## 2. Tech Stack

- **Domain:** C# entity and enum; no outer-layer dependencies.
- **Application:** MediatR commands/queries, FluentValidation, `Result<T>`, Homepage permission catalogue.
- **Infrastructure:** Supabase C# repositories and public product image URL mapping.
- **Web:** Blazor with native Shop components, `Strings.resx`, SCSS, bUnit.
- **Persistence:** PostgreSQL migration and RLS in Supabase.

## 3. High-level Architecture

`ManageFreshDrops` loads configuration and published product cards separately. The editor retains manual IDs in the unsaved draft while automatic mode is selected. Save sends one normalized draft to one command and one database transaction; saving Newest clears manual IDs.

```text
ManageFreshDrops.razor → IMediator.Send(CreateFreshDropsCommand | UpdateFreshDropsCommand)
  → validator + Homepage View and Create/Edit checks → FreshDrops domain invariants
  → IFreshDropsRepository → SupabaseFreshDropsRepository → save_fresh_drops_admin → fresh_drops (RLS)
GetFreshDropsForAdminQuery / GetFreshDropsProductsQuery
  → IFreshDropsRepository / IFreshDropsProductReader → RLS-gated admin RPCs
  → DTOs → editor, product dialog, errors and notifications
```

## 4. Data Model

### Domain entities & value objects

- `FreshDrops`: singleton ID `1`; `Title`, nullable paired `ButtonLabel`/`ButtonLink`, `SelectionMode`, ordered `ManualProductIds`, `IsVisible`. `Create` and `Update` enforce RULE-2, RULE-3, RULE-5–7 and throw `DomainException` with a stable error key. Manual IDs are empty in a saved Newest configuration; the page draft alone retains them during an unsaved mode switch.
- `FreshDropsSelectionMode`: `Newest`, `Manual`; serialize as `newest` / `manual` at the persistence boundary.

### DTOs (Application → Web)

- `FreshDropsAdminDto`: existence, title, optional button fields, mode, ordered manual IDs, visibility.
- `FreshDropsProductDto`: ID, name, brand, display price/currency, image URL, published/available state. Product cards for automatic mode contain only published products; saved manual IDs may be shown as unavailable placeholders in Edit.
- `FreshDropsProductsDto`: ordered cards plus search count/has-more data. Limit result pages to 24.

### Database tables

| Table | Purpose | Key columns |
|---|---|---|
| `fresh_drops` (new) | Single saved configuration | `id=1`, `title`, paired button fields, `selection_mode`, `manual_product_ids uuid[]`, `is_visible` |
| `products`, `brands`, `product_images` (read only) | Current published product cards | `is_published`, `created_at`, `display_price`, primary image key |

### Indexes

- `fresh_drops` primary key is sufficient for singleton reads.
- Existing `idx_products_created_at`, published/brand indexes serve the candidate query. Check the query plan during TASK-006; add a combined index only if needed.

## 5. Core Design Decisions

1. **Singleton row and action-specific writes.** A database `id=1` check and primary key enforce RULE-2 even under concurrent Add. RLS requires Homepage View with Create for INSERT or Edit for UPDATE; server checks mirror it. Rejected a collection model because the spec allows one section.
2. **Store manual IDs in order, resolve cards on read.** A `uuid[]` keeps order and retains unpublished/deleted choices for staff to inspect. Current automatic cards are queried from published products each load; manual unavailable choices are skipped from current usable cards without substitution (RULE-3/4). Rejected stored card snapshots because names, price, images, and publish state can change.
3. **Draft mode switch is reversible until Save.** Keep manual IDs in page state while mode is `newest`, so switching back before Save restores selections (AC-6). On Save in Newest mode, persist an empty manual ID list. Reopening Edit and switching to Manual therefore starts empty, as confirmed by the user. Rejected clearing IDs on mode switch because it would lose unsaved work.
4. **Published defines eligibility.** Product stock no longer exists. The Figma in-stock wording is replaced with published-product wording, matching the user's explicit decision (RULE-4).
5. **Reuse established admin UI and validation.** Model the editor on `ManageHeroBanner`, use native Shop components and shared link validation, plus explicit move buttons alongside pointer drag. The repository's native component rule supersedes the plan template's historical MudBlazor wording. The Add defaults and dynamic heading follow the user clarification over the Figma Add frame.
6. **One atomic Save and explicit outcomes.** Separate Create/Update commands use one RPC and preserve the draft after failure. `23505` is conflict, `P0002` is absent Edit, `42501` is denial. Rejected client-side multi-write saves because they can partially persist.
7. **Search behavior.** Match product or brand names without case sensitivity, show 24 results per page, and order by newest with ID as a stable tie break. This keeps large catalogues usable without changing selection limits. Rejected loading the full catalogue into the browser.

## 6. Core Functional Flow

### Flow 1: Open editor

`ManageHomepage` links to `Routes.Admin.ManageFreshDrops`. `GetFreshDropsForAdminQuery` requires Homepage View and returns present/absent separately from failure. Absent + Create builds a Visible, Newest draft with `Fresh Drops`, `View All Products`, `/products`. Existing loads saved values. Page heading renders localized `Add {Title}` or `Edit {Title}`, using `Fresh Drops` if Title is whitespace. View-only staff see data without mutation controls.

### Flow 2: Choose automatic products

Selecting `Newest` changes draft mode, retains manual IDs only in the unsaved page draft, and calls `GetFreshDropsProductsQuery` for the newest three published rows sorted by `created_at DESC, id ASC`. Show read-only cards and published-product guidance. Saving Newest clears manual IDs; reopening and switching to Manual starts empty. Reload after a product publish change refreshes candidates; automatic mode never exposes reorder controls.

### Flow 3: Choose manual products

Selecting `Manual` restores draft IDs. `ShopDialog` loads published candidates, searches product/brand, pages results, and tracks a dialog-local selection copy. Confirm commits up to three distinct IDs to the page draft; Cancel/Close discards dialog changes. The page permits remove, drag, and labeled move-up/down controls. Fetch saved IDs for Edit; an unavailable ID is labeled and is skipped from usable cards.

### Flow 4: Edit content and status

`ShopTextField` binds Title, Button label and link; `ShopChip` binds Visible/Hidden. Title updates the heading immediately. Validation shows field messages for required Title, paired button fields and safe link. Saved state changes only on Save.

### Flow 5: Save or cancel

`EditForm` submits `CreateFreshDropsCommand` or `UpdateFreshDropsCommand`. Validator checks shape; handler checks Homepage View plus action permission, normalizes manual IDs to empty for Newest, constructs domain entity, and calls repository RPC. On success show `FreshDrops_Saved` and navigate to Homepage administration. On failure preserve draft and show mapped error; disable repeat Save during request. Cancel navigates without a command.

## 7. Development Plan

### Step 1 — Domain

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add `FreshDrops` and selection-mode enum with singleton/content/selection invariants, Newest/manual-ID exclusivity, and focused domain tests.

**Completion gate:** Domain builds; invalid title, pair, mode, duplicate/fourth ID and status are covered; public API reported to Application.

### Step 2 — Application

**Depends on:** Step 1 API.

- [ ] **TASK-002** — Add `IFreshDropsRepository`, `IFreshDropsProductReader`, admin/product DTOs, and read queries with Homepage View enforcement.
- [ ] **TASK-003** — Add Create/Update commands, validators, error keys, Newest manual-ID clearing, and Homepage View plus action checks; test success, validation, denial, conflict and failed Save.

**Completion gate:** feature folders compile; handlers translate all Section 9 outcomes; downstream contracts compile.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| Create/Update commands, `Result<FreshDropsAdminDto>` | Application | Web | Stable after TASK-003 |
| Read queries and product DTOs | Application | Web | Stable after TASK-002 |
| `IFreshDropsRepository`, `IFreshDropsProductReader` | Application | Infrastructure | Stable after TASK-003 |

### Step 4 — Infrastructure

**Depends on:** stable Application contracts.

- [ ] **TASK-004** — Add `0036_manage_fresh_drops.sql` with Section 10 schema, functions, grants and RLS; apply via Supabase migration workflow and verify permission matrix.
- [ ] **TASK-005** — Add record/mapper, Supabase repositories and DI registration; map candidate image keys via existing product image storage URL service.
- [ ] **TASK-006** — Translate provider errors, preserve atomic Save and confirm published-only, order and empty-result behavior with focused infrastructure tests.

**Completion gate:** migration runs; repository matches frozen contracts; RLS denies every unauthorized read/write and function call.

### Step 5 — Web

**Depends on:** checkpoint; Infrastructure completes first in this session.

**Visual scope:** required

Figma captured 2026-10-30. Compare at actual desktop/tablet/phone widths; user-confirmed Add defaults and published wording supersede displayed Figma text.

### Visual targets

| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| add-desktop | /admin/homepage/fresh-drops | 1440x1165 | absent; Newest defaults | [Add desktop](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-53681) |
| add-tablet | /admin/homepage/fresh-drops | 834x1910 | absent; Newest defaults | [Add tablet](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-53744) |
| add-phone | /admin/homepage/fresh-drops | 390x2112 | absent; Newest defaults | [Add phone](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-53807) |
| edit-manual-desktop | /admin/homepage/fresh-drops | 1440x1254 | saved manual, 3 products | [Edit desktop](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-36411) |
| edit-manual-tablet | /admin/homepage/fresh-drops | 834x2017 | saved manual | [Edit tablet](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-41046) |
| edit-manual-phone | /admin/homepage/fresh-drops | 390x2330 | saved manual | [Edit phone](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-41783) |
| edit-auto-desktop | /admin/homepage/fresh-drops | 1440x1234 | saved automatic | [Automatic desktop](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-53870) |
| edit-auto-phone | /admin/homepage/fresh-drops | 390x2175 | saved automatic | [Automatic phone](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-53996) |
| picker-desktop | /admin/homepage/fresh-drops | 1440x900 | dialog, results | [Picker](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-56126) |
| picker-phone | /admin/homepage/fresh-drops | 390x844 | dialog, results | [Picker phone](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-56388) |
| no-results-desktop | /admin/homepage/fresh-drops | 1440x900 | dialog, no matches | [No results](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-56477) |
| no-results-phone | /admin/homepage/fresh-drops | 390x844 | dialog, no matches | [No results phone](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3183-56775) |
| skeleton-desktop | /admin/homepage/fresh-drops | 1440x1250 | loading | [Skeleton](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3194-31809) |
| skeleton-phone | /admin/homepage/fresh-drops | 390x2158 | loading | [Skeleton phone](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3194-32721) |

**Responsive decisions:** desktop two columns, tablet/phone stack products before settings; dialog uses wide grid on desktop and scrollable compact tiles on phone. Use deterministic three published fixture products and an explicit no-results search term. Browser readiness: `[data-testid="fresh-drops-editor"]` after load; `[data-testid="fresh-drops-product-dialog"]` for dialog; skeleton selector during delayed query. Export each Figma frame at 1x and crop its reference to the listed viewport for tall editor frames; crop both dialog reference and browser capture to the same dialog bounds. Capture after navigation, mode switch, dialog open, search, selection, reorder, validation and Save; inspect visual diff against Figma per visual loop.

**Figma references:** [supplied file](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56956&t=cWBtc9BODDtj4EUK-11). Nodes `3185:56956` section overview; `3169:36411` manual editor; `3183:53870` automatic editor; `3183:56126` picker; `3183:56477` empty picker; `3194:31809` loading. Figma MCP re-fetched the Add, automatic desktop, picker desktop, and manual phone frames on 2026-10-30. Map layout to `MainLayout`/breadcrumbs; mode/status chips to `ShopChip.Selected/SelectedChanged`; fields to `ShopTextField.Label` and value binding; product and action columns to `ShopTable` with page-owned row content; actions to `ShopButton` or `ShopIconButton.Label`; picker tiles to `ShopImageTile.Selected` with `Removable=false`; 960px dialog to `ShopDialog` slots with `ShopMaxWidth.Medium`; loading to `ShopSkeleton`. The picker is a separate page-owned component. These components expose the required variants; no new shared component capability is needed.

- [ ] **TASK-007** — Add route, Homepage link, breadcrumbs, busy keys, resource keys and responsive SCSS.
- [ ] **TASK-008** — Build `ManageFreshDrops.razor/.cs` and product dialog with Add/Edit/view-only, modes, dynamic heading, selection/order, loading/empty/error states and keyboard controls.
- [ ] **TASK-009** — Wire queries/commands and notifications; add focused bUnit tests for defaults, headings, within-draft mode restoration, post-Save clearing, picker limits, validation, permissions and failed Save.
- [ ] **TASK-010** — Run visual loop across table targets, fix layout/focus/overflow differences, record captures and gate evidence.

**Completion gate:** browser captures inspected against Figma with stated overrides; visual gate passes; native Shop components, no hardcoded user text/tokens; frozen contracts consumed; unauthorized state has no mutation controls.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5.

- [ ] **TASK-011** — Verify build, DI, migration/RLS matrix and end-to-end Add/Edit/Cancel/failure paths.
- [ ] **TASK-012** — Run `$theshop-test 004_fresh-drops`, `$theshop-verify 004_fresh-drops`, and document only if needed.

### Deviation procedure

Accept a deviation only when approved behavior and layer boundaries remain intact and project conventions improve. Reject requirement changes, weaker authorization, new business behavior or unrelated refactoring. For a contract change, stop dependent work, record the change and affected TASK IDs, re-freeze the checkpoint, then resume.

**Execution deviation (2026-10-30):** Native Shop controls replace historical MudBlazor references in the plan template. The live constitution requires the native dependency boundary; no Application contract or approved behavior changes.

**Execution update (2026-10-30):** The product rows use `ShopTable` for the Figma Product/Actions grid, and `Components/Admin/FreshDropsProductDialog` owns dialog-local search and selection. This follows the user's component request without changing saved data or Application contracts.

## 8. Acceptance Criteria → Task Mapping

| AC | Tasks |
|---|---|
| AC-1 Add defaults and heading | TASK-002, TASK-007, TASK-008, TASK-009 |
| AC-2 Edit loaded values and heading | TASK-002, TASK-005, TASK-008, TASK-009 |
| AC-3 guest and denied | TASK-003, TASK-004, TASK-008, TASK-011 |
| AC-4 view-only and combined permission | TASK-003, TASK-004, TASK-008, TASK-011 |
| AC-5 automatic newest published | TASK-002, TASK-005, TASK-006, TASK-009 |
| AC-6 dialog and within-draft mode restoration | TASK-001, TASK-008, TASK-009 |
| AC-7 selection limit/order/remove | TASK-001, TASK-004, TASK-008, TASK-009 |
| AC-8 no results | TASK-002, TASK-008, TASK-010 |
| AC-9 title and optional button pair | TASK-001, TASK-003, TASK-004, TASK-009 |
| AC-10 safe button link | TASK-001, TASK-003, TASK-009 |
| AC-11 persistence and failed Save | TASK-004, TASK-005, TASK-006, TASK-009, TASK-011 |
| AC-12 Cancel | TASK-008, TASK-009 |
| AC-13 singleton conflict | TASK-001, TASK-004, TASK-006, TASK-009 |
| AC-14 load/save/permission failures | TASK-003, TASK-006, TASK-008, TASK-009, TASK-011 |
| AC-15 responsive and accessible | TASK-007, TASK-008, TASK-009, TASK-010 |

## 9. Validation & Error Handling Strategy

### Validators and outcomes

| Rule/edge | Enforced by | Result and UI |
|---|---|---|
| RULE-1; guest, View-only, permission loss | Application authorization, RPC checks and RLS | `FreshDrops_AccessDenied`; no management content or Save |
| RULE-2; second Add/Hidden row | singleton PK + create conflict mapping | `FreshDrops_AlreadyExists`; retain draft, offer reload |
| RULE-3; fourth/duplicate, order | domain, command validator, SQL array check, dialog limit | `FreshDrops_SelectionInvalid`; preserve current draft order; saving Newest clears saved IDs |
| RULE-4; fewer than three, unpublished or removed | published-only product query; manual placeholder | show available count; `FreshDrops_UnavailableProduct` for saved ID |
| RULE-5; blank Title, partial button | validator, domain, table constraints | `FreshDrops_TitleRequired` / `FreshDrops_ButtonPairRequired`; retain draft |
| RULE-6; unsafe link | shared safe-link validation, validator, domain | `FreshDrops_ButtonLinkInvalid`; retain draft |
| RULE-7; invalid mode/status, failed Save | validator/domain/table, atomic RPC | `FreshDrops_SelectionInvalid` / `FreshDrops_SaveFailed`; old row intact |
| Search no results/failure | product reader and dialog | `FreshDrops_NoResults` / `FreshDrops_SearchFailed`; retain dialog selection |
| Load failure vs absent | query result/error distinction | `FreshDrops_LoadFailed` with retry; never show Add on error |
| Edit absent/repeated Save | RPC `P0002`; busy guard | `FreshDrops_NotFound` / one pending request |

### `Strings.resx` entries

| Key | English text |
|---|---|
| `Homepage_FreshDrops` | Fresh Drops |
| `FreshDrops_AddHeading` / `FreshDrops_EditHeading` | Add {0} / Edit {0} |
| `FreshDrops_Title` / `FreshDrops_ButtonLabel` / `FreshDrops_ButtonLink` | Title / Button label / Button link |
| `FreshDrops_ChooseProducts` / `FreshDrops_NewestAutomatically` / `FreshDrops_PickManually` | Choose products / Newest automatically / Pick manually |
| `FreshDrops_AddProduct` / `FreshDrops_AddProducts` / `FreshDrops_SearchProducts` | Add product / Add products / Search by product or brand |
| `FreshDrops_SelectedCount` / `FreshDrops_AutomaticCount` | {0} of 3 selected / {0} published products shown |
| `FreshDrops_AutomaticHint` / `FreshDrops_ManualHint` | The three newest published products update automatically. / Unpublished selections are skipped. |
| `FreshDrops_NoProducts` / `FreshDrops_NoResults` / `FreshDrops_NoResultsHint` | No products available / No products found / Try another product or brand name. |
| `FreshDrops_Status` / `FreshDrops_Visible` / `FreshDrops_Hidden` | Status / Visible / Hidden |
| `FreshDrops_MoveUp` / `FreshDrops_MoveDown` / `FreshDrops_Remove` | Move up / Move down / Remove product |
| `FreshDrops_UnavailableProduct` / `FreshDrops_Reload` | Product unavailable / Reload Fresh Drops |
| `FreshDrops_Saved` / `FreshDrops_TitleRequired` | Fresh Drops saved. / Enter a title. |
| `FreshDrops_ButtonPairRequired` / `FreshDrops_ButtonLinkInvalid` | Enter both button label and link, or leave both blank. / Enter a site path or HTTP/HTTPS URL. |
| `FreshDrops_SelectionInvalid` / `FreshDrops_AlreadyExists` / `FreshDrops_NotFound` | Choose up to three different products. / Fresh Drops already exists. Reload to edit it. / Fresh Drops no longer exists. Reload to add it. |
| `FreshDrops_AccessDenied` / `FreshDrops_LoadFailed` / `FreshDrops_SaveFailed` / `FreshDrops_SearchFailed` | You cannot manage Fresh Drops. / Fresh Drops could not load. Try again. / Fresh Drops could not save. Try again. / Products could not load. Try again. |

Reuse existing `Save`, `Cancel`, `Loading` keys. Resource formatting uses project localization conventions; no literal user-facing copy in Razor.

## 10. Database Schema & RLS Policies

Migration `0036_manage_fresh_drops.sql` applies the following SQL. Safe URL shape is checked in Application/domain; the table protects required/pair shape and selection integrity. Existing product RLS limits reads to published products.

```sql
BEGIN;

CREATE FUNCTION public.fresh_drops_manual_ids_valid(p_ids uuid[])
RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
  SELECT p_ids IS NOT NULL AND cardinality(p_ids) <= 3
    AND array_position(p_ids, NULL) IS NULL
    AND (SELECT count(DISTINCT x) = cardinality(p_ids) FROM unnest(p_ids) AS x);
$$;

CREATE TABLE public.fresh_drops (
  id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  title text NOT NULL CHECK (length(btrim(title)) > 0),
  button_label text,
  button_link text,
  selection_mode text NOT NULL CHECK (selection_mode IN ('newest', 'manual')),
  manual_product_ids uuid[] NOT NULL DEFAULT '{}'::uuid[]
    CHECK (public.fresh_drops_manual_ids_valid(manual_product_ids)),
  CONSTRAINT fresh_drops_newest_has_no_manual_ids CHECK (
    selection_mode <> 'newest' OR cardinality(manual_product_ids) = 0),
  is_visible boolean NOT NULL DEFAULT true,
  CONSTRAINT fresh_drops_button_pair CHECK (
    (button_label IS NULL AND button_link IS NULL) OR
    (button_label IS NOT NULL AND length(btrim(button_label)) > 0 AND
     button_link IS NOT NULL AND length(btrim(button_link)) > 0)
  )
);

REVOKE ALL ON public.fresh_drops FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.fresh_drops TO authenticated;
ALTER TABLE public.fresh_drops ENABLE ROW LEVEL SECURITY;
CREATE POLICY fresh_drops_read ON public.fresh_drops FOR SELECT TO authenticated
  USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY fresh_drops_create ON public.fresh_drops FOR INSERT TO authenticated
  WITH CHECK ((SELECT public.authorize('homepage.view')) AND
              (SELECT public.authorize('homepage.create')));
CREATE POLICY fresh_drops_edit ON public.fresh_drops FOR UPDATE TO authenticated
  USING ((SELECT public.authorize('homepage.view')) AND
         (SELECT public.authorize('homepage.edit')))
  WITH CHECK ((SELECT public.authorize('homepage.view')) AND
              (SELECT public.authorize('homepage.edit')));

CREATE FUNCTION public.get_fresh_drops_admin()
RETURNS SETOF public.fresh_drops LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT f.* FROM public.fresh_drops f WHERE f.id = 1;
END; $$;

CREATE FUNCTION public.save_fresh_drops_admin(
  p_create boolean, p_title text, p_button_label text, p_button_link text,
  p_selection_mode text, p_manual_product_ids uuid[], p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
     NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.fresh_drops
      (id, title, button_label, button_link, selection_mode, manual_product_ids, is_visible)
    VALUES (1, p_title, p_button_label, p_button_link, p_selection_mode,
            p_manual_product_ids, p_is_visible);
  ELSE
    UPDATE public.fresh_drops SET title = p_title, button_label = p_button_label,
      button_link = p_button_link, selection_mode = p_selection_mode,
      manual_product_ids = p_manual_product_ids, is_visible = p_is_visible
    WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN RAISE EXCEPTION 'fresh drops not found' USING ERRCODE = 'P0002'; END IF;
  END IF;
END; $$;

CREATE FUNCTION public.fresh_drops_product_cards(
  p_ids uuid[] DEFAULT NULL, p_search text DEFAULT NULL,
  p_limit integer DEFAULT 24, p_offset integer DEFAULT 0)
RETURNS TABLE (id uuid, name text, brand_name text, display_price numeric,
               currency text, image_key text, created_at timestamptz)
LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_limit IS NULL OR p_limit < 1 OR p_limit > 48 OR
     p_offset IS NULL OR p_offset < 0 THEN
    RAISE EXCEPTION 'invalid page' USING ERRCODE = '22023';
  END IF;
  RETURN QUERY
    SELECT p.id, p.name, b.name, p.display_price, p.currency,
           img.object_key, p.created_at
    FROM public.products p
    JOIN public.brands b ON b.id = p.brand_id
    LEFT JOIN LATERAL (
      SELECT pi.object_key FROM public.product_images pi
      WHERE pi.product_id = p.id
      ORDER BY pi.is_primary DESC, pi.position ASC, pi.id ASC LIMIT 1
    ) img ON true
    WHERE p.is_published = true
      AND (p_ids IS NULL OR p.id = ANY(p_ids))
      AND (NULLIF(btrim(p_search), '') IS NULL OR
           p.name ILIKE '%' || btrim(p_search) || '%' OR
           b.name ILIKE '%' || btrim(p_search) || '%')
    ORDER BY p.created_at DESC, p.id ASC
    LIMIT p_limit OFFSET p_offset;
END; $$;

REVOKE ALL ON FUNCTION public.fresh_drops_manual_ids_valid(uuid[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_fresh_drops_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_fresh_drops_admin(boolean,text,text,text,text,uuid[],boolean) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.fresh_drops_product_cards(uuid[],text,integer,integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.fresh_drops_manual_ids_valid(uuid[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_fresh_drops_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_fresh_drops_admin(boolean,text,text,text,text,uuid[],boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.fresh_drops_product_cards(uuid[],text,integer,integer) TO authenticated;

COMMIT;
```

Product query returns published cards in newest order; the Application layer reorders an ID-filtered result to saved manual order. Missing IDs become placeholders. Product search uses a capped page and continuation; the UI may derive has-more by asking for 25 rows while displaying 24. Application sends an empty manual ID array for Newest, matching the table constraint.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** Product publication may change after selection. Saved manual IDs remain inspectable; unavailable choices are labeled and skipped (TASK-006/TASK-009).
- **⚠️ Risk — ✅ Accepted:** A concurrent Add or interrupted Save can leave client state uncertain. Conflict mapping and reload reconcile the row before success is claimed (TASK-006/TASK-009).
- **⚠️ Risk — ✅ Accepted:** Figma Add defaults and in-stock copy differ from confirmed requirements. Visual review uses the agreed Newest defaults and published-product wording (TASK-010).

---
**Status:** Resolved · **Spec:** `.specs/004_fresh-drops/spec.md` · **Created:** 2026-10-30 · **Resolved:** 2026-10-30
