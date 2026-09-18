# Implementation Plan — Add Brand

> Companion to `.specs/add-brand/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Add an admin-only use case that lets a permission-holding staff member create a `Brand` (name required; optional logo, optional description; Active/Inactive status). The existing `Brand` entity is reference-data-only (`Id`/`Name`/`Slug`, no invariants) — this feature turns it into a full aggregate with invariants (name required and ≤100 chars, description ≤250 chars) and adds `Description`, `LogoPath`, and `IsActive`. Creation is gated end-to-end by a new fine-grained `brands.create` permission (UI policy, `[RequiresPermission]`, and Supabase RLS), and Inactive brands are hidden from every customer surface via the `brands` RLS SELECT predicate. The logo is uploaded to a new public `brand-logos` Supabase Storage bucket, mirroring the existing product-image storage pattern.

## 2. Tech Stack

- **Domain:** C# (no external deps) — extends `Brand` entity + `PermissionCatalogue`.
- **Application:** MediatR, FluentValidation, AutoMapper, `Result<T>`; `[RequiresPermission]` + `AuthorizationBehavior` (RBAC pipeline).
- **Infrastructure:** `supabase-csharp` — Postgrest for `brands`, Supabase Storage for the logo bucket. Migration applied via Supabase MCP.
- **Web:** MudBlazor (`MudForm`, `MudTextField`, **`MudFileUpload`** for the logo per user direction, `MudImage` preview, a two-option chip/toggle status control per the Figma), bUnit for component tests.
- **Persistence:** Supabase (PostgreSQL + RLS + Storage).

## 3. High-level Architecture

```
Admin fills AddBrand.razor (name, MudFileUpload logo, description, status) → clicks Save
   ↓  (BusyState.RunAsync(BusyKeys.AddBrand, …))
IMediator.Send(CreateBrandCommand(Name, Description, Logo?, IsActive))
   ↓  AuthorizationBehavior → [RequiresPermission("brands.create")]  → ValidationBehavior → CreateBrandCommandValidator
CreateBrandHandler (Application)
   ├── IBrandRepository.ExistsByNormalizedNameAsync(name)   // RULE-2 pre-check
   ├── Brand.Create(name, description, isActive)            // invariants enforced here (RULE-1/RULE-3)
   ├── IFileStorage.UploadAsync(StorageArea.BrandLogos, brand.Id, …) → key   // optional; then brand.AttachLogo(key)
   └── IBrandRepository.AddAsync(brand)
   ↓
SupabaseBrandRepository / SupabaseFileStorage (Infrastructure) → brands table + brand-logos bucket (RLS-gated: authorize('brands.create'))
   ↓
Result<BrandDto> → AddBrand.razor → Snackbar(Strings.Brand_Created) → NavigateTo(Routes.Admin.ManageBrands)
```

## 4. Data Model

### Domain entities & value objects

- **`Brand`** (extend `Domain/Entities/Brand.cs`) — new state: `string? Description`, `string? LogoPath`, `bool IsActive`.
  - `static Brand Create(string name, string? description, bool isActive)` — generates `Id`, trims `Name`, generates `Slug` from the trimmed name (lower-cased, non-alphanumerics → single hyphens), enforces invariants. Throws `BrandNameRequiredException` (RULE-1), `BrandNameTooLongException` (RULE-3, 100), `BrandDescriptionTooLongException` (RULE-3, 250). On a `brands.slug` unique collision that is *not* itself a name collision, Infrastructure appends a short unique suffix so slug generation never blocks a legitimately distinct name (name uniqueness, RULE-2, stays the user-facing rule).
  - `void AttachLogo(string logoPath)` — sets `LogoPath` after a successful storage upload.
  - `static Brand Rehydrate(Guid id, string name, string slug, string? description = null, string? logoPath = null, bool isActive = true)` — replaces the current `Create(id,name,slug)` (renamed; no invariants). Optional args keep `ProductMapper`'s existing 3-arg call source-compatible.
- **`PermissionCatalogue.Brands`** (new nested class) — `View`/`Create`/`Edit`/`Delete` (`brands.view` … `brands.delete`), appended to `All`. Only `Create` is consumed this feature; the full four-action set keeps the catalogue's uniform module shape (see Decision 2).
- **Domain exceptions** (`Domain/Exceptions/`) — `BrandNameRequiredException`, `BrandNameTooLongException`, `BrandDescriptionTooLongException`, each extending `DomainException` with `MessageKey = nameof(Strings.{Key})`.

### DTOs (Application → Web)

- **`BrandDto`** — `record BrandDto(Guid Id, string Name, string Slug, string? Description, string? LogoUrl, bool IsActive)`. `LogoUrl` is the resolved public bucket URL (mapper turns the stored key into a URL).
- **`BrandLogoUpload`** — `record BrandLogoUpload(byte[] Content, string FileName, string ContentType)`; the transport for the optional logo inside `CreateBrandCommand` (avoids leaking `IBrowserFile`/streams into Application).

### Application interfaces (generic storage)

- **`IFileStorage`** (`Common/Interfaces/`) — one provider-neutral, reusable file-storage contract replacing the per-feature `IProductImageStorage`: `Task<string> UploadAsync(StorageArea area, Guid ownerId, Stream content, string fileName, string contentType, CancellationToken ct)` (returns the stored object key) and `Task DeleteAsync(StorageArea area, string objectKey, CancellationToken ct)`.
- **`StorageArea`** enum (`Common/Storage/`) — the logical storage namespaces (`ProductImages`, `BrandLogos`), the single extension point for future storage features. Infrastructure maps each area → bucket + key prefix; Application never names a Supabase bucket (Rule 3).
- **`IBrandRepository`** (`Common/Interfaces/`) — `Task<bool> ExistsByNormalizedNameAsync(string name, CancellationToken ct)` (RULE-2 pre-check) and `Task AddAsync(Brand brand, CancellationToken ct)`.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `brands` (modified) | add mutable brand attributes | `+ description TEXT`, `+ logo_path TEXT`, `+ is_active BOOLEAN NOT NULL DEFAULT TRUE`, `+ created_at TIMESTAMPTZ NOT NULL DEFAULT now()` |
| `permissions` (seed) | register the new module | 4 rows: `brands.view/create/edit/delete`, module `brands` |
| `role_permissions` (seed) | grant to system roles | `brands` module → `Admin`; all → `SuperAdmin` (spec: Admin + Super Admin hold it) |
| `storage.buckets` (new) | logo storage | `brand-logos` (public) |

### Indexes

- `ux_brands_name_normalized` — `UNIQUE (lower(btrim(name)))` on `brands` — enforces RULE-2 (case/space-insensitive name uniqueness) at the DB as the race-condition backstop.
- Existing `brands.slug UNIQUE` retained.

## 5. Core Design Decisions

1. **Decision:** Extend the existing `Brand` entity into an invariant-enforcing aggregate (`Create`/`AttachLogo`/`Rehydrate`), rather than adding a parallel type.
   - **Why:** Rule 6 — business behavior/invariants belong on the entity. RULE-1/RULE-3 are integrity invariants, so `Brand.Create` throws domain exceptions; the FluentValidation validator mirrors them for field-level UX. `Rehydrate` follows the existing `Product` entity convention (`Create` = new, `Rehydrate` = persisted).
   - **Rejected:** A new `AdminBrand` type — duplicates identity, and `ProductMapper` already consumes `Brand`.

2. **Decision:** Add a full `Brands` module (`view/create/edit/delete`) to `PermissionCatalogue`, but only use `brands.create` here.
   - **Why:** The catalogue is uniform (every module is a 4-action set feeding the `permissions` seed from `All`); a `create`-only module would break that shape and the seed generator. `architecture-admin.md` recipe step 1 requires the permission before anything else.
   - **Rejected:** Reusing `products.create` — the spec mandates a *specific* fine-grained permission (Constraint §4), never a borrowed or "is-admin" gate.

3. **Decision:** Enforce Inactive-brand invisibility (FR-7 / AC-7) in the `brands` RLS SELECT predicate: `is_active OR authorize('brands.view')`.
   - **Why:** Layer 4 (RLS) is the only real security boundary. Because `get_catalogue_filters()` is `SECURITY INVOKER`, restricting the anon SELECT automatically drops inactive brands from the catalogue's brand filter with no separate query change; the RPC's brand facet gets `WHERE b.is_active` as defence-in-depth, mirroring its existing `is_published` pattern.
   - **Rejected:** Filtering only in the RPC/UI — a direct Supabase read would still expose inactive brands.

4. **Decision:** Replace the per-feature `IProductImageStorage` with **one generic `IFileStorage`** abstraction (Application) → a single `SupabaseFileStorage` (Infra), driven by a `StorageArea` enum and an area→bucket registry. This feature adds `StorageArea.BrandLogos`; the existing product path becomes `StorageArea.ProductImages` (per user direction — storage must be generic, maintainable, and extensible for future features).
   - **Why:** Avoids a new interface + implementation + key-builder per feature. Adding a future storage need is one enum value + one registry entry + one bucket migration. Keeps the Supabase SDK in Infrastructure (Rule 3) — Application passes a logical `StorageArea`, never a bucket name. The `--desc` pins `MudFileUpload` on the Web side, which hands bytes to the command.
   - **Retained:** Per-area **buckets** (`product-images`, `brand-logos`), each with its own permission-scoped RLS — the registry maps area → bucket + key prefix (`brands/{ownerId}/{guid}{ext}`). This preserves the existing product-images security model unchanged (code-only refactor; no product migration).
   - **Rejected:** A single shared bucket for all areas — forces path-prefix predicates into one storage RLS policy, coarser and harder to reason about than one policy set per bucket. Also rejected: base64 logo column — bloats rows, no CDN, breaks the storage-key convention.

5. **Decision:** Upload the logo *after* `Brand.Create` and the uniqueness check, then a single insert; on insert failure, best-effort delete the uploaded object.
   - **Why:** The storage key namespaces by `brand.Id`, which the entity generates; compensation keeps "no partial brand appears" (Section 5 edge case) without a two-phase DB write.
   - **Rejected:** Insert-then-upload-then-update — two writes and a wider partial-state window.

6. **Decision:** Add a minimal `ManageBrands` admin shell page as AddBrand's entry point and post-save return target.
   - **Why:** FR-6/AC-1 return the user to "manage-brands", but that page is out of scope and does not exist; a shell (mirroring the existing `ManageProducts` shell) is the minimal enabler. See Section 11 (📌).
   - **Rejected:** Navigating to `/admin` — contradicts the spec's stated destination.

## 6. Core Functional Flow

### Flow 1: Add a brand successfully (Behavior 1)

1. `AddBrand.razor` — `MudForm` bound to name/description/status; user clicks the save `MudButton` → `SaveAsync()`.
2. `await BusyState.RunAsync(BusyKeys.AddBrand, …)` → `Mediator.Send(new CreateBrandCommand(Name, Description, Logo, IsActive))`.
3. `AuthorizationBehavior` checks `[RequiresPermission("brands.create")]` → on miss, `Result.Fail(RbacErrorKeys.AccessDenied)`.
4. `ValidationBehavior` runs `CreateBrandCommandValidator` (Section 9). On failure → `Result.Fail(nameof(Strings.{Key}))`.
5. `CreateBrandHandler`: `ExistsByNormalizedNameAsync` → if taken, `Result.Fail(nameof(Strings.Brand_AlreadyExists))`.
6. `Brand.Create(name, description, isActive)` (catches `DomainException` → `Result.Fail(ex.MessageKey)`).
7. If `Logo` present → `IFileStorage.UploadAsync(StorageArea.BrandLogos, brand.Id, …)` → `brand.AttachLogo(key)`.
8. `IBrandRepository.AddAsync(brand)`; returns `Result.Ok(mapper.Map<BrandDto>(brand))`.
9. Page: `Snackbar.Add(Strings.Brand_Created)` → `Navigation.NavigateTo(Routes.Admin.ManageBrands)`.

### Flow 2: Attach / preview / remove a logo (Behavior 2)

1. `MudFileUpload` (`Accept=".png,.jpg,.jpeg,.webp"`, `MaximumFileCount="1"`) → `OnFilesChanged` reads the `IBrowserFile`.
2. Client guard: reject type/size (>2 MB) before accepting → sets `Localizer`-resolved field error, leaves other fields intact; otherwise reads bytes into state and renders a `MudImage` preview with a remove `MudIconButton`.
3. Remove → clears logo state, form returns to no-logo (Section 5 edge case).

### Flow 3: Submit invalid details (Behavior 3)

- Missing/whitespace name → validator → `Strings.Brand_NameRequired`; over-length → `Brand_NameTooLong` / `Brand_DescriptionTooLong`; duplicate → handler → `Brand_AlreadyExists`; bad logo → validator (server) + client guard → `Brand_LogoInvalidType` / `Brand_LogoTooLarge`. Each renders next to its field via `Localizer[result.Error]`; all entered input is preserved.

### Flow 4: Attempt without permission (Behavior 4)

- `AddBrand.razor` wraps content in `<AuthorizeView Policy="@PolicyNames.Permission(PermissionCatalogue.Brands.Create.Code)">` with `<AccessDeniedView />` in `NotAuthorized`; the ManageBrands nav entry renders only inside the matching `AuthorizeView` (hidden, not disabled). Direct API calls are stopped by RLS `authorize('brands.create')`.

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — Extend `Brand` (`Domain/Entities/Brand.cs`): add `Description`/`LogoPath`/`IsActive`; add `Create(name, description, isActive)` (Id + slug generation, invariants), `AttachLogo(logoPath)`; rename current factory to `Rehydrate(id, name, slug, description?, logoPath?, isActive?)`.
- [ ] **TASK-002** — Add `BrandNameRequiredException`, `BrandNameTooLongException`, `BrandDescriptionTooLongException` in `Domain/Exceptions/` (extend `DomainException`, `MessageKey = nameof(Strings.{Key})`).
- [ ] **TASK-003** — Add nested `Brands` class (`View`/`Create`/`Edit`/`Delete`) to `PermissionCatalogue` and append the four entries to `All`.
- [ ] **TASK-004** — Domain unit tests (`BrandTests.cs`): required/trim/length invariants, `IsActive` default, `AttachLogo`, slug generation; `PermissionCatalogueTests` asserts the four `brands.*` codes are present.

**Completion gate:** Domain builds · invariants covered by tests · no outer-layer type leaked · public API (factory signatures, exception `MessageKey`s, `PermissionCatalogue.Brands`) reported for later steps.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-005** — `CreateBrandCommand` + `CreateBrandHandler` + `CreateBrandCommandValidator` in `Features/Brands/Commands/CreateBrand/`; decorate command `[RequiresPermission("brands.create")]` (via `PermissionCatalogue.Brands.Create.Code`).
- [ ] **TASK-006** — `IBrandRepository` (`ExistsByNormalizedNameAsync`, `AddAsync`) in `Common/Interfaces/`; generic `IFileStorage` (`UploadAsync`, `DeleteAsync`) in `Common/Interfaces/` + `StorageArea` enum in `Common/Storage/` (`ProductImages`, `BrandLogos`) — replaces `IProductImageStorage`; `BrandDto`, `BrandLogoUpload`, and `BrandProfile` (AutoMapper) under the feature folder.
- [ ] **TASK-007** — New error + confirmation keys in `Strings.resx` (Section 9), mirrored in `Strings.fr.resx` with `[TODO]` placeholders.
- [ ] **TASK-008** — Application tests (`CreateBrandHandlerTests`, `CreateBrandCommandValidatorTests`): happy path, duplicate, invalid logo, optional-fields-omitted, domain-exception translation.

**Completion gate:** feature-folder convention followed · handler translates every Section 9 outcome to a key · all Infra/Web-consumed contracts compile.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `CreateBrandCommand` / `Result<BrandDto>` / `BrandLogoUpload` | Application | Web | Stable |
| `IBrandRepository` | Application | Infrastructure | Stable |
| `IFileStorage` + `StorageArea` | Application | Infrastructure | Stable |
| `BrandDto` shape | Application | Web | Stable |
| `PermissionCatalogue.Brands.Create.Code` = `"brands.create"` | Domain | Web, Infra (RLS) | Stable |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`IBrandRepository`, `IFileStorage` stable).

- [ ] **TASK-009** — Migration `0012_add_brand_admin.sql` (apply via Supabase MCP): `ALTER TABLE brands` (add `description`, `logo_path`, `is_active`, `created_at`); `ux_brands_name_normalized`; replace `brands_public_read` with `is_active OR authorize('brands.view')` + add `brands_admin_insert` (`authorize('brands.create')`); create public `brand-logos` bucket + storage RLS (read `brands.view`, insert `brands.create`, update `brands.edit`, delete `brands.delete`); seed `brands.*` into `permissions` and assign to `Admin`/`SuperAdmin`; harden `get_catalogue_filters()` brand facet with `WHERE b.is_active`. (Exact SQL in Section 10.)
- [ ] **TASK-010** — Extend `BrandRecord` (`description`, `logo_path`, `is_active`, `created_at`); add `BrandMapper` (`ToDomain` via `Brand.Rehydrate` / `ToRecord`); update `ProductMapper` for the renamed `Brand.Rehydrate`.
- [ ] **TASK-011** — Generic storage: `SupabaseFileStorage : IFileStorage` with an internal `StorageArea → (bucket, keyPrefix)` registry (`ProductImages → product-images`, `BrandLogos → brand-logos`) and one object-key builder (`{prefix}/{ownerId}/{guid}{ext}`); remove `IProductImageStorage`/`SupabaseProductImageStorage`/`ProductImageKey`. `SupabaseBrandRepository : IBrandRepository` (slug-collision suffix on unique violation, Decision 1). Register `IFileStorage` and `IBrandRepository` in `Infrastructure/DependencyInjection.cs` (replacing the `IProductImageStorage` registration).
- [ ] **TASK-012** — Failure translation: Postgres unique-violation (normalized name) → `Result.Fail(nameof(Strings.Brand_AlreadyExists))` backstop (mitigates the concurrent-create race); best-effort `IFileStorage.DeleteAsync(StorageArea.BrandLogos, key)` compensation if the insert fails after a logo upload (mitigates the orphaned-object risk, Decision 5).

**Completion gate:** migration applies cleanly · RLS policies match Section 10 verbatim · repositories satisfy the frozen interfaces · no Infrastructure type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (command / `BrandDto` / permission code stable).

**Figma references** *(re-fetched by `shop-ui-implementer` at build time)*

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Nodes** (enumerated via the plugin bridge; "Products" heading text in the design is a copy artifact — render the Add-Brand heading string):
  - `2465:1128` — "Create Brand" page frame (announcement bar / appbar / breadcrumb / footer are shared chrome, not built here).
  - `2465:1137` — the form container ("Frame 39") holding the logo area and field stack.
  - `2490:1257` / `2493:6632` — the logo upload control ("Add Logo", plus-icon button) → `MudFileUpload` with `MudImage` preview + remove.
  - `2493:4239` — Brand Name text field.
  - `2493:4240` — Description text field.
  - `2496:6638` — Status section: label + two chips (`2496:6641` Active / `2496:6642` Inactive) → a two-option single-select chip/toggle control, defaulting Active.
  - `2493:4247` — Actions row: `2493:4248` Create (submit) / `2493:4249` Cancel.

- [ ] **TASK-013** — Add `Routes.Admin.ManageBrands` (`/admin/brands`) and `Routes.Admin.AddBrand` (`/admin/brands/new`); minimal `ManageBrands.razor(.cs)` shell gated on `brands.view` (post-save return target + AddBrand entry point, mirroring `ManageProducts` shell).
- [ ] **TASK-014** — `AddBrand.razor(.cs)` under `Pages/Admin/`: `MudForm` with `MudTextField` (name/description — `Placeholder`, sibling `MudText Typo="Typo.caption"` labels per Rule 17), **`MudFileUpload`** logo (node `2490:1257`) with `MudImage` preview + remove `MudIconButton`, a two-option single-select chip/toggle status control (node `2496:6638`) defaulting Active, Create/Cancel actions (node `2493:4247`); `<AuthorizeView Policy="@PolicyNames.Permission(PermissionCatalogue.Brands.Create.Code)">` with `<AccessDeniedView />` in `NotAuthorized`; `[Route(Routes.Admin.AddBrand)]` on the partial.
- [ ] **TASK-015** — Wire `SaveAsync` to `Mediator.Send` via `BusyState.RunAsync(BusyKeys.AddBrand, …)`; handle every state (validation/duplicate-conflict/success→snackbar+`NavigateTo(ManageBrands)`/technical failure/unauthorized); client-side logo type + 2 MB guard with per-field error, preserving other input.
- [ ] **TASK-016** — `BusyKeys.AddBrand`; all UI resource strings from Section 9 in `Strings.resx` + `Strings.fr.resx`.
- [ ] **TASK-017** — bUnit tests (`AddBrandTests.cs`): renders for a permitted user, `AccessDeniedView` for a denied user, logo preview/remove, validation-error surfacing, success navigation.

**Completion gate:** matches Figma node `2465:1128` · MudBlazor-only, no hardcoded strings/design tokens (rules 2–5, 11, 16, 17) · consumes only frozen contracts · unauthorized users see `AccessDeniedView`, nav entry hidden.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-018** — Cross-layer verification: solution builds, DI resolves `IBrandRepository`/`IBrandLogoStorage`, migration applied, happy + duplicate + invalid-logo + unauthorized flows work end-to-end; a created Inactive brand is absent from the catalogue brand filter.
- [ ] **TASK-019** — Run `/theshop.test add-brand` → `/theshop.verify add-brand` → `/theshop.review add-brand` → `/theshop.document`.

### Deviation procedure

- **Accept** a deviation only if it preserves approved behavior and layer boundaries, aligns with existing conventions, and doesn't weaken authorization/integrity or expand scope.
- **Reject** it if it changes a requirement, adds business behavior, violates dependency direction, silently alters a frozen contract, or smuggles in unrelated refactoring.
- **Contract change:** stop dependent work → update the freeze table and affected TASK ids here → resume only after re-freezing.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: valid name → created, confirmation, return to manage-brands, available in store | TASK-001, TASK-005 (handler happy path), TASK-011 (persist), TASK-013 (return target), TASK-014/TASK-015 (form + nav), TASK-009 (is_active RLS makes Active brand visible) |
| AC-2: empty/whitespace name refused with "name required" | TASK-001 (invariant), TASK-002, TASK-005 (validator), TASK-004/TASK-008 (tests) |
| AC-3: name differing only by case/spaces refused, input preserved | TASK-005 (uniqueness pre-check), TASK-009 (`ux_brands_name_normalized`), TASK-012 (unique-violation → key), TASK-015 (preserve input) |
| AC-4: valid logo attached → created with logo, previewed before save | TASK-014 (`MudFileUpload` + preview), TASK-006/TASK-011 (`IFileStorage`), TASK-005 (upload+`AttachLogo`) |
| AC-5: bad type/oversize logo refused with message, rest preserved | TASK-005 (server validator), TASK-015 (client guard), TASK-007 (`Brand_LogoInvalidType`/`Brand_LogoTooLarge`) |
| AC-6: only a name entered → created successfully | TASK-005 (optional fields), TASK-008 (test), TASK-014 (optional inputs) |
| AC-7: Inactive brand in admin list but hidden from customers + product assignment + catalogue filter | TASK-001 (`IsActive`), TASK-014 (status control), TASK-009 (`brands` RLS `is_active OR authorize('brands.view')` + RPC facet hardening) |
| AC-8: user without permission denied, incl. direct link | TASK-005 (`[RequiresPermission]`), TASK-009 (RLS `authorize('brands.create')`), TASK-014 (`AuthorizeView`/`AccessDeniedView`), TASK-003 (catalogue entry) |
| AC-9: EN/FR follows active language | TASK-007, TASK-016 (all keys in `Strings.resx` + `Strings.fr.resx`) |
| AC-10: keyboard + screen-reader operable, focus visible, validation announced | TASK-014 (MudForm semantics, labels associated), TASK-017 (a11y assertions) |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `CreateBrandCommandValidator`:
  - `Name` — `NotEmpty()` after trim (RULE-1) → `Strings.Brand_NameRequired`; `MaximumLength(100)` on trimmed (RULE-3) → `Strings.Brand_NameTooLong`.
  - `Description` — `MaximumLength(250)` when present (RULE-3) → `Strings.Brand_DescriptionTooLong`.
  - `Logo` (when present, RULE-4) — `ContentType ∈ {image/png, image/jpeg, image/webp}` → `Strings.Brand_LogoInvalidType`; `Content.Length ≤ 2 MB` → `Strings.Brand_LogoTooLarge`.

### Domain exceptions

- `BrandNameRequiredException` — empty/whitespace name → `nameof(Strings.Brand_NameRequired)`.
- `BrandNameTooLongException` — >100 chars → `nameof(Strings.Brand_NameTooLong)`.
- `BrandDescriptionTooLongException` — >250 chars → `nameof(Strings.Brand_DescriptionTooLong)`.

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `Brand_NameRequired` | "Brand name is required." |
| `Brand_NameTooLong` | "Brand name must be 100 characters or fewer." |
| `Brand_DescriptionTooLong` | "Description must be 250 characters or fewer." |
| `Brand_AlreadyExists` | "A brand with this name already exists." |
| `Brand_LogoInvalidType` | "Logo must be a PNG, JPG, or WebP image." |
| `Brand_LogoTooLarge` | "Logo must be 2 MB or smaller." |
| `Brand_CreateFailed` | "The brand could not be created. Please try again." |
| `Brand_Created` | "Brand created." |

UI strings (new): `AddBrand_PageTitle`, `AddBrand_Heading`, `AddBrand_NameLabel`, `AddBrand_NamePlaceholder`, `AddBrand_DescriptionLabel`, `AddBrand_DescriptionPlaceholder`, `AddBrand_LogoLabel`, `AddBrand_LogoUploadButton`, `AddBrand_LogoRemove`, `AddBrand_StatusLabel`, `AddBrand_StatusActive`, `AddBrand_StatusInactive`, `AddBrand_SaveButton`, `AddBrand_CancelButton`, `ManageBrands_PageTitle`, `ManageBrands_Heading`, `ManageBrands_ShellNotice`. Access-denied text reuses existing RBAC strings.

Unexpected technical failures (connection loss, storage 5xx) surface as `Brand_CreateFailed` (Section 5 edge case) with input preserved; all keys mirrored in `Strings.fr.resx` (`[TODO]` acceptable first pass — the review step's French gate catches stragglers).

## 10. Database Schema & RLS Policies

### Schema
```sql
ALTER TABLE brands
    ADD COLUMN IF NOT EXISTS description TEXT,
    ADD COLUMN IF NOT EXISTS logo_path   TEXT,
    ADD COLUMN IF NOT EXISTS is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS created_at  TIMESTAMPTZ NOT NULL DEFAULT now();

-- RULE-2: case/space-insensitive name uniqueness, enforced at the DB.
CREATE UNIQUE INDEX IF NOT EXISTS ux_brands_name_normalized
    ON brands (lower(btrim(name)));

-- New permission module (generated from PermissionCatalogue.Brands).
INSERT INTO permissions (code, module) VALUES
    ('brands.view', 'brands'), ('brands.create', 'brands'),
    ('brands.edit', 'brands'), ('brands.delete', 'brands')
ON CONFLICT (code) DO NOTHING;

-- Admin gets the brands module; SuperAdmin already gets everything via its seed.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r JOIN permissions p ON p.module = 'brands'
WHERE r.name_key = 'Admin'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r JOIN permissions p ON p.module = 'brands'
WHERE r.name_key = 'SuperAdmin'
ON CONFLICT DO NOTHING;

-- Logo bucket (public, mirrors product-images).
INSERT INTO storage.buckets (id, name, public)
VALUES ('brand-logos', 'brand-logos', true)
ON CONFLICT (id) DO NOTHING;
```

### RLS policies (the only real security boundary — per `rules/architecture-admin.md`)
```sql
-- brands table: customers see only ACTIVE brands (FR-7/AC-7); staff with
-- brands.view see all. Write gated on brands.create.
DROP POLICY IF EXISTS "brands_public_read" ON brands;
CREATE POLICY "brands_read" ON brands
    FOR SELECT USING (is_active OR (SELECT public.authorize('brands.view')));

CREATE POLICY "brands_admin_insert" ON brands
    FOR INSERT WITH CHECK ((SELECT public.authorize('brands.create')));

-- brand-logos storage objects: read for brands.view, write per action.
CREATE POLICY "brand_logos_read" ON storage.objects
    FOR SELECT USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.view')));
CREATE POLICY "brand_logos_insert" ON storage.objects
    FOR INSERT WITH CHECK (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.create')));
CREATE POLICY "brand_logos_update" ON storage.objects
    FOR UPDATE USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.edit')))
    WITH CHECK (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.edit')));
CREATE POLICY "brand_logos_delete" ON storage.objects
    FOR DELETE USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.delete')));
```

`get_catalogue_filters()` is `SECURITY INVOKER`, so the `brands_read` predicate already drops inactive brands from the anon-facing filter facet; TASK-009 additionally adds `WHERE b.is_active` to the RPC's brand-facet subquery as defence-in-depth (matching its existing `is_published` posture). Public logo reads use the bucket's public object URL (no SELECT policy needed); the admin `brand_logos_read` policy covers future management listing.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved. The manage-brands shell (Decision 6), the full `Brands` permission module (Decision 2), auto-generated slugs with collision suffixing (Section 4 / Decision 1), the generic `IFileStorage` refactor (Decision 4), the orphaned-logo compensation and concurrent-name unique-index backstop (Decision 5 / TASK-012) are all settled decisions in the body above. Figma nodes are enumerated in Section 7 Step 5.

---
**Status:** Resolved · **Spec:** `.specs/add-brand/spec.md` · **Created:** 2026-07-20 · **Resolved:** 2026-07-20
