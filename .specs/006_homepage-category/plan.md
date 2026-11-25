# Implementation Plan — Homepage Category Section

**Feature:** `006_homepage-category`

## 1. Objective

Add a single persisted homepage category configuration with three ordered category IDs, editable section fields, and Visible/Hidden status. Staff manage it through a permission-gated Add/Edit form. The Add form starts with prefilled section fields and no selected categories. Remove the public homepage rendering and read path introduced by the earlier implementation; shopper-facing display belongs to a separate feature.

## 2. Tech Stack

- **Domain:** C# `HomepageCategorySection` for singleton and selection invariants.
- **Application:** MediatR commands/queries, FluentValidation, `Result` and immutable DTOs in `Features/Homepage`.
- **Infrastructure:** Existing Supabase client, `CategoryRecord`, and public category-image storage URLs.
- **Web:** Blazor WebAssembly, native `Shop*` controls, `Strings.resx`, project SCSS, bUnit.
- **Persistence:** PostgreSQL singleton table, one atomic save RPC, RLS, and schema tests.

## 3. High-level Architecture

The admin page dispatches a read query to choose Add or Edit, loads active category choices, and sends the corresponding save command. The validator handles input shape, the handler creates or updates the domain configuration and verifies selected category availability, and Infrastructure saves one row under Homepage permissions. Edit restores the saved order and status. No public page reads this configuration in this feature.

```text
ManageHomepageCategory.razor → IMediator.Send(CreateHomepageCategoryCommand / UpdateHomepageCategoryCommand)
  → validators → handlers → HomepageCategorySection.Create/Update
  → IHomepageCategoryRepository.SaveAsync → SupabaseHomepageCategoryRepository
  → save_homepage_category_admin RPC → homepage_category_section (RLS + checks)

ManageHomepageCategory.razor → IMediator.Send(GetHomepageCategoryForAdminQuery)
  → IHomepageCategoryRepository.GetForAdminAsync + ICategoryRepository.GetByIdAsync
  → HomepageCategoryDto → saved fields, ordered category rows, visibility
```

## 4. Data Model

### Domain entities & value objects

- **`HomepageCategorySection`** — singleton `Id = 1`; `Title`, `ButtonLabel`, `ButtonLink`, ordered read-only `CategoryIds`, and `IsVisible`. `Create` and `Update` enforce three distinct, nonempty IDs, a nonblank title, both nonblank button fields, and a safe destination; `SetVisibility` changes status. Domain failures carry keys through `DomainException`.
- Existing `Category` remains the source of current name, image path, and active status. The section stores IDs, not category copies.

### DTOs (Application → Web)

- **`HomepageCategoryDto`** — saved fields, ordered category IDs, selected `CategoryDto` details where those records still exist, and visibility; `null` means no configuration. IDs remain present if a category was deleted, so Edit can show an unavailable row.
- **`GetHomepageCategoryChoicesQuery`** returns active `CategoryDto` choices for the picker, including image URLs. Reuse `CategoryDtoMapper`; a missing uploaded image stays `null` for `ShopImage`'s accessible placeholder.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `public.homepage_category_section` (new) | One complete, ordered configuration | `id = 1`, `title`, `button_label`, `button_link`, `category_ids uuid[]`, `is_visible` |
| `public.categories` (unchanged) | Category names, activity, and image paths | `id`, `name`, `image_path`, `is_active` |

### Indexes

- The singleton primary key and existing `categories(id)` primary key cover all reads and writes; no new secondary index is needed.

## 5. Core Design Decisions

1. **Decision:** Use a dedicated `HomepageCategorySection` and `IHomepageCategoryRepository` under the existing Homepage feature, following the Fresh Drops singleton pattern.
   - **Why:** The fixed three-category rule differs from Featured tiles and Fresh Drops product selection; a complete row save preserves order and visibility together (RULE-1, RULE-4, RULE-6; constitution 6, 9).
   - **Rejected:** Reusing `FreshDrops` or `FeaturedSection` would mix different business rules and presentation data.
2. **Decision:** Persist three ordered category IDs in one singleton row and validate their shape in Domain, Application, and the database. The save RPC checks create versus edit atomically.
   - **Why:** Order is part of the saved configuration, and a database backstop prevents direct writes or races from creating a second or incomplete section (RULE-1, RULE-6; constitution 4, 6, 31).
   - **Rejected:** A join table adds ordering and transaction complexity for a fixed three-item section.
3. **Decision:** Extend `ICategoryRepository` with an active category read for the picker; resolve saved IDs through its existing `GetByIdAsync` and `CategoryDtoMapper`. Save checks that selected IDs are active, with a database trigger closing the check/save race.
   - **Why:** Existing category records and image URL mapping remain authoritative. The admin query restores saved order and marks a subsequently deleted category unavailable so it can be replaced (Section 11 risk).
   - **Rejected:** Storing category names or Figma images in the section would become stale after category edits.
4. **Decision:** Prefill and persist the button destination as `Routes.Categories` (`/categories`) without rendering the button or adding category-card routes.
   - **Why:** The user confirmed the `/categories` page and public homepage display are separate work. This feature stores the editable destination and restores it in Edit (AC-1, AC-8; constitution 21).
   - **Rejected:** Changing the Figma default to `/products` or adding a public directory page would change the confirmed scope.
5. **Decision:** Reuse `ShopTable`, `ShopTextField`, `ShopChip`, `ShopButton`, `ShopIconButton`, `ShopImage`, `ShopDialog`, `ShopImageTile`, `ShopSkeleton`, `BusyFor`, and `ShopCommandValidation` for the admin editor. The title heading reads the on-input bound model and falls back only in the heading.
   - **Why:** These controls support the inspected Figma variants, keyboard actions, loading, labels, and command errors (RULE-2; constitution 11, 14, 17, 22, 33, 35).
   - **Rejected:** Page-owned copies of shared controls or placeholder-only inputs would break the established contracts.

## 6. Core Functional Flow

### Flow 1: Start or resume editing

1. `ManageHomepageCategory.razor.cs` sends `GetHomepageCategoryForAdminQuery` (`homepage.view`) and `GetHomepageCategoryChoicesQuery` through `BusyState.RunAsync(BusyKeys.Homepage.LoadCategorySection, ...)`. The admin query resolves saved IDs through `ICategoryRepository.GetByIdAsync`, including inactive records.
2. A `null` saved DTO selects Add with `HomepageCategoryFormModel` defaults: `Strings.HomepageCategory_DefaultTitle`, `Strings.HomepageCategory_ViewAllCategories`, `Routes.Categories`, Visible, and `CategoryIds = []`. A saved DTO selects Edit and copies its values.
3. Load failure shows a resource-backed retry state; view-only staff see the saved section but no mutation controls.

### Flow 2: Change the heading and section details

1. `ShopTextField` updates the title on input; the code-behind formats `Strings.HomepageCategory_AddHeading` or `EditHeading` with the trimmed title or default title when blank.
2. Other fields and Visible/Hidden chips edit only the local form model. `EditForm` uses `OnSubmit`, `novalidate`, and `ShopCommandValidation`; Application validators own Save rules.

### Flow 3: Curate categories

1. Add Category opens a `ShopDialog` with active choices rendered using `ShopImageTile`; chosen IDs return to the page in selection order.
2. `ShopTable` renders current names/images, using `ShopImage`'s accessible placeholder for a category without uploaded artwork. `ShopIconButton` arrows move one row, Remove deletes a draft row, and native drag/drop may mirror the Figma handle; arrows remain the keyboard path. The count and Add disabled state derive from the draft list.
3. Selected IDs stay local until Save; the picker excludes duplicates and cannot exceed three.

### Flow 4: Save or cancel

1. The page clears prior command errors and runs `CreateHomepageCategoryCommand` or `UpdateHomepageCategoryCommand` through `BusyKeys.Homepage.SaveCategorySection`.
2. `ValidationBehavior` returns all field failures. The handler verifies selected categories still exist and are active, invokes `HomepageCategorySection`, then calls `IHomepageCategoryRepository.SaveAsync(section, create, ct)`.
3. The authorized RPC inserts or updates the whole row; database checks and a category-active trigger defend against direct or concurrent invalid saves. Expected conflict/not-found/unavailable outcomes return resource keys. Success shows a notification and returns to `Routes.Admin.ManageHomepage`.
4. Cancel is a `ShopButton` navigation link to `Routes.Admin.ManageHomepage` and leaves the saved row unchanged.
5. Edit reloads the saved order and Visible/Hidden value. A deleted selected category appears as unavailable in the admin table until replaced; no shopper-facing read or render occurs.

## 7. Development Plan

### Step 1 — Domain

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add `Domain/Entities/HomepageCategorySection.cs` with complete-update invariants and matching Domain tests for count, duplicates, button pair, visibility, and title.

**Completion gate:** Domain builds; invariants have tests; no outer-layer types enter Domain; report the public API to Application.

### Step 2 — Application

**Depends on:** Step 1's Domain API.

- [ ] **TASK-002** — Keep `IHomepageCategoryRepository` admin-read/save methods and `HomepageCategoryDto`; remove `GetVisibleAsync` and `HomepageCategoryForStorefrontDto` from the earlier implementation. Keep `ICategoryRepository.GetActiveForHomepageAsync` for the picker.
- [ ] **TASK-003** — Add create/update commands, permission attributes, validators, handlers, and `HomepageCategoryErrorKeys`; normalize input once at the Application boundary.
- [ ] **TASK-004** — Keep admin and active-choice queries/handlers, mapping existing `Category` via `CategoryDtoMapper` in saved order; remove `GetHomepageCategoryForStorefrontQuery` and handler.
- [ ] **TASK-005** — Add Application tests for authorization, exact-three validation, category availability, Add/Edit conflict, saved Hidden status, and ordered admin projection.

**Completion gate:** Application builds; all requests and error outcomes in Section 9 are covered; contracts consumed by Infrastructure/Web compile.

### Step 3 — Application contract checkpoint

**Depends on:** Step 2 complete. Freeze these before Infrastructure or Web edits.

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `CreateHomepageCategoryCommand` / `UpdateHomepageCategoryCommand` → `Result` | Application | Web | Stable after TASK-003 |
| `GetHomepageCategoryForAdminQuery`, `GetHomepageCategoryChoicesQuery`, and admin DTOs | Application | Web | Stable after TASK-004 |
| `IHomepageCategoryRepository`, `ICategoryRepository.GetActiveForHomepageAsync` | Application | Infrastructure | Stable after TASK-004 |

### Step 4 — Infrastructure

**Depends on:** stable Application contracts.

- [ ] **TASK-006** — Revise the unshipped `supabase/migrations/0038_manage_homepage_categories.sql` to Section 10's admin-only grants and RLS while retaining the singleton table, active-selection trigger, and RPCs.
- [ ] **TASK-007** — Keep `HomepageCategoryRecord`, mapper, admin-read/save methods in `SupabaseHomepageCategoryRepository`, the active-category read in `SupabaseCategoryRepository`, and DI registration; remove the public `GetVisibleAsync` read.
- [ ] **TASK-008** — Add repository and PostgreSQL schema tests covering round-trip order, create/edit races, direct-write checks, anonymous read denial, and the Homepage permission matrix.

**Completion gate:** Migration applies cleanly; RLS matches Section 10; repository satisfies frozen interfaces; Infrastructure tests pass.

### Step 5 — Web

**Depends on:** stable Application contracts and completed Infrastructure work.

**Figma references** (inspected from the supplied [file](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56958&t=ZaH338FUob2pht8E-11)):

- `3169:37239`, `3169:43962`, `3169:44664` — Edit desktop/tablet/phone; `3218:64306`, `3218:64716`, `3218:65100` — Add desktop/tablet/phone; `3194:34419` — loading skeleton. Add's table stays empty per the corrected spec; section fields use the values shown in Edit. The public section node `3185:56958` is reference context only.
- Figma Button `170:2647` Outlined/Primary/Medium → `ShopButton` with Add icon; Save `170:2625` Filled/Primary/Large and Cancel `170:2656` Outlined/Primary/Large → existing `ShopButton` variants and link semantics.
- Figma Text Field `2732:18856` Normal → `ShopTextField` for the three labelled, prefilled fields. Figma Chip `2036:10249` Selected and `180:518` Default → `ShopChip` for Visible/Hidden.
- Figma manual table row `3204:47324` → `ShopTable`; image `2944:14402` SquareContain → `ShopImage`; icon buttons `322:6451` Text/Primary/Medium and `2947:16985` Text/Error/Medium → `ShopIconButton` with `ShopIcons` arrows/trash. The Add Category picker is not pictured in these frames; compose existing `ShopDialog` and `ShopImageTile` without a new shared primitive.
- Category artwork comes from the saved category image URL, not exported Figma assets; image-free categories use `ShopImage`'s accessible placeholder and remain selectable. Desktop uses the two-column form; tablet/phone stack the category table above settings and retain the same control order. The `ShopTable` empty state and `ShopSkeleton` use existing components. Missing Figma focus, busy, error, and picker states follow native project behavior.

- [ ] **TASK-009** — Add `HomepageCategoryFormModel`, `ManageHomepageCategory.razor/.razor.cs`, and a single-use category picker using the mapped controls; include Add/Edit/empty/loading/error/validation/unauthorized states and accessible ordering.
- [ ] **TASK-010** — Keep `Routes.Admin.ManageHomepageCategory`, admin Homepage busy keys, `Strings.resx` keys from Section 9, a Manage Homepage entry, and page-owned `Styles/layouts/_homepage-category.scss`; remove `Routes.CategoryProducts(Guid)` and `BusyKeys.Homepage.LoadCategoryStorefront`.
- [ ] **TASK-011** — Remove the earlier public section from `Home.razor/.razor.cs` and its storefront-only SCSS while preserving the pre-feature Home page.
- [ ] **TASK-012** — Add bUnit tests for prefilled fields with an empty Add table, live heading fallback, picker/reordering, command errors, permissions, Cancel, and restored Visible/Hidden values in Edit.

**Completion gate:** Web build and design checks pass; components and SCSS match mapped variants; no hardcoded user text/routes; keyboard and responsive states work; view-only and unauthorized users cannot save.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-013** — Run solution/build, targeted Domain/Application/Infrastructure/Web tests, and browser checks at 1440, 834, 390, and 320 px for Add, Edit, Save, Hidden, focus, and admin navigation. Confirm no homepage category section renders publicly.
- [ ] **TASK-014** — Run `$theshop-test 006_homepage-category`, then `$theshop-verify 006_homepage-category`; document only if needed by the optional stage.

### Deviation procedure

- Accept only a change that preserves the confirmed spec and constitution, improves fit with existing conventions, and does not weaken authorization or integrity.
- Reject changes that add category administration, a `/categories` directory page, public homepage rendering, or unrelated homepage redesign.
- If a stable Application contract must change, stop dependent work, record the change and affected TASK IDs here, re-freeze the contract, then resume.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: prefilled fields, empty table | TASK-009, TASK-010, TASK-012 |
| AC-2: live Add/Edit heading | TASK-009, TASK-012 |
| AC-3: blank-title heading fallback | TASK-009, TASK-010, TASK-012 |
| AC-4: fourth category unavailable | TASK-009, TASK-012 |
| AC-5: fewer than three rejected | TASK-001, TASK-003, TASK-005, TASK-009, TASK-012 |
| AC-6: Edit restores three ordered category rows with images and names | TASK-001, TASK-002, TASK-004, TASK-006, TASK-007, TASK-008, TASK-009, TASK-012, TASK-013 |
| AC-7: button fields required | TASK-001, TASK-003, TASK-005, TASK-009, TASK-012 |
| AC-8: saved section text and destination restored in Edit | TASK-004, TASK-006, TASK-007, TASK-009, TASK-012 |
| AC-9: Hidden status restored and editable | TASK-004, TASK-006, TASK-008, TASK-009, TASK-012 |
| AC-10: Cancel preserves saved values | TASK-009, TASK-012 |
| AC-11: singleton Add conflict | TASK-001, TASK-003, TASK-006, TASK-008, TASK-009, TASK-012 |
| AC-12: keyboard/responsive controls | TASK-009, TASK-010, TASK-012, TASK-013 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `CreateHomepageCategoryCommandValidator` and `UpdateHomepageCategoryCommandValidator`: nonblank title, button label, and button link; `HeroBannerInput.IsDestination` for safe relative or HTTP/HTTPS destinations; exactly three distinct nonempty IDs (RULE-1, RULE-3). The page does not duplicate these input rules.
- The Domain entity repeats the persistent invariants (RULE-1, RULE-3); handlers check selected IDs are currently active. The database enforces singleton, shape, nonblank fields, safe link scheme, and active selection. `ValidationBehavior` returns all field failures to `ShopCommandValidation`.
- RULE-2 is the Web heading expression; blank or whitespace title shows the default immediately, even though Save needs a title. RULE-4 is the ordered array plus admin DTO projection. RULE-5 is persisted Visible/Hidden status restored in Edit. RULE-6 is the database singleton key plus create-only versus update-only RPC.
- Edge outcomes: underfilled selection stays on the form with field guidance; fourth choice is disabled until removal; blank button fields show field errors; Cancel dispatches no command; Hidden saves and reopens as Hidden; a deleted category is shown as unavailable in Edit and returns `HomepageCategory_CategoryUnavailable` if submitted unchanged.
- `SupabaseHomepageCategoryRepository` maps SQLSTATE `23505` to `HomepageCategory_AlreadyExists`, `P0002` to `HomepageCategory_NotFound`, `P0001` to `HomepageCategory_CategoryUnavailable`, and `42501` to the existing access-denied key. Unknown database failures map to `HomepageCategory_SaveFailed`; cancellation propagates.

### Domain exceptions

- `DomainException` from `HomepageCategorySection` uses `HomepageCategory_TitleRequired`, `HomepageCategory_ButtonLabelRequired`, `HomepageCategory_ButtonLinkRequired`, `HomepageCategory_ButtonLinkInvalid`, or `HomepageCategory_SelectionInvalid`; handlers return the key through `Result.Fail`.

### Result.Fail error keys (new `Strings.resx` entries)

| Key | English text |
|---|---|
| `HomepageCategory_TitleRequired` | Enter a section title. |
| `HomepageCategory_ButtonLabelRequired` | Enter a button label. |
| `HomepageCategory_ButtonLinkRequired` | Enter a button link. |
| `HomepageCategory_ButtonLinkInvalid` | Enter a site path or HTTP/HTTPS URL. |
| `HomepageCategory_SelectionInvalid` | Choose exactly three different categories. |
| `HomepageCategory_CategoryUnavailable` | A selected category is unavailable. Choose another category. |
| `HomepageCategory_AlreadyExists` | This section already exists. Reload to edit it. |
| `HomepageCategory_NotFound` | This section no longer exists. Reload to add it. |
| `HomepageCategory_LoadFailed` | The category section could not load. Try again. |
| `HomepageCategory_SaveFailed` | The category section could not save. Try again. |
| `HomepageCategory_ChoicesLoadFailed` | Categories could not load. Try again. |

Additional typed UI keys: `HomepageCategory_DefaultTitle` = `Discover by Category`, `HomepageCategory_ViewAllCategories` = `View All Categories`, Add/Edit heading format strings, Categories/Section/Title/Button label/Button link/Status/Visible/Hidden, selected-count text, Add Category, empty-table title/hint, move up/down/remove, unavailable category, reload, picker title, and saved notification. Reuse shared `Save`, `Cancel`, `Loading`, and icon registry entries. Application follows the existing Homepage `*ErrorKeys` contract; tests verify every key exists in `Strings.resx`.

## 10. Database Schema & RLS Policies

### Schema

Revise the unshipped `0038_manage_homepage_categories.sql` migration. The complete singleton row is written in one transaction. A category can later be deactivated or deleted; the admin projection retains saved IDs and identifies missing records for repair. Only authorized staff can read the section.

```sql
BEGIN;

CREATE FUNCTION public.homepage_category_ids_valid(p_ids uuid[])
RETURNS boolean LANGUAGE sql IMMUTABLE SET search_path = '' AS $$
  SELECT p_ids IS NOT NULL
    AND cardinality(p_ids) = 3
    AND array_position(p_ids, NULL) IS NULL
    AND NOT ('00000000-0000-0000-0000-000000000000'::uuid = ANY(p_ids))
    AND (SELECT count(DISTINCT selected_id) = 3
         FROM unnest(p_ids) AS selected(selected_id));
$$;

CREATE TABLE public.homepage_category_section (
  id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  title text NOT NULL CHECK (length(btrim(title)) > 0),
  button_label text NOT NULL CHECK (length(btrim(button_label)) > 0),
  button_link text NOT NULL CHECK (
    length(btrim(button_link)) > 0
    AND button_link !~ '[[:space:]]'
    AND position(chr(92) in button_link) = 0
    AND ((left(button_link, 1) = '/' AND left(button_link, 2) <> '//')
      OR left(lower(button_link), 7) = 'http://'
      OR left(lower(button_link), 8) = 'https://')),
  category_ids uuid[] NOT NULL CHECK (public.homepage_category_ids_valid(category_ids)),
  is_visible boolean NOT NULL DEFAULT true
);

CREATE FUNCTION public.homepage_category_selection_active()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF (SELECT count(*) FROM (
      SELECT c.id FROM public.categories c
      WHERE c.id = ANY(NEW.category_ids) AND c.is_active
      FOR SHARE
  ) selected) <> 3 THEN
    RAISE EXCEPTION 'selected category unavailable' USING ERRCODE = 'P0001';
  END IF;
  RETURN NEW;
END; $$;

CREATE TRIGGER homepage_category_selection_active
BEFORE INSERT OR UPDATE OF category_ids ON public.homepage_category_section
FOR EACH ROW EXECUTE FUNCTION public.homepage_category_selection_active();

REVOKE ALL ON public.homepage_category_section FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.homepage_category_section TO authenticated;

CREATE FUNCTION public.get_homepage_category_admin()
RETURNS SETOF public.homepage_category_section LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT s.* FROM public.homepage_category_section s WHERE s.id = 1;
END; $$;

CREATE FUNCTION public.save_homepage_category_admin(
  p_create boolean, p_title text, p_button_label text, p_button_link text,
  p_category_ids uuid[], p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
     NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.homepage_category_section
      (id, title, button_label, button_link, category_ids, is_visible)
    VALUES (1, p_title, p_button_label, p_button_link, p_category_ids, p_is_visible);
  ELSE
    UPDATE public.homepage_category_section SET title = p_title,
      button_label = p_button_label, button_link = p_button_link,
      category_ids = p_category_ids, is_visible = p_is_visible
    WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN RAISE EXCEPTION 'section not found' USING ERRCODE = 'P0002'; END IF;
  END IF;
END; $$;

REVOKE ALL ON FUNCTION public.homepage_category_ids_valid(uuid[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.homepage_category_selection_active() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_homepage_category_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_homepage_category_admin(
  boolean,text,text,text,uuid[],boolean) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.homepage_category_ids_valid(uuid[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_homepage_category_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_homepage_category_admin(
  boolean,text,text,text,uuid[],boolean) TO authenticated;
```

### RLS policies

```sql
ALTER TABLE public.homepage_category_section ENABLE ROW LEVEL SECURITY;

CREATE POLICY homepage_category_admin_read ON public.homepage_category_section
  FOR SELECT TO authenticated USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY homepage_category_create ON public.homepage_category_section
  FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));
CREATE POLICY homepage_category_edit ON public.homepage_category_section
  FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
  WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

COMMIT;
```

Read the two SQL blocks as one migration, in the shown order. No anonymous SELECT or DELETE grant/policy exists. Category image URLs are resolved for the admin editor through the existing storage mapper; no new image assets or storage table are created.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** A selected category can be deleted after Save. TASK-004 keeps its ID and shows an unavailable row in Edit so an administrator can replace it; TASK-008 covers Save-time availability. A deactivated category still shows its current details and can be replaced on Edit. Public display behavior is outside this feature.
- **⚠️ Risk — ✅ Accepted:** `/categories` has no public page yet. The user chose to keep that prefilled destination and build its page separately. TASK-009 preserves the editable value, and TASK-012 verifies it is saved and restored; this feature does not render or follow the destination.

---
**Status:** Resolved · **Spec:** `.specs/006_homepage-category/spec.md` · **Created:** 2026-11-05 · **Revised:** 2026-11-05 · **Resolved:** 2026-11-05
