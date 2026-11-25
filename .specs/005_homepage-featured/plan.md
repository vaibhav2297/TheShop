# Implementation Plan — Manage Homepage Featured

**Feature:** `005_homepage-featured`

Companion: `.specs/005_homepage-featured/spec.md` (Confirmed).

## 1. Objective

Add singleton Featured administration with two fixed tile positions. Persist section text, visibility, tile text, destinations, and image references atomically. Reuse native controls, Homepage permissions, image inspection, and established heading behavior. Implement desktop Section panel stopping within editor bounds; preserve normal tablet/phone scrolling. No public homepage work.

## 2. Tech Stack

- **Domain:** pure C# entity, immutable tile value object, domain exceptions.
- **Application:** existing MediatR authorization/validation pipeline, FluentValidation, `Result`/`Result<T>`, `ImageUpload`, `IFileStorage`.
- **Infrastructure:** existing Supabase C# client, PostgreSQL/RLS, private Storage, browser image inspector.
- **Web:** .NET 10 Blazor WebAssembly, native Shop controls, `Strings.resx`, project SCSS, small JS module.
- **Tests:** existing xUnit, NSubstitute, bUnit, PostgreSQL Testcontainers, Playwright projects. No new package.

## 3. High-level Architecture

New types below are proposed feature additions. Existing reuse appears explicitly in Section 5. Application owns validation and save coordination; Web owns draft state and presentation. SDK types stay in Infrastructure.

```text
ManageFeatured / EditForm
  IMediator.Send(CreateFeaturedCommand | UpdateFeaturedCommand)
  AuthorizationBehavior, then ValidationBehavior
  FeaturedWrite: load current, upload replacements, construct FeaturedSection
  IFeaturedRepository / SupabaseFeaturedRepository
  save_featured_admin RPC, featured_sections row protected by RLS
  Result, field errors or notification; navigate only on confirmed success

GetFeaturedForAdminQuery: repository read, signed image URLs, FeaturedDto?
ValidateFeaturedImageQuery: shared inspector, Featured square-image rules
```

## 4. Data Model

### Domain entities & value objects

- `Entities/FeaturedSection.cs`: fixed `short Id => 1`, `Title`, `FeaturedTile Tile1`, `FeaturedTile Tile2`, `IsVisible`. `Create(title, tile1, tile2, isVisible)`, `Rehydrate(...)`, `Update(...)`. Validate complete replacement before mutating state. No collection, position editor, deletion, or extra identifiers.
- `ValueObjects/FeaturedTile.cs`: immutable `Label?`, `Title`, `ButtonLabel?`, `ButtonLink?`, `ImageKey`; factory `Create(...)`. Require image/title; button fields both absent or both present. Section requires nonblank title and two nonnull tiles.
- Exceptions under `Domain/Exceptions/`: `FeaturedSectionTitleRequiredException`, `FeaturedTileTitleRequiredException`, `FeaturedImageRequiredException`, `FeaturedButtonPairRequiredException`. Each exposes `MessageResourceKey`, following existing Hero Banner exceptions. No Web resource dependency in Domain.

### Application contracts

All DTOs under `Features/Homepage/DTOs/`; interfaces under `Features/Homepage/Interfaces/`. Async interface methods accept `CancellationToken`.

```csharp
// New immutable records; normalize text with existing HeroBannerInput helpers.
FeaturedImageDto(string ObjectKey, string PreviewUrl)
FeaturedTileDto(string? Label, string Title, string? ButtonLabel,
    string? ButtonLink, FeaturedImageDto Image)
FeaturedDto(string Title, FeaturedTileDto Tile1, FeaturedTileDto Tile2, bool IsVisible)
FeaturedTileInput(string? Label, string Title, string? ButtonLabel,
    string? ButtonLink, ImageUpload? NewImage, bool RemoveImage)
CreateFeaturedCommand(string Title, FeaturedTileInput Tile1,
    FeaturedTileInput Tile2, bool IsVisible) // IRequest<Result>
UpdateFeaturedCommand(string Title, FeaturedTileInput Tile1,
    FeaturedTileInput Tile2, bool IsVisible) // IRequest<Result>
GetFeaturedForAdminQuery() // IRequest<Result<FeaturedDto?>>
ValidateFeaturedImageQuery(ImageUpload Image) // IRequest<Result>

// IFeaturedRepository: same cancellation signatures as IHeroBannerRepository.
Task<FeaturedSection?> GetAsync(CancellationToken cancellationToken);
Task<Result> AddAsync(FeaturedSection section, CancellationToken cancellationToken);
Task<Result> UpdateAsync(FeaturedSection section, CancellationToken cancellationToken);
```

Create requires View + Create; Update requires View + Edit. Both queries require View. Client never supplies trusted existing image keys: write handler loads them from saved configuration. `NewImage == null && !RemoveImage` retains saved image on Update. Removal without replacement fails validation. Create requires both new images.

### Persistence

`featured_sections` stores one row, including two sets of fixed tile columns. Primary key/check `id=1` prevents duplicate Add. No secondary index needed. `FeaturedSectionRecord` and `FeaturedSectionMapper` live under Infrastructure `Persistence/Records/` and `Persistence/Mappers/`; map fixed columns to tile value objects.

Add `StorageArea.FeaturedImages` and registry entry in existing `SupabaseFileStorage.Areas`: private bucket `featured-images`, prefix `featured`. Immutable keys: `featured/{staff-user-id}/{guid:N}.{png|jpg|webp}`. Preview URLs expire after 900 seconds; never persist signed URLs. Reopening/reloading refreshes previews.

## 5. Core Design Decisions

1. **Fixed row and two typed tiles.** RULE-2 requires one section and exactly two positions. Fixed columns enforce both without a child-table transaction or variable collection. Concurrent Edit uses last successful complete-row Save; user confirmed older drafts may overwrite earlier saves. No version conflict detection. Rejected general campaign/tile collection.
2. **Reuse existing byte inspector and destination helpers.** `IHeroBannerImageInspector` / `BrowserHeroBannerImageInspector` / `heroBannerImages.js` already return detected MIME, decoded dimensions, and validity without enforcing banner geometry. Consume unchanged; `FeaturedImageValidation` owns square/minimum/5 MB rules. Reuse `HeroBannerInput.Required`, `Optional`, `IsDestination`, already shared by Fresh Drops. User accepts existing editor validation boundary: authorized direct API callers can bypass geometry/byte-decoding and destination checks. Server still enforces permissions, row constraints/references, and bucket MIME/size limits. No trusted server decoder or new validation service. Reject duplicate decoder/link parser or unrelated renaming.
3. **Immutable uploads, atomic row commit.** Upload only changed images through `IFileStorage`, then persist complete row with one RPC. No object overwrite. Do not delete committed or staged objects from this feature; bucket grants read/insert only. This prevents cleanup from breaking a write with unknown outcome. User accepts private unused-file retention and storage growth after replacement or failed uploads. No cleanup job in this feature. Rejected deleting old images or treating network failure as proof of rollback.
4. **Existing permission boundary.** Reuse `PermissionCatalogue.Homepage.ViewCode/CreateCode/EditCode`; no new permission module. Route, controls, request attributes, table/RPC checks, and Storage policies enforce existing capabilities. Add has no upsert. Rejected role-name gates and browser-only authorization.
5. **Native component reuse.** Rule 35 mapping below uses supported parameters. User clarified the rule applies throughout ShopImageUpload: drop zones, previews, and grid add tiles follow the configured image preset. One shared Sass preset map drives ShopImage and ShopImageUpload, including responsive Hero/Banner ratios. Generic preset/responsive classes replace square/portrait branches; remove fixed heights, portrait widths, and layout-specific drop-zone ratios. BrandLogo has no intrinsic ratio; uploader retains its existing reserved square frame for that preset. Short frames hide decorative upload icons while retaining visible labels/hints. User clarification supersedes Figma placeholder height; no new parameter. `ShopTextField` already binds immediately; heading needs no shared-field change. `Components/Admin/FeaturedTileEditor` serves two actual fixed-position call sites. No general page/layout wrappers.
6. **Page-owned sticky behavior.** Add `wwwroot/js/featuredEditor.js`, scoped to editor root and `.shop-featured-settings`. Port only offset measurement from `productDetail.js`; do not import its gallery/variant callbacks or change product detail. JS sets dynamic `--shop-featured-sticky-top`; SCSS owns all static geometry. Desktop offset is `min(0, scrollRoot.clientHeight - panel.getBoundingClientRect().height)`, with panel height including 24px bottom padding. Rejected fixed positioning, nested vertical scrolling, and freezing before panel content is reachable.
7. **Heading/default override.** Add model initializes Title from `Strings.Homepage_Featured`. Heading and `PageTitle` use `string.Format(Featured_AddHeading/Featured_EditHeading, trimmed Title or Homepage_Featured)`. Immediate binding updates heading while typing; blank fallback never mutates required input. Other Add text/images remain empty.

### Figma input and component mappings

**File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56957&t=cWBtc9BODDtj4EUK-11

Inspected through MCP: section `3185:56957`; Edit desktop/tablet/phone `3169:36840`, `3169:42518`, `3169:43241`; Add `3218:63016`, `3218:63464`, `3218:63886`; skeleton `3194:33156`, `3194:33595`, `3194:34008`. Re-fetch nodes during Execute.

| Figma source / instance | Existing source/API | Decision |
|---|---|---|
| Upload placeholder `3013:17941`; Add instance `I3218:63017;2985:16708;3218:63029` | `ShopImageUpload`, composing `ShopFileDropzone` | `Multiple=false`, `Layout=SinglePreview`, `MaxFileSize=5242880`, `ValidateFileAsync`; resource upload hint says 5 MB. |
| Image Upload Tile `3016:20008`; Edit `I3169:36841;2985:16708;3176:52795` | `ShopImageUpload`, `ShopImageTile`, `ShopImage` | `PreviewPreset=SquareContain`, `ResponsivePreview=false`; removable, no primary badge. Existing single-preview mode preserves prior artwork after rejected replacement. |
| Text Field `2732:18856`; tile Label `...;3169:37143`, Section Title `...;3169:37067` | `ShopTextField : InputBase<string?>` | Label, immediate binding, stable unique IDs, owned field errors. No placeholder parameter or duplicate page error markup. |
| Chip selected `2036:10249`, default `180:518` | `ShopChip` | Medium; complementary Selected values; fieldset/legend; disabled during Save. |
| Save `170:2625`; Cancel `170:2656`; Replace `170:2647` | `ShopButton` | Large Filled submit + `ShopIcons.Outlined.Arrow_Right_MD`; Large Outlined navigation; existing Medium Outlined replacement. |
| Remove `2947:17027`, icon `151:1926`; helper `151:1422` | Existing upload removal; `ShopIcon`, `ShopIcons.Outlined.Info` | Use trusted registry icons and tile-specific accessible removal names. |
| Skeleton frames above | `ShopSkeleton` | Inline page skeleton; Rectangle/Text; same responsive containers; no editable skeleton fields. |
| MainLayout/chrome/breadcrumbs | Existing MainLayout and `BreadcrumbState` | Reuse unchanged; editor owns body only. |

Instance overrides: two distinct image/text sets; no primary badge; replace/removal enabled; Visible selected; Hidden unselected. Figma image hashes `b0af2d3b9907c00626de05b49d25c0a99736cf44` and `cb1db61616c70785ea58485294beca524635611f` are sample artwork, not shipped defaults. Staff supplies content; no bitmap export needed. Existing fonts/icons/tokens supply remaining assets.

Consumer width ownership: Hero Banner mobile upload retains its Figma 216px maximum through a wrapper in ManageHeroBanner and its page SCSS (frame 3176:51656). Drop zone and preview retain shared PortraitCover 4:5 geometry. No portrait-width exception inside ShopImageUpload.

Layout in `Styles/layouts/_featured.scss`, loaded by `TheShop.scss`: desktop outer 2:1 columns, 32px gap; two left tiles equal width with 24px gap, 16px card padding/internal gap. Below 1024px, Section follows tiles with 48px gap. Below 768px, tiles stack, Section gap 32px, page gutters 16px. Existing breakpoints module owns thresholds. Reuse typography tokens: heading h2 desktop/tablet, h4 phone; group h5, tile subtitle-2, Status subtitle-1, helper caption. Status uses automatic line height per Figma. Long titles wrap. Component appearance stays in existing component partials; no copied field/button styles.

## 6. Core Functional Flow

### Flow 1: Open management

`ManageHomepage` links through new `Routes.Admin.ManageFeatured = "/admin/homepage/featured"`. Partial `ManageFeatured` has `[Route(...)]`, `[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]`, and existing admin import policy. Add `BusyKeys.Homepage.LoadFeatured = "homepage.featured.load"` and `SaveFeatured = "homepage.featured.save"`. `BusyState.RunAsync(BusyKeys.Homepage.LoadFeatured, ...)` sends read query. Null means confirmed absence; errors never select Add. Handler signs both private images and returns DTO. View-only renders data without mutation controls; absent view-only shows explanatory text.

### Flow 2: Prepare tile content

`FeaturedFormModel` owns fixed `Tile1`/`Tile2` instances of `FeaturedTileFormModel`; each owns text and `IReadOnlyList<ShopUploadedImage> Images`. Uploader validates through `ValidateFeaturedImageQuery`; `FeaturedImageValidation` uses shared inspector before accepting preview. Browser cancel/rejection preserves previous accepted image. FilesChanged notifies correct nested field. No storage write until Save.

### Flow 3: Configure section

`ShopTextField` binds Section Title with immediate heading recomputation. `ShopChip` updates bool status and notifies EditContext. Draft state stays local. Use `EditForm OnSubmit` with `novalidate` and `ShopCommandValidation`; no DataAnnotations, page-owned business validation, `EditContext.Validate()`, or direct validator calls.

### Flow 4: Scroll

After loaded editor renders, mount module against actual `.shop-main-scroll` ancestor and settings panel. Desktop SCSS uses `position:sticky`, `align-self:start`, calculated inset and `padding-block-end:var(--shop-space-6)`. Grid/form ancestor bounds release panel before footer. Observe panel and scroll root with ResizeObserver; listen to desktop media-query changes and window resize. Error/notification-driven panel growth triggers fresh measurement after rendering. Clear dynamic offset below desktop. Preserve native focus scrolling and reverse scrolling. Re-measure newly mounted panel after reload replaces editor content; disconnect old observers before rebinding. Dispose listeners, observer, and JS reference on navigation. No new scroll container.

### Flow 5: Save/cancel

Guard permission, repeated Save, and required-reload state; run with `BusyKeys.Homepage.SaveFeatured`. Await both upload controls' `WaitForPendingFilesAsync` before constructing normalized command. Clear old errors. Validator checks input; handler reads current row, detects absent Edit/duplicate Add, uploads replacements, constructs complete aggregate, and calls Add/Update RPC. Success notifies `Featured_Saved`, then navigates to `Routes.Admin.ManageHomepage`. Field failures go to bridge; other failures use `IShopNotificationService`. Unknown commit outcome preserves draft and offers reload before another Save. Cancel navigates without command or upload.

FieldMap: `Tile1.NewImage` maps to `Tile1.Images`, `Tile2.NewImage` to `Tile2.Images`; other nested text paths match model names. Readiness root: `data-testid="featured-admin"`, `data-state="loading|add|edit|absent|load-error"`. Add/edit state published only after model hydration. Stable selectors: `featured-title`, `featured-tile-1`, `featured-tile-2`, `featured-settings`, `featured-save`, `featured-cancel`, `featured-reload`; each tile gets scoped `featured-image`, `featured-label`, `featured-tile-title`, `featured-button-label`, `featured-button-link`.

## 7. Development Plan

### Step 1 — Domain

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add FeaturedSection, FeaturedTile, four exceptions, and `FeaturedSectionTests`/`FeaturedTileTests`. Cover required titles/images, paired buttons, optional labels/buttons, complete replacement, preserved state after rejected update, fixed positions, and Hidden persistence.

**Completion gate:** Domain builds/tests pass; public API reported; no outer-layer dependency.

### Step 2 — Application

**Depends on:** TASK-001 API.

- [ ] **TASK-002** — Add DTOs, IFeaturedRepository, read/image-validation queries and handlers/validators, FeaturedErrorKeys, square image validation, and `StorageArea.FeaturedImages`. Add focused read/inspection/authorization tests.
- [ ] **TASK-003** — Add Create/Update command folders, validators, FeaturedWrite coordination, and handler/validation tests. Freeze normalization, nested field paths, current-image retention, permissions, atomic commit, last-successful-Save semantics, and unknown-outcome behavior.

**Completion gate:** Application builds/tests pass; every expected failure has Section 9 key; no SDK/Web dependency.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| FeaturedDto, FeaturedTileDto, FeaturedImageDto, read query | Application | Web | Stable |
| IFeaturedRepository and StorageArea.FeaturedImages | Application | Infrastructure | Stable |
| FeaturedTileInput, Create/Update commands, Result/field paths | Application | Web | Stable |
| ValidateFeaturedImageQuery, existing IHeroBannerImageInspector | Application | Web/Infrastructure | Stable |

Do not start downstream consumers until their rows are Stable. Signatures above are freeze target.

### Step 4 — Infrastructure

**Depends on:** Application checkpoint.

- [ ] **TASK-004** — Add `supabase/migrations/0037_manage_featured.sql` containing Section 10 SQL. Add `FeaturedSchemaTests` with real PostgreSQL roles: permission matrix, singleton conflict, atomic writes, required fields/pairs, unavailable image, no anonymous read, no object overwrite/delete. Run locally; apply through established migration tooling during authorized Execute. Recheck sequence before creating migration filename; never overwrite another migration.
- [ ] **TASK-005** — Add FeaturedSectionRecord, FeaturedSectionMapper, SupabaseFeaturedRepository; register repository in Infrastructure DI and private area in SupabaseFileStorage. Reuse inspector registration unchanged. Test RPC parameter mapping, signed previews, provider error translation, immutable key area, and cancellation.

**Completion gate:** SQL applies cleanly to local test database; RLS and RPC tests pass; adapter fulfills frozen contract; no existing Homepage data/policies changed.

### Step 5 — Web

**Depends on:** Application checkpoint and completed Infrastructure work.

- [ ] **TASK-006** — Add `Pages/Admin/ManageFeatured.razor`/`.razor.cs`, FeaturedFormModel, FeaturedTileFormModel, and repeated `Components/Admin/FeaturedTileEditor` composition. Wire mapped controls, read-only/absent/loading/error states, route, Homepage entry, BusyKeys, resource inventory, nested validation, dynamic heading, Save/Cancel/reload.
- [ ] **TASK-007** — Add `_featured.scss` and `featuredEditor.js`; implement Figma layouts and bounded tall/short sidebar behavior without shared product detail changes. Recalculate after error-height changes, zoom/viewport resize, and panel remount; disconnect obsolete observers. Preserve disposal, keyboard reachability, and reverse scrolling.
- [ ] **TASK-008** — Add `ManageFeaturedTests` and `FeaturedTileEditorTests`: defaults, live headings/fallback, saved hydration, read-only controls, field mapping/clearing, uploader parameters/callbacks, repeated Save guard, failure retention, Cancel, and lifecycle cleanup.

**Completion gate:** Web build/bUnit/design-rule checks pass; supported existing controls reused; resources resolve; no vendor assets, literal routes, hardcoded display strings, or page-owned validator rules. User owns manual appearance review.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4–5.

- [ ] **TASK-009** — Add ManageFeaturedPage and admin/view-only/anonymous/customer journeys under existing E2E projects. Use existing AdminEmail/SupportEmail/CustomerEmail personas. Isolate/reset only Featured fixtures through service credentials in test setup. Generate distinct 1200×1200 PNG/JPG/WebP fixtures, exact/over-5-MB valid files, corrupt/non-square/undersized files, complete/blank button pairs, and Hidden configuration. Delay/fail specific requests for loading/error proof. Verify AC-16–19 with real scroll-root geometry and keyboard focus, including 320/390/834/1440 widths, 768/1024 boundaries, short viewport, changed error height, reverse scrolling, and editor/footer containment. Geometry assertions test behavior only; no screenshot comparison. Keep test-only height stress scoped to isolated layout tests and also test natural editor layout.
- [ ] **TASK-010** — Build `TheShop.slnx`; run focused tests with `[Trait("Feature", "005_homepage-featured")]`, preserve existing Hero Banner/Fresh Drops tests for shared dependencies, run design-rule check, and run `graphify update .` after code changes. Record test/AC evidence for later Test/Verify stages. Do not auto-invoke later `$theshop-*` skills.

**Completion gate:** all 20 ACs have behavior/test evidence; focused tests and relevant shared regressions pass. Later Test/Verify stages produce required manifests through their canonical workflows. Explicit visual acceptance remains user confirmation.

### Deviation procedure

Accept only behavior-preserving adjustments matching constitution and existing code. Reject scope growth, weaker authorization/integrity, unrelated refactors, or silent contract changes. Contract change: stop dependent work, update contract rows and affected tasks, re-freeze before resuming. Report database/environment blockers without substituting mocks for required RLS/browser proof.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1 Add defaults/live heading | TASK-006, TASK-008, TASK-009 |
| AC-2 Edit hydration/live heading | TASK-002, TASK-006, TASK-008, TASK-009 |
| AC-3 Guest/denied access | TASK-002, TASK-004, TASK-006, TASK-009 |
| AC-4 View-only and direct denial | TASK-003, TASK-004, TASK-006, TASK-008, TASK-009 |
| AC-5 Draft image operations | TASK-002, TASK-006, TASK-008, TASK-009 |
| AC-6 Valid images/guidance | TASK-002, TASK-006, TASK-009 |
| AC-7 Image limits/rejections | TASK-002, TASK-003, TASK-009 |
| AC-8 Required fields/fallback | TASK-001, TASK-003, TASK-006, TASK-008, TASK-009 |
| AC-9 Button pair | TASK-001, TASK-003, TASK-004, TASK-008, TASK-009 |
| AC-10 Destinations | TASK-003, TASK-005, TASK-009 |
| AC-11 Atomic Save/status | TASK-001, TASK-003, TASK-004, TASK-005, TASK-009 |
| AC-12 Cancel | TASK-006, TASK-008, TASK-009 |
| AC-13 Conflict/repeated Save | TASK-003, TASK-004, TASK-005, TASK-008, TASK-009 |
| AC-14 Loading/failure | TASK-002, TASK-006, TASK-008, TASK-009 |
| AC-15 Failure/permission loss | TASK-003, TASK-004, TASK-005, TASK-008, TASK-009 |
| AC-16 Tall panel stop | TASK-007, TASK-009 |
| AC-17 Short panel stop | TASK-007, TASK-009 |
| AC-18 Reverse/resize/zoom/focus | TASK-007, TASK-008, TASK-009 |
| AC-19 Responsive scrolling | TASK-006, TASK-007, TASK-009 |
| AC-20 Accessibility/resources | TASK-006, TASK-008, TASK-009, TASK-010 |

## 9. Validation & Error Handling Strategy

### Validators and invariant ownership

Create/Update validators normalize through request construction; no validator mutation. Title nonempty; both nested inputs nonnull; per-tile Title nonempty; optional paired button fields; existing `HeroBannerInput.IsDestination`. Create requires NewImage for both tiles. Update rejects RemoveImage without NewImage. Every supplied NewImage passes asynchronous FeaturedImageValidation: nonempty bytes, at most 5,242,880, allowed MIME matching detected bytes, decodable, width=height, width>=1200. Selection query uses same validation. Pipeline authorization precedes inspection/uploads.

Domain factories enforce nonblank titles/images and button pairing independently. Section construction occurs after valid tile construction; update is atomic in memory. Handlers map DomainException.MessageKey; no Web reference. `FeaturedErrorKeys` follows existing Homepage constant-catalogue pattern; domain invariant entries alias exception MessageResourceKey. Resource coverage tests verify every returned key exists in `Strings.resx`.

| Spec rule / edge | Owner and outcome |
|---|---|
| RULE-1; guest, denied, permission loss | Existing route denial/sign-in, request attributes, RLS/RPC/Storage; `RbacErrorKeys.AccessDenied`; no read/mutation leak. |
| RULE-2; absent/Hidden/concurrent Add | Null read selects Add only after successful load; SQL singleton; `23505` maps AlreadyExists; no upsert. Hidden stays Edit. |
| RULE-3; removed image, empty titles/labels | Validators emit exact nested paths; domain repeats invariants; SQL required columns/checks; Section Title fallback affects display only. |
| RULE-4; invalid/corrupt/large image, cancelled/rejected replacement | Shared inspector plus Featured validation; invalid selection returns local error without changing accepted Files; browser cancel does nothing. |
| RULE-5; missing button counterpart | Nested validator, tile factory, SQL pair checks; field guidance. Blank pair normalized to null. |
| RULE-6; malformed or unavailable target | Existing parser; reject invalid syntax. Never verify target availability; no navigation from editor. |
| RULE-7; invalid status, failed/partial upload, repeated Save | Bool contract/SQL NOT NULL; complete row RPC; draft retained; BusyState single dispatch. Upload failure never calls commit. |
| RULE-8; tall/short panel, resize/errors/zoom/reverse/end/mobile | Page JS/SCSS and browser geometry tests; no Result key; adjust/clear offset and preserve native focus scrolling. |
| Load failure | Query catches expected denial separately; unexpected read/signing failure returns LoadFailed; retry, never false absence. |
| Definite Save failure | SQL `42501`: denial; `23505`: AlreadyExists; `P0002`: NotFound; `22023`: ImageUnavailable; `23502/23514`: SaveFailed. Preserve draft. |
| Unknown write outcome | Transport/unrecognized provider failure maps SaveUnconfirmed; retain uploads and draft, no success notification, block blind repeat until explicit reload reconciles saved state. |

### Resource inventory

Add only to `Web/Resources/Strings.resx`; generated accessor remains build-owned. Static UI uses `Strings.Key`; returned keys use runtime Localizer. Reuse existing Save, Cancel, Loading, image replacement, and access-denied resources.

| Key | English text |
|---|---|
| `Homepage_Featured` | Featured |
| `Featured_AddHeading` | Add {0} |
| `Featured_EditHeading` | Edit {0} |
| `Featured_Tiles` | Tiles |
| `Featured_Tile` | Tile {0} |
| `Featured_Section` | Section |
| `Featured_Title` | Title |
| `Featured_Label` | Label |
| `Featured_ButtonLabel` | Button label |
| `Featured_ButtonLink` | Button link |
| `Featured_Status` | Status |
| `Featured_Visible` | Visible |
| `Featured_Hidden` | Hidden |
| `Featured_RemoveImage` | Remove image from Tile {0} |
| `Featured_UploadHint` | Drag and drop, or click to browse. PNG, JPG or WebP up to 5 MB. |
| `Featured_ImageHint` | Square images, at least 1200 × 1200. Show products only, no people or lifestyle scenes. Text sits on the bottom-left. |
| `Featured_Absent` | No Featured configuration exists. |
| `Featured_Reload` | Reload Featured |
| `Featured_Saved` | Featured saved. |
| `Featured_SectionTitleRequired` | Enter a section title. |
| `Featured_TileTitleRequired` | Enter a tile title. |
| `Featured_ImageRequired` | Add an image for this tile. |
| `Featured_ButtonPairRequired` | Enter both button label and button link, or leave both blank. |
| `Featured_ButtonLinkInvalid` | Enter a site-relative path starting with / or an HTTP/HTTPS URL. |
| `Featured_ImageInvalidType` | Choose a valid PNG, JPG or WebP image. |
| `Featured_ImageTooLarge` | Choose an image no larger than 5 MB. |
| `Featured_ImageUnreadable` | This image cannot be read. Choose another image. |
| `Featured_ImageDimensionsInvalid` | Choose a square image at least 1200 × 1200. |
| `Featured_AlreadyExists` | Featured already exists. Reload to edit it. |
| `Featured_NotFound` | Featured no longer exists. Reload to continue. |
| `Featured_ImageUnavailable` | A selected image is unavailable. Reload or choose another image. |
| `Featured_LoadFailed` | Featured could not be loaded. Retry. |
| `Featured_UploadFailed` | Images could not be uploaded. Your changes remain available. |
| `Featured_SaveFailed` | Featured could not be saved. Your changes remain available. |
| `Featured_SaveUnconfirmed` | Save could not be confirmed. Reload before saving again. |

## 10. Database Schema & RLS Policies

Additive migration `0037_manage_featured.sql`. Existing Homepage permissions already seeded; no role changes. Apply literal SQL below. No live database mutation during Plan. No initial Featured row or sample artwork seeded.

### Schema, private storage, and RLS

```sql
BEGIN;

CREATE TABLE public.featured_sections (
  id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  title text NOT NULL CHECK (length(btrim(title)) > 0),
  tile1_label text,
  tile1_title text NOT NULL CHECK (length(btrim(tile1_title)) > 0),
  tile1_button_label text,
  tile1_button_link text,
  tile1_image_key text NOT NULL CHECK (
    tile1_image_key ~ '^featured/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
  tile2_label text,
  tile2_title text NOT NULL CHECK (length(btrim(tile2_title)) > 0),
  tile2_button_label text,
  tile2_button_link text,
  tile2_image_key text NOT NULL CHECK (
    tile2_image_key ~ '^featured/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
  is_visible boolean NOT NULL DEFAULT true,
  CONSTRAINT featured_tile1_button_pair CHECK (
    (tile1_button_label IS NULL AND tile1_button_link IS NULL) OR
    (tile1_button_label IS NOT NULL AND length(btrim(tile1_button_label)) > 0
      AND tile1_button_link IS NOT NULL AND length(btrim(tile1_button_link)) > 0)),
  CONSTRAINT featured_tile2_button_pair CHECK (
    (tile2_button_label IS NULL AND tile2_button_link IS NULL) OR
    (tile2_button_label IS NOT NULL AND length(btrim(tile2_button_label)) > 0
      AND tile2_button_link IS NOT NULL AND length(btrim(tile2_button_link)) > 0))
);

REVOKE ALL ON public.featured_sections FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.featured_sections TO authenticated;
ALTER TABLE public.featured_sections ENABLE ROW LEVEL SECURITY;
CREATE POLICY featured_read ON public.featured_sections
  FOR SELECT TO authenticated
  USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY featured_create ON public.featured_sections
  FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));
CREATE POLICY featured_edit ON public.featured_sections
  FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
  WITH CHECK ((SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

INSERT INTO storage.buckets
  (id, name, public, file_size_limit, allowed_mime_types)
VALUES ('featured-images', 'featured-images', false, 5242880,
  ARRAY['image/png', 'image/jpeg', 'image/webp']);

CREATE POLICY featured_images_read ON storage.objects
  FOR SELECT TO authenticated USING (
    bucket_id = 'featured-images' AND
    (SELECT public.authorize('homepage.view')));
CREATE POLICY featured_images_create ON storage.objects
  FOR INSERT TO authenticated WITH CHECK (
    bucket_id = 'featured-images' AND
    name ~ '^featured/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    (SELECT public.authorize('homepage.view')) AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))));
-- No UPDATE/DELETE policies for Featured objects; uploads are immutable.

CREATE FUNCTION public.featured_require_images()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM storage.objects o
      WHERE o.bucket_id = 'featured-images' AND o.name = NEW.tile1_image_key)
     OR NOT EXISTS (SELECT 1 FROM storage.objects o
      WHERE o.bucket_id = 'featured-images' AND o.name = NEW.tile2_image_key) THEN
    RAISE EXCEPTION 'image unavailable' USING ERRCODE = '22023';
  END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER featured_images_required
  BEFORE INSERT OR UPDATE ON public.featured_sections
  FOR EACH ROW EXECUTE FUNCTION public.featured_require_images();

CREATE FUNCTION public.get_featured_admin()
RETURNS SETOF public.featured_sections
LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT f.* FROM public.featured_sections f WHERE f.id = 1;
END;
$$;

CREATE FUNCTION public.save_featured_admin(
  p_create boolean, p_title text,
  p_tile1_label text, p_tile1_title text, p_tile1_button_label text,
  p_tile1_button_link text, p_tile1_image_key text,
  p_tile2_label text, p_tile2_title text, p_tile2_button_label text,
  p_tile2_button_link text, p_tile2_image_key text, p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
    (p_create IS TRUE AND NOT public.authorize('homepage.create')) OR
    (p_create IS FALSE AND NOT public.authorize('homepage.edit')) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.featured_sections
      (id, title, tile1_label, tile1_title, tile1_button_label, tile1_button_link,
       tile1_image_key, tile2_label, tile2_title, tile2_button_label,
       tile2_button_link, tile2_image_key, is_visible)
    VALUES (1, p_title, p_tile1_label, p_tile1_title, p_tile1_button_label,
      p_tile1_button_link, p_tile1_image_key, p_tile2_label, p_tile2_title,
      p_tile2_button_label, p_tile2_button_link, p_tile2_image_key, p_is_visible);
  ELSE
    UPDATE public.featured_sections SET title = p_title,
      tile1_label = p_tile1_label, tile1_title = p_tile1_title,
      tile1_button_label = p_tile1_button_label,
      tile1_button_link = p_tile1_button_link, tile1_image_key = p_tile1_image_key,
      tile2_label = p_tile2_label, tile2_title = p_tile2_title,
      tile2_button_label = p_tile2_button_label,
      tile2_button_link = p_tile2_button_link, tile2_image_key = p_tile2_image_key,
      is_visible = p_is_visible WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN
      RAISE EXCEPTION 'featured not found' USING ERRCODE = 'P0002';
    END IF;
  END IF;
END;
$$;

REVOKE ALL ON FUNCTION public.featured_require_images() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.get_featured_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_featured_admin(
  boolean, text, text, text, text, text, text,
  text, text, text, text, text, boolean) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_featured_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_featured_admin(
  boolean, text, text, text, text, text, text,
  text, text, text, text, text, boolean) TO authenticated;

COMMIT;
```

Database protects access, existence, fixed positions, required fields, pairs, and atomicity even for direct table writes. Browser decoder/Application pipeline enforces image bytes/geometry and destination syntax; no server decoder introduced. Bucket enforces declared MIME/byte size independently. This distinction is explicit in Section 11.

Rollback: remove Featured navigation/route deployment first; retain additive table/bucket and saved data for forward repair. Do not drop data or delete uploaded objects as routine rollback. Migration application and RLS proof occur during Execute/Test, not Plan.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** Private uploads remain after replacement, partial upload, or unknown commit outcome. User accepts storage growth to avoid deleting possibly committed artwork. TASK-003/005 retain immutable files; cleanup remains outside scope.
- **⚠️ Risk — ✅ Accepted:** Authorized direct API callers can bypass editor image geometry/decoding and destination checks. User accepts existing editor validation for this administration-only feature. TASK-002/003 validate normal app flow; TASK-004 retains independent server permissions, row/reference constraints, and bucket MIME/size limits.

---
**Status:** Resolved · **Spec:** `.specs/005_homepage-featured/spec.md` · **Created:** 2026-11-01 · **Resolved:** 2026-11-01
