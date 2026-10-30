# Implementation Plan — Manage Hero Banner

**Feature:** `003_manage-hero-banner`

Companion: `.specs/003_manage-hero-banner/spec.md` (Confirmed). Implementation progress and local migration evidence: `execute.md`.

## 1. Objective

Deliver permission-gated banner administration: one persisted banner, two artwork slots, plain content and destination text, Visible/Hidden, Save/Cancel. Limit each image to 5,242,880 bytes. Storefront rendering and shopper navigation remain excluded.

## 2. Tech Stack

- **Domain:** pure C# entity, exceptions, permission catalogue.
- **Application:** existing MediatR authorization/validation pipeline, FluentValidation, `Result`/`Result<T>`, immutable records.
- **Infrastructure:** existing Supabase 1.1.1 client, PostgreSQL/RLS, private Storage bucket, browser image-inspection adapter through existing JS interop dependency. No new image-processing package.
- **Web:** .NET 10 Blazor WebAssembly, native HTML, existing Shop controls, owned SCSS/tokens, English `Strings.resx`.
- **Proof:** existing xUnit/NSubstitute, PostgreSQL Testcontainers, bUnit, Playwright and `.sdd/scripts/capture-ui.ps1`.

## 3. High-level Architecture

`ManageHeroBanner.razor` dispatches requests through `IMediator.Send`. Authorization precedes validators. Handlers coordinate `HeroBanner`, its feature-owned repository, shared `IFileStorage`, and immutable responses. Supabase RPCs explicitly reject missing live permissions; RLS also protects direct table/object operations.

```text
ManageHeroBanner / EditForm
  IMediator.Send(CreateHeroBannerCommand | UpdateHeroBannerCommand)
  ExceptionHandlingBehavior / AuthorizationBehavior / ValidationBehavior
  CreateHeroBannerHandler | UpdateHeroBannerHandler
    IHeroBannerRepository.GetAsync
    IFileStorage.UploadAsync(StorageArea.HeroBannerImages, authenticatedUserId, ...)
    HeroBanner.Create | ChangeContent | ChangeImages | SetVisibility
    IHeroBannerRepository.AddAsync | UpdateAsync
  Result -> notification; successful save navigates to Manage Homepage
```

Existing anchors inspected: `Features/Categories/Commands/UpdateCategory`, `CategoryForm`, `ShopImageUpload`, `IFileStorage`, `SupabaseFileStorage`, `AdminDashboardCatalogue`, `Routes.Admin`, `BusyKeys`, `BreadcrumbTrail`. Graph query located these reuse points; source inspection resolved details absent from truncated graph output.

## 4. Data Model

### Domain entities & value objects

- New `Domain/Entities/HeroBanner.cs`: singleton identity `short` value 1; Heading, optional Description/ButtonLabel/ButtonLink, required DesktopImageKey, optional MobileImageKey, IsVisible. Methods `Create`, `Rehydrate`, `ChangeContent`, `ChangeImages`, `SetVisibility`. Domain treats destination as opaque content, never navigates.
- Invariants: heading/desktop key nonblank; button label/link paired. Exceptions `HeroBannerHeadingRequiredException`, `HeroBannerDesktopImageRequiredException`, `HeroBannerButtonPairRequiredException`, each with `MessageResourceKey`, following existing category exceptions. No persistence, image decoder, URLs-to-navigate, or resource assembly dependency.
- Extend `PermissionCatalogue.Homepage`: `ViewCode`, `CreateCode`, `EditCode` compile-time constants; corresponding `Permission` values; append three entries to `All`. No Delete permission.

### DTOs and stable Application contracts

Place single-feature interfaces in `Application/Features/Homepage/Interfaces/`; records in `DTOs/`.

| Contract | Shape |
|---|---|
| `HeroBannerDto` | Heading, nullable Description/ButtonLabel/ButtonLink, IsVisible, DesktopImage, nullable MobileImage |
| `HeroBannerImageDto` | ObjectKey, PreviewUrl; preview URL is short-lived, never persisted |
| `HeroBannerImageUpload` | byte[] Content, string FileName, string ContentType |
| `HeroBannerImageRole` | Desktop / Mobile enum in Application |
| `HeroBannerImageInspection` | detected content type, decoded width/height, decodable flag; provider-neutral |
| `GetHeroBannerForAdminQuery` | `IRequest<Result<HeroBannerDto?>>`; successful null alone means absent |
| `ValidateHeroBannerImageQuery` | ImageRole, Image; `IRequest<Result>`; validates candidate before replacing preview |
| `CreateHeroBannerCommand` | Heading, Description?, ButtonLabel?, ButtonLink?, DesktopImage, MobileImage?, IsVisible; `IRequest<Result>` |
| `UpdateHeroBannerCommand` | same text/status; NewDesktopImage?, NewMobileImage?, RemoveDesktopImage, RemoveMobileImage; `IRequest<Result>` |
| `IHeroBannerRepository` | `Task<HeroBanner?> GetAsync(ct)`; `Task<HeroBanner?> GetForWriteAsync(bool create, ct)`; `Task<Result> AddAsync(HeroBanner, ct)`; `Task<Result> UpdateAsync(HeroBanner, ct)` |
| `IFileStorage` (shared) | Existing `UploadAsync(area, ownerId, stream, fileName, contentType, ct)` and `DeleteAsync(area, key, ct)`; add `Task<string> CreateSignedUrlAsync(StorageArea area, string objectKey, int expiresInSeconds, CancellationToken ct)` |
| `IHeroBannerImageInspector` | `Task<HeroBannerImageInspection> InspectAsync(byte[] content, ct)` |

Every async boundary takes `CancellationToken`. Write handlers derive owner from `ICurrentUserService.Id`, never from request input; Storage RLS enforces authenticated ownership. Application selects canonical extension from validated content type before passing a filename to shared storage. Both image slots use the same upload record and independent immutable object keys. Add `StorageArea.HeroBannerImages`, mapped to private `hero-banner-images` with prefix `hero`. `GetPublicUrl` rejects private areas.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `public.hero_banners` (new) | Single admin configuration | checked singleton id; content; image keys; visibility |
| `public.permissions`, `role_permissions` (seed additions) | Homepage permissions/grants | three codes; existing role IDs |
| `storage.buckets`, `storage.objects` | Private banner artwork | `hero-banner-images`; `hero/{userId}/{randomId}.{ext}` |

### Indexes

Singleton primary key answers every banner query; no secondary index. Existing Storage bucket/name index handles object lookup.

## 5. Core Design Decisions

1. **One route chooses Add/Edit from successful read.** New `Routes.Admin.ManageHeroBanner = "/admin/homepage/hero-banner"`; no separate add/edit routes or banner ID input. Reject second creation through singleton primary key, never upsert. Why: RULE-2 and confirmed singleton scope. Rejected: carousel/list infrastructure.
2. **One permission per action, matching Brands.** Keep existing single-use `RequiresPermissionAttribute` and `AuthorizationBehavior`. Page routes and management queries require Homepage View; Create requires only Create; Update requires only Edit. `GetForWriteAsync(create, ct)` loads save preparation through an action-gated RPC, without a hidden View prerequisite. RLS permits writer reads needed by PostgreSQL UPDATE and immutable-image existence checks; these grants do not grant management page access. Confirmed default grants: Admin/SuperAdmin receive View/Create/Edit; Support receives View only; Customer receives none. Seed through migration in Section 10; runtime checks remain permission-based. Use catalogue constants in attributes. Existing `_Imports.razor` supplies authentication only; do not resurrect obsolete `AdminArea` policy. Rejected: handler checks after validation, role-name checks, or broader dashboard access as banner prerequisite. Rules 31–32; RULE-1.
3. **Private artwork through shared storage.** User-directed revision: extend `IFileStorage` and `SupabaseFileStorage` with signed URL support and `StorageArea.HeroBannerImages`; remove the banner-specific storage adapter/interface. Existing public-area callers retain current upload/delete/public URL contracts. Signed previews last 900 seconds and renew on reload/retry. Public URL lookup rejects the private area. Application derives authenticated ownership and canonical image extension; existing RLS guards cleanup of committed objects. Accepted: issued previews remain usable until expiry, including after permission revocation. Rejected: public artwork bucket or duplicated storage plumbing.
4. **Application owns input rules, including image geometry.** Shared upload component retains bounded browser reads; `ValidateHeroBannerImageQueryValidator` and shared feature image validator enforce bytes, actual format, successful decode and dimensions. `BrowserHeroBannerImageInspector` in Infrastructure implements image inspection through `wwwroot/js/heroBannerImages.js`, written during Web step. Browser decode does not introduce a server image-processing service. Full commands revalidate supplied bytes on submission. Accepted boundary: permitted direct callers can bypass browser dimension/content validation; Storage still enforces permissions and 5 MB limit. No trusted server decoder added for current admin scope. Rejected: trusting filename/MIME/dimensions supplied by page, hand-written codec parser, or duplicated Razor validation.
5. **Atomic configuration save; staged immutable objects.** Keep selected bytes local until Save. Upload candidates first; write all text/status/keys in one RPC transaction. Definite pre-commit failures compensate only newly uploaded, unreferenced objects. Accepted: keep previously committed/replaced artwork privately; interrupted uploads may also remain private. No automatic cleanup or background garbage collector in this slice. Never delete candidate objects after uncertain write outcome until reload establishes references. Why: RULE-7 and concurrent saves must not destroy stored images. Rejected: deleting outgoing artwork before commit, storage overwrite, or DB upsert.
6. **Successful save returns `Result`, not preview DTO.** After success, show Saved then navigate to Manage Homepage. Reopening the editor loads fresh signed previews. Concurrent existing edits use last successful write; unique constraint settles concurrent creation. Rejected: silent overwrite through retrying Add as Update or optimistic-lock product behavior absent from spec.
7. **Reuse controls through explicit capability additions.** Existing upload grid and tile are square; `ShopImage.Hero` changes ratio on phones. Add opt-in single-preview layout and static preview support as specified in Step 5; preserve existing defaults. Description needs multiline `ShopTextField` support. Confirmed mitigation: banner opts into new capabilities; existing Product/Brand/Category callers retain square/grid behavior, binding, file errors and URL cleanup. TASK-017/018 regression checks cover these callers and existing single-permission authorization; missing required permission must still stop dispatch before validation. Rejected: page CSS overriding control internals, duplicate text inputs, or new broad UI suite. Constitution Rules 14, 25, 35.
8. **Homepage first; temporary parent page.** Confirmed: place Homepage card first among visible Admin Console modules; preserve existing modules' relative order. Its Manage Homepage link navigates to `Routes.Admin.ManageHomepage = "/admin/homepage"`. Create temporary Manage Homepage page with one Hero Banner navigation button targeting `Routes.Admin.ManageHeroBanner`; gate page/button on Homepage View. Cancel from banner editor returns here. Reuse existing `AdminModule`/catalogue/card, counting saved banner as 0/1 (including Hidden). Apply supplied Admin Console design to Homepage card; do not add other modules shown only in design. Existing dashboard permission still applies only to `/admin`; direct Homepage routes need only Homepage permissions. Rejected: placeholder Cancel target, building unrelated Homepage editors, or granting dashboard permission implicitly.

## 6. Core Functional Flow

### Flow 1: Open management

1. Route authentication and `[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]` gate parent/editor. Breadcrumbs use existing `BreadcrumbState`/`BreadcrumbTrail.Admin()` with Homepage parent.
2. `BusyState.RunAsync(BusyKeys.Homepage.LoadHeroBanner, ...)` sends read query. New keys: `LoadHeroBanner = "homepage.hero.load"`, `SaveHeroBanner = "homepage.hero.save"` under `BusyKeys.Homepage`. Explicit SQL authorization distinguishes denial from absent row.
3. Handler maps entity and asynchronously signs image keys. Success with null shows Add/absent state; entity shows Edit/view-only. Failed load shows retry; never creates Add state.
4. Authorized View-only users see encoded content, previews and selected status as read-only content. Mutation inputs, upload/remove controls and Save are absent. Missing Create/Edit denies writes independently of rendered controls.

### Flow 2: Prepare artwork

1. Both single-slot `ShopImageUpload` instances await file preparation. Read limit is 5,242,880 bytes, including a streamed overflow check.
2. New validation hook delegates candidate to `Mediator.Send(new ValidateHeroBannerImageQuery(role, upload), ct)`. Inspector identifies PNG/JPEG/WebP bytes and attempts browser decode; validators own dimension decisions.
3. On rejection, show localized error beside matching slot; retain prior accepted selection and object URL. On acceptance, publish new controlled selection then revoke replaced local URL. No Storage operation occurs.
4. Removal updates draft only. Save awaits both `WaitForPendingFilesAsync()` tasks. Disposal cancels pending selection and revokes only owned local URLs.

### Flow 3: Configure content and navigation

1. `HeroBannerFormModel` holds plain content/status and slot editing state, without DataAnnotations or business rules. Normalize heading and optional text once in Application requests before validation; trim destination boundaries, never alter URL case/path or silently repair invalid destinations.
2. Button link remains `ShopTextField` with native `type="text"`, allowing `/products`; no picker and no actual navigation. `ShopChip.SelectedChanged` chooses exactly one boolean Visible/Hidden value inside labelled fieldset.
3. Application validators emit all field failures; `ShopCommandValidation.FieldMap` maps nested image paths to DesktopImage/MobileImage form fields. Edits clear stale errors through existing bridge.

### Flow 4: Save or cancel

1. Save guard uses `BusyKeys.Homepage.SaveHeroBanner`; clear command errors, await preparation, send Create or Update according to loaded state.
2. Authorization checks only Create or Edit before input validation. Each handler loads current entity through its action-authorized write-preparation read; Keep uses current persisted image key, Replace uses uploaded candidate, Remove clears requested slot. Missing desktop after changes returns required-field failure.
3. Handler mutates entity, uploads required candidates, calls repository RPC. Expected conflicts/missing row/denial become typed resource keys. Upload/definite save failure preserves local draft and prior saved row.
4. Successful save notifies and navigates to Manage Homepage. Add conflict exposes explicit Reload action; never discards draft silently. Read failures remain retryable; stale signing URLs renew on reload.
5. Cancel is native `ShopButton Href="@Routes.Admin.ManageHomepage"`; component disposal discards local draft. No command, upload, or delete occurs.

## 7. Development Plan

Implement in one layered session after Resolve. Test authoring belongs to explicitly invoked Test/E2E stages; task descriptions below reserve necessary proof, without authorizing later workflow stages automatically.

### Step 1 — Domain

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add `HeroBanner`, three invariant exceptions, and Homepage catalogue constants/entries under existing Domain folders. Document public contracts.

**Completion gate:** Domain builds; pure C# dependency boundary; report factory/mutation/rehydration signatures. Existing permission tests identify expected catalogue-count updates; new invariant tests reserved for TASK-017.

### Step 2 — Application

**Depends on:** TASK-001 reported API.

- [ ] **TASK-002** — Keep single-use `RequiresPermission` attributes and existing pipeline behavior. Create declares Create only; Update declares Edit only. View remains on page routes and management queries.
- [ ] **TASK-003** — Add Homepage DTOs/interfaces, `GetHeroBannerForAdminQuery`/handler and mapping, typed error-key constants. Read handler signs previews; missing row is successful null, never exception fallback.
- [ ] **TASK-004** — Add image inspection query/handler/validators plus Create/Update validators and shared feature normalization/image validation. Enforce Section 9 rules without page duplication.
- [ ] **TASK-005** — Add Create/Update requests/handlers, invariant translation, staged uploads, compensation and save outcomes. No DTO signing after committed save.
- [ ] **TASK-006** — Append `AdminModule.Homepage` enum value to preserve existing numeric identities; prepend Homepage View catalogue entry so its card appears first. Preserve existing modules' relative order and permissions.

**Completion gate:** Application builds; complete request/interface signatures; validators registered by existing assembly scan; authorization runs before validation; no Infrastructure/Web types leak inward.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| Entity API / Homepage permission codes | Domain | Application, Web, Infrastructure | Stable — Domain build/scope pass; execute.md signatures |
| Read/image queries, write requests and Result shapes from Section 4 | Application | Web | Stable — Application build/scope pass |
| Feature repository/inspector, DTOs, shared storage extension from Section 4 | Application | Infrastructure | Stable — shared-storage revision compiled; Application scope pass |
| Single-permission action contract | Application | All decorated requests | Stable — existing pipeline restored; Create/Edit each declare one code |
| `AdminModule.Homepage` | Application | Infrastructure, Web | Stable — Application build/scope pass |

Mark each row Stable after compiling/reporting signatures; Infrastructure and Web start only after dependencies are Stable. No silent contract edits after checkpoint.

### Step 4 — Infrastructure

**Depends on:** stable Application contracts.

- [ ] **TASK-007** — Add `supabase/migrations/0034_manage_hero_banner.sql` using Section 10. Check next migration number again before creation; never overwrite concurrent migration. Apply only to explicit development/local target during Execute. Preserve already-applied 0034; add 0035_hero_banner_action_permissions.sql for the user-directed single-action correction.
- [ ] **TASK-008** — Add `HeroBannerRecord`, mapper and `SupabaseHeroBannerRepository` under `Persistence/{Records,Mappers,Repositories}`. Read/save RPCs use authenticated client; map SQLSTATE outcomes in Section 9.
- [ ] **TASK-009** — Extend shared `SupabaseFileStorage` for the private HeroBannerImages area and signed preview URLs; reuse upload/delete with immutable canonical-extension keys and guarded compensation. Remove the feature-specific storage adapter/interface. Never embed service-role key in WebAssembly.
- [ ] **TASK-010** — Add `BrowserHeroBannerImageInspector` adapter; register the feature repository and inspector alongside existing shared `IFileStorage` in Infrastructure DI. Extend `SupabaseAdminDashboardRepository.CountModuleAsync` only for Homepage through `count_hero_banner_admin`; preserve existing RPC cases.

**Completion gate:** Infrastructure builds; SQL/RLS exercised on isolated local fixture; signatures match checkpoint. SDK RPC/Storage methods currently lack cancellation overloads: honor token before dispatch, await issued side effects, retain keys/outcomes for cleanup. Do not pretend cancellation guarantees transaction rollback.

### Step 5 — Web

**Depends on:** stable contracts and completed Infrastructure.

**Visual scope:** required

### Visual targets

| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| homepage-card-desktop | /admin | 1440x900 | Homepage first; authorized Admin; saved banner count 1 | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2496-6781 |
| homepage-card-tablet | /admin | 834x1194 | same Homepage card state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3048-80290 |
| homepage-card-phone | /admin | 390x844 | same Homepage card state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3048-81366 |
| hero-add-desktop | /admin/homepage/hero-banner | 1440x900 | absent; authorized creator; empty slots; Visible | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3176-51656 |
| hero-add-tablet | /admin/homepage/hero-banner | 834x1194 | same Add state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3176-51697 |
| hero-add-phone | /admin/homepage/hero-banner | 390x844 | same Add state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3176-51738 |
| hero-edit-desktop | /admin/homepage/hero-banner | 1440x900 | saved visible; two images; design text | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-36055 |
| hero-edit-tablet | /admin/homepage/hero-banner | 834x1194 | same Edit state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-39766 |
| hero-edit-phone | /admin/homepage/hero-banner | 390x844 | same Edit state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-40407 |
| hero-hidden-phone | /admin/homepage/hero-banner | 390x844 | Hidden; mobile absent; inferred state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3169-40407 |
| hero-validation-phone | /admin/homepage/hero-banner | 390x844 | Add; Save empty; required errors; inferred state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3176-51738 |

**Figma references:** preserve original supplied URL: https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56955&t=wfmg6SLH72nvfFsJ-11 . Section `3185:56955` contains all six frames. Desktop screenshot and instance sources inspected through Desktop Bridge on 2026-10-27; REST was rate-limited. Re-fetch exact nodes at Web start. No design JSON.

Additional supplied Admin Console URL: https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3048-79606&t=cWBtc9BODDtj4EUK-11 . Inspected section `3048:79606`, desktop screenshot and responsive card bounds through Desktop Bridge on 2026-10-28. Homepage is first in all three designed viewports. Reference card nodes: desktop `I2985:20873;2985:16708;3183:56889`, tablet `I3048:80291;3038:32548;3183:56913`, phone `I3048:81367;3038:32795;3183:56937`.

**Capture scope:** crop `[data-testid="hero-banner-admin"]` to matching Figma Body/Main bounds; export exact Body/Main node at 1x. Element capture includes tall form below fold; reference and actual dimensions must match, never stretch. Inspect header/breadcrumb/footer separately through existing shell behavior. For inferred states, retain exact base-frame reference plus written expected error/selection changes; do not claim independently designed Figma parity.

For `homepage-card-*`, crop `[data-testid="admin-module-homepage"]` and export corresponding card node at 1x. Wait for its loaded count/link, not skeleton. Verify first position separately in dashboard DOM and browser geometry. Design count 5 is sample data; actual count is 0/1 for current singleton scope. Record this expected data difference without editing reference. Existing card order and absent Permissions/Orders modules stay unchanged. Test temporary parent heading/button, destination, View restriction, and Cancel return behavior at desktop/phone widths; parent has no dedicated supplied frame, so no Figma parity claim.

**Readiness:** root has `data-state="add|edit|absent|load-error"`; await `[data-testid="hero-banner-admin"][data-state="add"]` or `edit`, then image/font readiness. Controls: `hero-heading`, `hero-description`, `hero-button-label`, `hero-button-link`, `hero-visible`, `hero-hidden`, `hero-save`, `hero-cancel`, `hero-reload`, `hero-desktop-image`, `hero-mobile-image`. Give heading field explicit `id="hero-heading"`. Validation target: click Save, then wait for `#hero-heading-error .validation-message` using existing field error-region contract. Hidden target: click Hidden, remove mobile artwork, Save, then reopen editor and wait for Edit with Hidden selected. Supply final `--wait-for` after asynchronous actions.

**Deterministic data:** isolated local fixture, never production. Add state has no row. Edit text: `Elevate Your Experience`; `Premium vaping products, authentic brands, and fast shipping — built for Canada.`; `Shop Now`; `/products`. Reuse local `AuthStateFactory` Admin/Support/Customer personas; prepare additional View+Create and View+Edit combinations in test fixture. Refresh cached token state after grants. Use one dedicated local banner fixture; serialize singleton scenarios and restore fixture state explicitly. Missing local environment blocks capture; do not run reset-bearing `start-e2e-env.ps1` implicitly.

**Assets:** Figma image hash `8470d1a8112fbfd4b34058f9e0660338e7303c62` is 1672×941, below accepted desktop minimum. Confirmed mitigation: export image-only crop at 1920×1080 and mobile crop at 864×1080 for visual fixtures; retain Figma provenance. Enlarging adds no source detail. Production upload validation stays unchanged: original 1672×941 desktop file is rejected. No automatic production resizing. Edit desktop image node: `I3169:36056;2985:16708;3176:52777;3016:20009;2944:14403`; mobile equivalent uses `3176:52786`. Never use full page screenshot as artwork.

**Responsive decisions:** desktop columns 2fr/1fr with 32px gap; page padding 64px/32px and heading gap 64px. Below shared 1024px threshold, stack with 48px gaps/padding and 32px inline gutters; below 768px use 32px gaps/block padding and 16px gutters. Desktop/tablet title uses existing h2 token; phone uses h4; section headings use h5. Desktop image preview stays 16:9 on every admin viewport; mobile stays 4:5. Execute reference comparison corrects earlier phone-width reading: mobile preview stays 216px on all designed widths, shrinking only when available space is narrower. Artwork sections use 24px gap on desktop/tablet and 32px on phone; settings use 24px gap. Check 320px and both sides of 768px/1024px. The 600px helper describes future storefront use only; it does not change admin preview/layout.

### Component mapping and capability decisions

| Figma source / selected variant | Existing code and parameters | Decision / gap |
|---|---|---|
| Admin Console Homepage card; frame nodes recorded above | existing `AdminModuleCard` with new `AdminModule.Homepage` mapping | Reuse card markup/styles; add root test marker for Homepage; first visible card links to Manage Homepage |
| Button `157:24`; Save Filled/Primary/Large `170:2625` | `ShopButton Type="submit" Size=Large EndIcon=ShopIcons.Outlined.Arrow_Right_MD Loading=busy` | Reuse; no separate spinner |
| Cancel Outlined/Primary/Large `170:2656`; Replace Outlined/Primary/Medium `170:2647` | `ShopButton Variant=Outlined`, Cancel Href uses parent route | Preserve navigation semantics; Replace opens picker |
| Text Field `170:136`; Normal `2732:18856`, Placeholder `2732:18850` | `ShopTextField Label`, bound Value; link native type=text | Reuse labels; add optional `Lines` parameter default 1, multiline textarea when >1 for Description |
| Chip `180:485`; Selected Medium `2036:10249`, Default Medium `180:518` | `ShopChip Size=Medium Selected SelectedChanged` | Reuse boolean selection inside labelled fieldset |
| Image Dropzone `3013:17971`, Default `3013:17941` | `ShopImageUpload` composed `ShopFileDropzone` | Add opt-in `ShopImageUploadLayout.SinglePreview` (default Grid), retaining bounded reads/URL ownership |
| Image Upload Tile `3013:18188`, Hover `3016:20008`; image overrides resize SquareContain source | `ShopImageTile`, `ShopImage`, `ShopIconButton` | Add `ShopImageTileFrame.Image` (default Square) and `ResponsivePreview` pass-through; add `ShopImage.Responsive` default true. Static Hero preview stays 16:9; PortraitCover stays 4:5 |
| Remove Filled/Surface/Small `2947:17027` | existing tile uses `ShopIconButton` Surface/Filled/Small | Reuse accessible RemoveLabel; no duplicate SVG |
| Image selection rejection | current upload replaces previous single selection on rejection | New optional `ValidateFileAsync(ShopUploadedImage, CancellationToken) -> Task<string?>` hook; SinglePreview rejects candidate without replacing prior Files/URLs. Default Grid behavior unchanged |

Upload hook returns localized failure text supplied by page after mediator result; contains no feature rules. `ShopImageUpload` passes PreviewPreset/ResponsivePreview through tile. Frame=Image lets inner ShopImage reserve height instead of square outer aspect. New properties are planned extensions, not claims about current APIs. Shared SCSS owns new modifiers; no page overrides of shared internals.

**Accepted design-rule differences:** 5 MB helper overrides Figma 2 MB; real labels remain accessible; Description uses multiline existing field extension; inferred keyboard/error/loading/read-only states use native/Shop contracts. Use existing tokens/fonts, no MudBlazor. Homepage dashboard card follows newly supplied Admin Console frames, with actual 0/1 count; temporary Homepage parent reuses current heading/button patterns without dedicated Figma parity claim. Existing Product/Brand/Category upload callers keep default parameters; no visual change planned there. Re-run their upload/field regression coverage; if defaults change, expand visual targets before implementation.

- [ ] **TASK-011** — Add opt-in upload/tile/static-image and multiline text-field capabilities above, with owned component SCSS; preserve existing callers and native semantics. User-directed text-field decision: retain original field sizing and validation spacing; keep `Lines` support for multiline Description, without a compact parameter or styling override. User-directed button decision: retain original `ShopButton` size behavior, including icon-based minimum height for text-only buttons; Replace/Cancel use existing sizes without a new sizing parameter or override. These decisions take precedence over Figma's shorter field and text-only button heights. Artwork sections use 24px gap on desktop/tablet and 32px on phone; settings sections use 24px gap. Optional UploadHint supplies browsing guidance without changing existing callers.
- [ ] **TASK-012** — Add temporary `ManageHomepage.razor/.razor.cs` with heading and one Hero Banner `ShopButton Href` link; add `ManageHeroBanner.razor/.razor.cs`, route/busy constants, breadcrumbs and Homepage `AdminModuleCard` mapping/test marker. Place Homepage first; derive console skeleton count from module catalogue instead of fixed five. Match supplied Homepage card design; retain other modules' relative order. Prove card destination, parent button, denied access and Cancel return.
- [ ] **TASK-013** — Wire editor `EditForm`, plain `HeroBannerFormModel`, two controlled upload slots, mediator validation, read-only/load-error states; global App route denial, command errors, Save/Reload/Cancel and notifications. Keep page orchestration only; no business rules.
- [ ] **TASK-014** — Add `Styles/layouts/_hero-banner.scss`, load through `TheShop.scss`, English resources in Section 9, and `wwwroot/js/heroBannerImages.js` for browser inspection. Shared appearance stays in owning component partials. No generated CSS/accessor edits.
- [ ] **TASK-015** — Prepare isolated visual fixture, export exact references, rebuild app and capture every target. Inspect reference/actual/overlay/difference and browser geometry; fix responsible code, recapture after final edits, document component reuse and reviewed capture hashes.

**Completion gate:** Web builds; no duplicate validators, literal routes, hardcoded UI text or vendor consumers; source matches component mapping; read-only/denied states work. Visual gate passes with reviewed evidence for every target. Three consecutive non-improving visual passes stop Execute with concrete blocker.

### Step 6 — Integration & pipeline

**Depends on:** Infrastructure/Web completion.

- [ ] **TASK-016** — Verify solution/DI, migration replay, least-privilege RLS, primary save/failure paths, private previews, no storefront code changes, and current visual evidence. Run required design/scope gates.
- [ ] **TASK-017** — Test stage: author focused Domain invariant/catalogue tests; Application query/handler/validator and single-action authorization regressions; Infrastructure repository, storage/inspection, schema/RPC/RLS tests; Web bUnit editor and shared-control regressions. Stamp feature trait/filter `003_manage-hero-banner` and manifest every AC.
- [ ] **TASK-018** — E2E/Test/Verify stages: cover real OTP personas, Add/Edit/reload, file rejection, 5 MB boundary, optional mobile, Hidden, view-only/denied, cancellation and duplicate creation; verify native assets and visual gate. Preserve existing Product/Category/Brand upload and single-permission behavior.

Planned focused commands: `dotnet build TheShop.slnx --nologo -m:1 -p:UseSharedCompilation=false`; `dotnet test TheShop.slnx --filter "Feature=003_manage-hero-banner"`; existing shared authorization/upload tests with their actual test filters. Author new tests only in owning Test/E2E stage. After code changes, run `graphify update .`.

### Deviation procedure

- Accept convention-aligned changes preserving confirmed behavior, authorization, integrity and layer ownership. Reject scope expansion and weakened validation/security.
- Contract change: stop dependents, update Section 4/checkpoint/affected tasks, compile and mark Stable again before resuming.
- New requirement or unresolved product behavior returns to Spec/Clarify; new technical decision enters Section 11 and Resolve. Keep task IDs stable once resolved.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: absent/Add defaults | TASK-003, TASK-012, TASK-013, TASK-017, TASK-018 |
| AC-2: existing/Edit hydration | TASK-003, TASK-008, TASK-013, TASK-015, TASK-017 |
| AC-3: guest/no View denied | TASK-002, TASK-007, TASK-012, TASK-017, TASK-018 |
| AC-4: separate View/Create/Edit | TASK-001, TASK-002, TASK-007, TASK-013, TASK-017, TASK-018 |
| AC-5: draft image selection | TASK-004, TASK-011, TASK-013, TASK-017, TASK-018 |
| AC-6: bytes/format/geometry rejection | TASK-004, TASK-010, TASK-011, TASK-014, TASK-017, TASK-018 |
| AC-7: required heading/desktop | TASK-001, TASK-004, TASK-005, TASK-013, TASK-017 |
| AC-8: button pair | TASK-001, TASK-004, TASK-013, TASK-017 |
| AC-9: URL text validation/persistence | TASK-004, TASK-007, TASK-008, TASK-013, TASK-017 |
| AC-10: save/reopen all fields | TASK-005, TASK-007, TASK-008, TASK-009, TASK-013, TASK-018 |
| AC-11: persisted Visible/Hidden | TASK-001, TASK-005, TASK-007, TASK-013, TASK-015, TASK-018 |
| AC-12: Cancel | TASK-011, TASK-012, TASK-013, TASK-017, TASK-018 |
| AC-13: mobile removal/desktop required | TASK-004, TASK-005, TASK-013, TASK-017, TASK-018 |
| AC-14: load/upload/save/permission failures | TASK-002, TASK-005, TASK-007, TASK-008, TASK-009, TASK-013, TASK-017, TASK-018 |
| AC-15: duplicate/concurrent creation | TASK-005, TASK-007, TASK-008, TASK-013, TASK-017, TASK-018 |
| AC-16: responsive/accessibility | TASK-011, TASK-012, TASK-013, TASK-014, TASK-015, TASK-017, TASK-018 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- **RULE-1:** single-action authorization before validators; SQL checks only the live Create or Edit permission for writes. Management read RPCs and routes require View. App owns `AccessDeniedView`; page handles save denial through existing error notifications and load denial through the retryable load-error state.
- **RULE-2:** Create precheck gives friendly conflict; singleton primary key guarantees race safety. SQLSTATE `23505` on `hero_banners_pkey` maps `HeroBanner_AlreadyExists`; Reload action keeps draft until chosen. Hidden counts as existing; repeated Save guarded by BusyState.
- **RULE-3:** nonblank trimmed Heading and effective desktop slot required. Create requires upload; Update rejects explicit removal without replacement. Domain repeats persisted invariants through typed exceptions. Description optional; empty optional text becomes null.
- **RULE-4:** shared image validator checks nonempty bytes, length <=5,242,880, declared MIME in allowlist, detected format matches MIME, successful decode. Desktop `width >=1920`, `height >=1080`, `width*9 == height*16` using widened arithmetic; mobile `width*5 == height*4`. No invented mobile minimum. Filename never determines actual type; adapter writes canonical extension. Reject corrupt files or failed decoding. Browser read guards are transport bounds, not duplicated feature validation.
- **RULE-5:** normalize whitespace-only optional fields to null; require both button fields or neither. Both validators and entity preserve pair invariant.
- **RULE-6:** reject whitespace/control characters inside destination and any backslash; single-root `/` allowed, `//` denied. Absolute `Uri.TryCreate` must have HTTP/HTTPS scheme and nonempty host. Reject script/data/other schemes. No destination network probe; plain text persistence only.
- **RULE-7:** bool IsVisible produces exactly one selected status; no other state. Save RPC atomically writes entire row; failure preserves prior row. New-image object existence checked before save. Typed repository failures remain Results; cancellation propagates, never converted to fake absent state.

Application validators return all `FieldValidationError`s; image query errors display beside slot without replacing prior accepted selection. Exception resource constants follow existing Domain pattern; `HeroBannerErrorKeys` references these constants where applicable. Add Web resource-key coverage tests, following existing Category/Brand localization tests.

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `HeroBanner_HeadingRequired` | Enter a heading. |
| `HeroBanner_DesktopImageRequired` | Add a desktop image before saving. |
| `HeroBanner_ButtonPairRequired` | Enter both a button label and link, or leave both blank. |
| `HeroBanner_ButtonLinkInvalid` | Enter a site path beginning with / or a valid HTTP or HTTPS URL. |
| `HeroBanner_ImageInvalidType` | Select a PNG, JPG, or WebP image. |
| `HeroBanner_ImageTooLarge` | Each image must be 5 MB or smaller. |
| `HeroBanner_ImageUnreadable` | This image could not be read. Select a valid image. |
| `HeroBanner_DesktopDimensionsInvalid` | Use a 16:9 image at least 1920 × 1080 pixels. |
| `HeroBanner_MobileDimensionsInvalid` | Use a mobile image with 4:5 proportions. |
| `HeroBanner_AlreadyExists` | A hero banner already exists. Reload to edit it. |
| `HeroBanner_NotFound` | The hero banner is no longer available. Reload to continue. |
| `HeroBanner_ImageUnavailable` | An image is no longer available. Select it again or reload. |
| `HeroBanner_LoadFailed` | Could not load the hero banner. Try again. |
| `HeroBanner_UploadFailed` | Could not upload the image. Your draft is still available. |
| `HeroBanner_SaveFailed` | Could not save the hero banner. Your draft is still available. |
| `HeroBanner_SaveUnconfirmed` | Save could not be confirmed. Reload to check the saved banner before retrying. |

SQLSTATE `42501` maps existing access-denied key; `P0002` maps NotFound; `22023` from missing stored object maps ImageUnavailable. Transport error after RPC dispatch maps SaveUnconfirmed, with no automatic destructive compensation/retry. Loading/signing failures map LoadFailed and explicit retry; no Add state on read failure. Signed image URL expiry shows existing image placeholder plus Reload guidance; draft text stays local.

Additional typed UI keys in same resource file: `Homepage_Heading` = Homepage; `Homepage_HeroBanner` = Hero Banner; `AdminConsole_Module_Homepage` = Homepage; `HeroBanner_AddHeading` = Add Hero Banner; `HeroBanner_EditHeading` = Edit Hero Banner; `HeroBanner_Absent` = No hero banner has been added.; `HeroBanner_DesktopImage` = Desktop image; `HeroBanner_MobileImage` = Mobile image (optional); `HeroBanner_Content` = Content; `HeroBanner_Heading` = Heading; `HeroBanner_Description` = Description; `HeroBanner_ButtonLabel` = Button label; `HeroBanner_ButtonLink` = Button link; `HeroBanner_Status` = Status; `HeroBanner_Visible` = Visible; `HeroBanner_Hidden` = Hidden; `HeroBanner_Reload` = Reload; `HeroBanner_Saved` = Hero banner saved.; `HeroBanner_RemoveDesktop` = Remove desktop image; `HeroBanner_RemoveMobile` = Remove mobile image; `HeroBanner_DesktopHint` = 16:9. At least 1920 × 1080. PNG, JPG or WebP up to 5 MB. Keep the left third clear for text.; `HeroBanner_MobileHint` = 4:5, shown below 600px. Without one, phones show a centre crop of the desktop image. Reuse existing `Save`, `Cancel`, `Loading`, image-upload and notification keys. Preview alt uses associated slot label; all dynamic text encoded.

No arbitrary maximum content lengths added beyond confirmed spec. MainLayout/shared navigation remain intact. No public homepage code or data reader added.

## 10. Database Schema & RLS Policies

Migration `0034_manage_hero_banner.sql` establishes the feature. Additive `0035_hero_banner_action_permissions.sql` supersedes its combined write gates; apply both in order. SQL below records original 0034, not the final permission policy; existing auth/RBAC/storage schemas come from preceding migrations/local Supabase. No application secret or remote project identifier needed.

### Schema, grants and seed

```sql
BEGIN;

INSERT INTO public.permissions (code, module) VALUES
    ('homepage.view', 'homepage'),
    ('homepage.create', 'homepage'),
    ('homepage.edit', 'homepage')
ON CONFLICT (code) DO NOTHING;

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM public.roles r
JOIN public.permissions p ON p.module = 'homepage'
WHERE r.name_key IN ('Admin', 'SuperAdmin')
   OR (r.name_key = 'Support' AND p.code = 'homepage.view')
ON CONFLICT (role_id, permission_id) DO NOTHING;

CREATE TABLE public.hero_banners (
    id SMALLINT PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    heading TEXT NOT NULL CHECK (length(btrim(heading)) > 0),
    description TEXT,
    button_label TEXT,
    button_link TEXT,
    desktop_image_key TEXT NOT NULL CHECK (
        desktop_image_key ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
    mobile_image_key TEXT CHECK (mobile_image_key IS NULL OR
        mobile_image_key ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
    is_visible BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT hero_banner_button_pair CHECK (
        (button_label IS NULL AND button_link IS NULL) OR
        (button_label IS NOT NULL AND length(btrim(button_label)) > 0 AND
         button_link IS NOT NULL AND length(btrim(button_link)) > 0))
);

REVOKE ALL ON public.hero_banners FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.hero_banners TO authenticated;
ALTER TABLE public.hero_banners ENABLE ROW LEVEL SECURITY;

CREATE POLICY hero_banners_read ON public.hero_banners
FOR SELECT TO authenticated
USING ((SELECT public.authorize('homepage.view')));

CREATE POLICY hero_banners_create ON public.hero_banners
FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));

CREATE POLICY hero_banners_edit ON public.hero_banners
FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

INSERT INTO storage.buckets
    (id, name, public, file_size_limit, allowed_mime_types)
VALUES ('hero-banner-images', 'hero-banner-images', FALSE, 5242880,
        ARRAY['image/png', 'image/jpeg', 'image/webp'])
ON CONFLICT (id) DO UPDATE SET public = FALSE,
    file_size_limit = EXCLUDED.file_size_limit,
    allowed_mime_types = EXCLUDED.allowed_mime_types;

CREATE POLICY hero_images_read ON storage.objects
FOR SELECT TO authenticated USING (
    bucket_id = 'hero-banner-images' AND
    (SELECT public.authorize('homepage.view')));

CREATE POLICY hero_images_create ON storage.objects
FOR INSERT TO authenticated WITH CHECK (
    bucket_id = 'hero-banner-images' AND
    name ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    (SELECT public.authorize('homepage.view')) AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))));

CREATE POLICY hero_images_discard_uncommitted ON storage.objects
FOR DELETE TO authenticated USING (
    bucket_id = 'hero-banner-images' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    (SELECT public.authorize('homepage.view')) AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))) AND
    NOT EXISTS (SELECT 1 FROM public.hero_banners h
        WHERE h.desktop_image_key = storage.objects.name
           OR h.mobile_image_key = storage.objects.name));
```

No banner DELETE grant/policy and no Storage UPDATE policy. New immutable objects only. Existing bucket policies use bucket predicates; new private area gets no anonymous read.

### Explicit read/save RPCs

```sql
CREATE FUNCTION public.get_hero_banner_admin()
RETURNS SETOF public.hero_banners
LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('homepage.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN QUERY SELECT h.* FROM public.hero_banners h WHERE h.id = 1;
END;
$$;

CREATE FUNCTION public.save_hero_banner_admin(
    p_create BOOLEAN, p_heading TEXT, p_description TEXT,
    p_button_label TEXT, p_button_link TEXT,
    p_desktop_image_key TEXT, p_mobile_image_key TEXT,
    p_is_visible BOOLEAN)
RETURNS VOID LANGUAGE plpgsql SECURITY INVOKER
SET search_path = public AS $$
DECLARE affected INTEGER;
BEGIN
    IF NOT public.authorize('homepage.view') OR
       (p_create IS TRUE AND NOT public.authorize('homepage.create')) OR
       (p_create IS FALSE AND NOT public.authorize('homepage.edit')) OR
       p_create IS NULL THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM storage.objects o
        WHERE o.bucket_id = 'hero-banner-images'
          AND o.name = p_desktop_image_key) OR
       (p_mobile_image_key IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM storage.objects o
        WHERE o.bucket_id = 'hero-banner-images'
          AND o.name = p_mobile_image_key)) THEN
        RAISE EXCEPTION 'image unavailable' USING ERRCODE = '22023';
    END IF;
    IF p_create THEN
        INSERT INTO public.hero_banners
            (id, heading, description, button_label, button_link,
             desktop_image_key, mobile_image_key, is_visible)
        VALUES (1, p_heading, p_description, p_button_label, p_button_link,
                p_desktop_image_key, p_mobile_image_key, p_is_visible);
    ELSE
        UPDATE public.hero_banners SET heading = p_heading,
            description = p_description, button_label = p_button_label,
            button_link = p_button_link,
            desktop_image_key = p_desktop_image_key,
            mobile_image_key = p_mobile_image_key,
            is_visible = p_is_visible
        WHERE id = 1;
        GET DIAGNOSTICS affected = ROW_COUNT;
        IF affected = 0 THEN
            RAISE EXCEPTION 'hero banner not found' USING ERRCODE = 'P0002';
        END IF;
    END IF;
END;
$$;

CREATE FUNCTION public.count_hero_banner_admin()
RETURNS BIGINT LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('homepage.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN (SELECT count(*) FROM public.hero_banners);
END;
$$;

REVOKE ALL ON FUNCTION public.get_hero_banner_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.count_hero_banner_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_hero_banner_admin(
    BOOLEAN, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, BOOLEAN) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_hero_banner_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.count_hero_banner_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_hero_banner_admin(
    BOOLEAN, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, BOOLEAN) TO authenticated;

COMMIT;
```

### Single-action permission correction

`0035_hero_banner_action_permissions.sql` is authoritative for final authorization. It changes table INSERT to Create, UPDATE to Edit, and immutable Storage writes/guarded compensation to Create OR Edit. Table/object SELECT permits View OR Create OR Edit because writes need to inspect current configuration and objects. This does not grant the Homepage View permission. Management route/query/read/count RPCs still require View.

New `get_hero_banner_for_write(p_create boolean)` checks only Create when true or Edit when false; NULL is denied. Both command handlers use it before staged uploads. `save_hero_banner_admin` checks the same single action and keeps all previous atomicity, object-existence and conflict behavior. No schema/data changes or grants to anon. Existing callers remain compatible because existing RPC signatures stay unchanged.

Rollback: restore the six original 0034 policy expressions and original save RPC definition; retain the additive write-preparation RPC while new clients may still use it. Deploy the previous client before revoking that function. Banner rows, image keys, private bucket settings and role assignments remain unchanged. Local verification applies the migration without reset and rolls back all temporary fixture writes/grants.

Existing permission-version triggers handle new grants. No auth hook rewrite. Tests apply actual checked-in migration with real permission helpers and Storage stubs/local Storage as applicable; assert guest/no-permission denial; View-only read without writes; Create-only creation and Edit-only update without View; cross-action denial; management route/read RPC denial without View; live revocation; owner-prefix restrictions; no object overwrite and no referenced-object delete. Direct-table tests prove singleton/paired/required invariants; Application/browser tests prove image content/geometry and URL format. SQL/Storage MIME metadata alone does not prove decoded image content.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** Permitted direct callers can bypass browser dimension/content validation. Current admin scope accepts this boundary; Storage enforces permissions, 5 MB limit and declared MIME, not trusted decoding. No server upload-validation service added. TASK-004/007/010/017/018 cover browser rejection and unauthorized-access prevention.
- **⚠️ Risk — ✅ Accepted:** Replaced/interrupted artwork may remain stored privately; existing preview links remain usable until 900-second expiry even after permission revocation. Retention avoids deleting files involved in concurrent saves. Automatic cleanup remains outside scope; guarded compensation handles definite failures and Reload resolves uncertain Save before retry. TASK-005/009/013/017 own safeguards.

---
**Status:** Resolved · **Spec:** `.specs/003_manage-hero-banner/spec.md` · **Created:** 2026-10-27 · **Resolved:** 2026-10-28
