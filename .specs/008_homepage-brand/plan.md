# Implementation Plan — Homepage Shop by Brand

**Feature:** `008_homepage-brand`

## 1. Objective

Add one staff-managed Shop by Brand configuration beside existing Homepage sections. Persist ordered brand IDs, text, destination, and visibility atomically. Enforce Homepage permissions and active-brand selection in Application and PostgreSQL. Build Add/Edit and brand-selection dialog from existing native components.

## 2. Tech Stack

- **Domain:** C# `HomepageBrandSection` aggregate.
- **Application:** MediatR commands and queries, FluentValidation, `Result`, `BrandDtoMapper`.
- **Infrastructure:** Supabase repository, PostgreSQL migration, RLS, RPC.
- **Web:** Blazor WebAssembly, `Shop*` components, project SCSS, `Strings.resx`.
- **Tests:** xUnit, bUnit, PostgreSQL Testcontainers, Playwright.

## 3. High-level Architecture

Follow the existing `HomepageCategorySection` split. Add and Edit use separate permissioned commands; both create a complete domain section and call one repository contract. A dedicated Homepage brand-choice query returns active brands with resolved logo URLs. The saved-section query also resolves selected brands by ID, including inactive ones, so Edit can identify and replace them.

```text
ManageHomepageBrand.razor.cs SaveAsync
  → IMediator.Send(CreateHomepageBrandCommand | UpdateHomepageBrandCommand)
  → ValidationBehavior → handler → HomepageBrandSection.Create
  → IHomepageBrandRepository.SaveAsync
  → SupabaseHomepageBrandRepository → save_homepage_brand_admin RPC
  → Result → field errors or notification, then Homepage route
```

## 4. Data Model

### Domain entities & value objects

- `HomepageBrandSection`: singleton `Id = 1`; `Title`, `ButtonLabel`, `ButtonLink`, ordered `BrandIds`, `IsVisible`. `Create` and `Update` trim text; enforce nonblank fields, safe destination, unique nonempty IDs, at most six, and at least one ID when Visible. Throw `DomainException` with `HomepageBrand_*` resource key for invariant failure. Active status remains a state-dependent handler/database check.

### DTOs (Application to Web)

- `HomepageBrandDto`: title, label, link, ordered IDs, selected `BrandDto` details, visibility. Retain missing IDs for repair.
- Reuse `BrandDto` with `Id`, `Name`, `LogoUrl`, `IsActive`; `BrandDtoMapper` resolves logo storage paths. No new logo asset or copied static brand list.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `public.homepage_brand_section` (new) | Singleton editor configuration | `id = 1`, text fields, `brand_ids uuid[]`, `is_visible` |

### Indexes

- Primary key on `id` enforces one section. No extra index: one row and at most six IDs.

## 5. Core Design Decisions

1. **Decision:** New singleton aggregate and table follow Homepage Category, with separate Create and Update commands.
   - **Why:** Existing pattern already gives action-specific permission checks, atomic writes, and absent/present handling (RULE-1, RULE-5, RULE-6; constitution 4–6).
   - **Rejected:** Upsert from one command; it blurs Create versus Edit permissions and duplicate-Add feedback.

2. **Decision:** Add draft defaults come from `HomepageBrandFormModel`: `Shop by Brand`, `View All Brands`, `Routes.Brands`, Visible, empty IDs.
   - **Why:** Matches corrected spec AC-1; staff can change every field. Existing Category form uses this pattern.
   - **Rejected:** Figma Edit example brands as Add defaults; user explicitly removed preselected brands.

3. **Decision:** `GetHomepageBrandChoicesQuery` reads only active brands through `IBrandRepository.GetActiveForHomepageAsync`; map through `BrandDtoMapper` to include logos. Search filters this active set by name in the dialog. Open loads choices and shows skeletons; retry repeats the query.
   - **Why:** `GetActiveBrandsQuery` returns only ID/name. `GetBrandsPageQuery` requires Brands View and paginates, which Homepage staff may lack (RULE-8).
   - **Rejected:** Unfiltered brand list with hidden cards; inactive data must never appear in the dialog.

4. **Decision:** `GetHomepageBrandForAdminQuery` resolves saved IDs with `GetByIdAsync`, including inactive brands. Edit keeps each inactive or missing row in saved order, shows a warning to replace it, and leaves Remove available. The dialog still shows only active brands. Save checks each selected ID is active. A database trigger locks and checks selected brand rows during the write.
   - **Why:** Edit must retain identity/order for repair after a brand changes status or is deleted (RULE-2). Staff can remove the unavailable row and choose an active replacement.
   - **Rejected:** Trusting the picker list at Save; stale data could persist.

5. **Decision:** Use `ShopDialog`, `ShopImageTile` with `ShopImagePreset.BrandLogo`, `ShopImage`, `ShopTextField`, `ShopChip`, `ShopButton`, `ShopIconButton`, `ShopSkeleton`, and `ShopTable` where their supported contracts fit. `ShopImageTile` already exposes selection state and native button semantics. Keep page-specific layout in `_homepage-brand.scss`.
   - **Why:** Maps Figma instances to existing components (constitution 14, 17, 25, 35).
   - **Rejected:** New dialog/card/input primitives for one page.

6. **Decision:** Dialog keeps a copy of ordered draft IDs. Add Brands commits that copy to the editor; Cancel, close, Escape, and backdrop discard it. Selected count includes staged IDs; cards only show active brands.
   - **Why:** Dismissal must not change draft (RULE-7). Existing Category picker uses copy-on-open.
   - **Rejected:** Mutating editor selection while cards are clicked.

7. **Decision:** Editor row arrows provide keyboard ordering; drag may supplement them on pointer devices. At six selected, Add Brand is disabled until removal. Form dispatches through `EditForm OnSubmit` and `ShopCommandValidation`.
   - **Why:** Meets keyboard and count rules without duplicate Web validation (RULE-2, FR-7; constitution 31–34).
   - **Rejected:** Drag-only ordering and page-owned input validation.

## 6. Core Functional Flow

### Flow 1: Open editor

1. `ManageHomepage` links to `Routes.Admin.ManageHomepageBrand`; page requires Homepage View.
2. `BusyState.RunAsync(BusyKeys.Homepage.LoadBrandSection, ...)` dispatches `GetHomepageBrandForAdminQuery`.
3. Null result builds empty-ID default Add form; saved result builds Edit form with selected names/logos and order. Inactive or missing selections show a replacement warning in Edit. Load error shows retry, never a false Add form.
4. Create/Edit permission controls whether actions render. Guest uses existing sign-in route handling; denied staff use existing access-denied view.

### Flow 2: Curate brands

1. Add Brand opens `ShopDialog`; `BusyKeys.Homepage.LoadBrandChoices` loads active choices with logos.
2. Search filters loaded active cards case-insensitively by name. No match shows guidance; pending load shows `ShopSkeleton`; failure shows retry.
3. Card click changes staged IDs, maximum six. Add Brands copies staged IDs to form and updates count; Cancel/Close discards them. Row removal and move controls update form order.

### Flow 3: Configure and finish

1. `ShopTextField` edits title, label, link; `ShopChip` selects status. `EditForm OnSubmit` clears old command errors and dispatches Create or Update through `BusyKeys.Homepage.SaveBrandSection`.
2. Validator checks required fields, destination, and ID count/uniqueness. Handler checks brand availability, creates domain section, then calls repository RPC. Authorization precedes validation.
3. Field failures reach `ShopCommandValidation`; state conflicts and technical failures use localized notifications. Failed Save retains draft. Successful Save notifies and navigates to `Routes.Admin.ManageHomepage`; Cancel navigates without write.

## 7. Development Plan

### Step 1 — Domain

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add `HomepageBrandSection` with complete invariants and matching Domain tests.

**Completion gate:** Domain builds; zero/one/six/seven, duplicate, blank, link, and visibility invariants pass; report public API.

### Step 2 — Application

**Depends on:** TASK-001 API.

- [ ] **TASK-002** — Add `IHomepageBrandRepository`, `HomepageBrandDto`, and `IBrandRepository.GetActiveForHomepageAsync` contracts.
- [ ] **TASK-003** — Add authorized `GetHomepageBrandForAdminQuery`, `GetHomepageBrandChoicesQuery`, Create/Update commands, shared validator, handlers, error-key constants, and focused Application tests.
- [ ] **TASK-004** — Add exact `HomepageBrand_*` English keys from Section 9 to `Strings.resx` so typed references compile.

**Completion gate:** Application builds; permission, validator, availability, and failure tests pass; later-layer contracts compile.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| Create/Update commands and `Result` | Application | Web | Stable after TASK-003 |
| `HomepageBrandDto` and `BrandDto` choices | Application | Web | Stable after TASK-003 |
| `IHomepageBrandRepository`, `IBrandRepository.GetActiveForHomepageAsync` | Application | Infrastructure | Stable after TASK-003 |
| `HomepageBrand_*` keys | Application/Web | Web and Infrastructure | Stable after TASK-004 |

### Step 4 — Infrastructure

**Depends on:** stable Application contracts.

- [ ] **TASK-005** — Add `supabase/migrations/0040_manage_homepage_brands.sql` using Section 10, plus schema/RLS/RPC tests for permissions, singleton, visibility, active selection, and races.
- [ ] **TASK-006** — Add `HomepageBrandRecord`, mapper, `SupabaseHomepageBrandRepository`, brand active-choice query in `SupabaseBrandRepository`, DI registration, and repository tests.

**Completion gate:** Migration applies; RLS denies unauthorized direct reads/writes; RPC writes atomically; repository satisfies stable interfaces.

### Step 5 — Web

**Depends on:** TASK-005 and TASK-006.

**Figma references:** [The Vape Shop, Shop by Brand](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56959&t=QoPhLgu2hu5FaRIQ-11). Re-fetch at execution. Figma dialog example has two selected cards; corrected Add behavior starts with none.

- `3218:65482`, `3218:65919`, `3218:66330`: Add desktop/tablet/phone; fields prefilled and no selected brands.
- `3169:37648`, `3169:45364`, `3169:46114`: Edit desktop/tablet/phone; ordered logo rows and settings.
- `3194:35676`, `3194:36150`, `3194:36598`: Edit skeletons.
- `3273:62103`, `3273:62247`, `3273:62363`: Brand Selection Dialog; search, cards, count, actions.
- `3273:62451`, `3273:62491`, `3273:62531`: no-results dialog.
- `3273:62571`, `3273:62731`, `3273:62859`: dialog skeleton.
- Component mapping: Figma Button/Icon Button to `ShopButton`/`ShopIconButton`; field instances to `ShopTextField`; status chips to `ShopChip`; logo to `ShopImage`/`ShopImageTile` with `BrandLogo`; dialog to `ShopDialog`; placeholders to `ShopSkeleton`. Existing brand logo URLs provide assets. No Figma image export needed.
- Responsive decision: two-column editor at desktop, stacked settings at tablet/phone; dialog card grid reduces columns and body scrolls while actions remain visible. Use shared 1024px/768px SCSS breakpoints; verify 1440, 834, 390, and 320px, plus zoom and keyboard.

- [ ] **TASK-007** — Add `Routes.Admin.ManageHomepageBrand`, Homepage navigation link, and load/save/choice `BusyKeys`.
- [ ] **TASK-008** — Build `ManageHomepageBrand.razor`/`.razor.cs`, form model, dialog, responsive `_homepage-brand.scss`, and stylesheet registration using components above.
- [ ] **TASK-009** — Add bUnit tests for defaults, saved order, inactive/missing warnings, dialog staging/search, active-only results, skeleton/retry, validation, permissions, and keyboard controls; add Playwright journey with stable `data-testid` readiness selectors.

**Completion gate:** Web builds; no hardcoded user text; permission and state behavior pass; no horizontal overflow; component reuse verified in source.

### Step 6 — Integration & pipeline

**Depends on:** TASK-005 through TASK-009.

- [ ] **TASK-010** — Run solution build, migration and focused layer tests; run `$theshop-test 008_homepage-brand`, then `$theshop-verify 008_homepage-brand`. Check Add/Edit/Hidden, inactive race, dialog dismissal, and responsive browser journey.

### Deviation procedure

- Accept a change only when approved behavior and layer boundaries remain intact; record affected TASK IDs.
- Reject scope expansion, weaker authorization, or silent stable-contract changes.
- For a contract change: stop dependent work, update contract table and affected tasks, then freeze again before resuming.

## 8. Acceptance Criteria to Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: empty Add and editable defaults | TASK-007, TASK-008, TASK-009 |
| AC-2: Edit loads saved data and order | TASK-003, TASK-006, TASK-008, TASK-009 |
| AC-3: add distinct active brand with logo/count | TASK-003, TASK-006, TASK-008, TASK-009 |
| AC-4: stop at six | TASK-001, TASK-003, TASK-008, TASK-009 |
| AC-5: boundary controls and saved order | TASK-001, TASK-005, TASK-008, TASK-009 |
| AC-6: Visible/Hidden count, duplicate/inactive rejection | TASK-001, TASK-003, TASK-005, TASK-009 |
| AC-7: required fields and safe link | TASK-001, TASK-003, TASK-005, TASK-008, TASK-009 |
| AC-8: atomic Save and Hidden Edit | TASK-005, TASK-006, TASK-008, TASK-009 |
| AC-9: Cancel discards draft | TASK-008, TASK-009 |
| AC-10: Edit skeleton and retry | TASK-003, TASK-008, TASK-009 |
| AC-11: guest and permission denial | TASK-003, TASK-005, TASK-007, TASK-009 |
| AC-12: responsive and keyboard use | TASK-008, TASK-009 |
| AC-13: active-only dialog and search | TASK-003, TASK-006, TASK-008, TASK-009 |
| AC-14: staged Apply versus dismiss | TASK-008, TASK-009 |
| AC-15: no results, loading, failure/retry | TASK-003, TASK-008, TASK-009 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- Shared `HomepageBrandInput.AddRules` runs for Create and Update. Nonblank trimmed `Title`, `ButtonLabel`, `ButtonLink` (RULE-3); `HeroBannerInput.IsDestination` for safe site path or HTTP/HTTPS URL (RULE-4); one to six IDs when Visible, zero to six when Hidden, distinct and nonempty IDs (RULE-2).
- Handler checks each selected brand through `IBrandRepository.GetByIdAsync` and rejects missing/inactive brands (RULE-2). Domain enforces the same entity invariants. Database check and row locks close the save race. No Web-side rule duplication.
- `GetHomepageBrandChoicesQuery` filters Active at repository boundary and Web filters only that result for search (RULE-8). Dialog staging implements RULE-7; repository create flag and primary key implement RULE-1; RPC transaction implements RULE-5; permission attributes and RLS implement RULE-6.

### Domain exceptions

- `DomainException` carries matching `HomepageBrand_*` key for invalid section state. Handler translates to `Result.Fail`; cancellation propagates.

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `HomepageBrand_TitleRequired` | Enter a section title. |
| `HomepageBrand_ButtonLabelRequired` | Enter a button label. |
| `HomepageBrand_ButtonLinkRequired` | Enter a button link. |
| `HomepageBrand_ButtonLinkInvalid` | Enter a site path or HTTP/HTTPS URL. |
| `HomepageBrand_SelectionInvalid` | Choose one to six different brands when visible, or up to six when hidden. |
| `HomepageBrand_BrandUnavailable` | A selected brand is inactive or unavailable. Choose another brand. |
| `HomepageBrand_AlreadyExists` | This section already exists. Reload to edit it. |
| `HomepageBrand_NotFound` | This section no longer exists. Reload to add it. |
| `HomepageBrand_LoadFailed` | The brand section could not load. Try again. |
| `HomepageBrand_SaveFailed` | The brand section could not save. Try again. |
| `HomepageBrand_ChoicesLoadFailed` | Brands could not load. Try again. |

UI keys in `Strings.resx`: `HomepageBrand_DefaultTitle` = `Shop by Brand`, `HomepageBrand_ViewAllBrands` = `View All Brands`, `HomepageBrand_AddHeading` = `Add {0}`, `HomepageBrand_EditHeading` = `Edit {0}`, `HomepageBrand_SelectedCount` = `{0} of 6 selected`; also `Brands`, `Brand`, `Actions`, `Section`, `Title`, `ButtonLabel`, `ButtonLink`, `Status`, `Visible`, `Hidden`, `AddBrand`, `AddBrands`, `SearchBrands`, `NoResults`, `NoChoices`, `EmptyTitle`, `EmptyHint`, `SelectionHint`, `RemoveFirst`, `MoveUp`, `MoveDown`, `Remove`, `UnavailableBrand`, `Absent`, `Reload`, `PickerTitle`, `Saved` under `HomepageBrand_`. Reuse shared `Strings.Save`, `Strings.Cancel`, `Strings.Loading`, `Strings.Close`.

**Edge coverage:** Empty Visible, zero Hidden, six/full, duplicate, invalid link, no logo, no search match, inactive search, deactivated/deleted selection, dialog load failure, section load failure, failed Save, Cancel, and permission denial map to the rules and keys above. Missing logo displays name through `ShopImage` placeholder. Inactive and missing saved brands show `UnavailableBrand` warning while retaining their IDs for removal and replacement. Add starts with zero brands even though the Figma dialog example contains selected cards.

## 10. Database Schema & RLS Policies

### Schema

Save as `supabase/migrations/0040_manage_homepage_brands.sql`. Migration is new; no backfill. The domain/validator performs stronger URL parsing; SQL rejects unsafe schemes, spaces, backslashes, and protocol-relative paths for direct callers.

```sql
BEGIN;

CREATE FUNCTION public.homepage_brand_ids_valid(p_ids uuid[], p_visible boolean)
RETURNS boolean LANGUAGE sql IMMUTABLE SET search_path = '' AS $$
  SELECT p_ids IS NOT NULL AND p_visible IS NOT NULL
    AND cardinality(p_ids) <= 6
    AND (NOT p_visible OR cardinality(p_ids) >= 1)
    AND array_position(p_ids, NULL) IS NULL
    AND NOT ('00000000-0000-0000-0000-000000000000'::uuid = ANY(p_ids))
    AND (SELECT count(DISTINCT selected_id) = cardinality(p_ids)
         FROM unnest(p_ids) AS selected(selected_id));
$$;

CREATE TABLE public.homepage_brand_section (
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
  brand_ids uuid[] NOT NULL,
  is_visible boolean NOT NULL DEFAULT true,
  CONSTRAINT homepage_brand_selection_valid CHECK (public.homepage_brand_ids_valid(brand_ids, is_visible))
);

CREATE FUNCTION public.homepage_brand_selection_active()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF (SELECT count(*) FROM (
      SELECT b.id FROM public.brands b
      WHERE b.id = ANY(NEW.brand_ids) AND b.is_active
      FOR SHARE
  ) selected) <> cardinality(NEW.brand_ids) THEN
    RAISE EXCEPTION 'selected brand unavailable' USING ERRCODE = 'P0001';
  END IF;
  RETURN NEW;
END; $$;

CREATE TRIGGER homepage_brand_selection_active
BEFORE INSERT OR UPDATE ON public.homepage_brand_section
FOR EACH ROW EXECUTE FUNCTION public.homepage_brand_selection_active();

REVOKE ALL ON public.homepage_brand_section FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.homepage_brand_section TO authenticated;
ALTER TABLE public.homepage_brand_section ENABLE ROW LEVEL SECURITY;
CREATE POLICY homepage_brand_admin_read ON public.homepage_brand_section
  FOR SELECT TO authenticated USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY homepage_brand_create ON public.homepage_brand_section
  FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));
CREATE POLICY homepage_brand_edit ON public.homepage_brand_section
  FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
  WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

CREATE FUNCTION public.get_homepage_brand_admin()
RETURNS SETOF public.homepage_brand_section LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT s.* FROM public.homepage_brand_section s WHERE s.id = 1;
END; $$;

CREATE FUNCTION public.save_homepage_brand_admin(
  p_create boolean, p_title text, p_button_label text, p_button_link text,
  p_brand_ids uuid[], p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
     NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.homepage_brand_section
      (id, title, button_label, button_link, brand_ids, is_visible)
    VALUES (1, p_title, p_button_label, p_button_link, p_brand_ids, p_is_visible);
  ELSE
    UPDATE public.homepage_brand_section SET title = p_title,
      button_label = p_button_label, button_link = p_button_link,
      brand_ids = p_brand_ids, is_visible = p_is_visible WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN RAISE EXCEPTION 'section not found' USING ERRCODE = 'P0002'; END IF;
  END IF;
END; $$;

REVOKE ALL ON FUNCTION public.homepage_brand_ids_valid(uuid[],boolean) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.homepage_brand_selection_active() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_homepage_brand_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_homepage_brand_admin(boolean,text,text,text,uuid[],boolean) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.homepage_brand_ids_valid(uuid[],boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_homepage_brand_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_homepage_brand_admin(boolean,text,text,text,uuid[],boolean) TO authenticated;

COMMIT;
```

Repository maps SQLSTATE `42501` to `RbacErrorKeys.AccessDenied`, `23505` to `HomepageBrand_AlreadyExists`, `P0002` to `HomepageBrand_NotFound`, `P0001` to `HomepageBrand_BrandUnavailable`, `23514` to validation/save failure, and other provider failures to `HomepageBrand_SaveFailed`. Schema tests verify direct table writes cannot bypass permission or selection checks.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

---
**Status:** Resolved · **Spec:** `.specs/008_homepage-brand/spec.md` · **Created:** 2026-11-25 · **Resolved:** 2026-11-25
