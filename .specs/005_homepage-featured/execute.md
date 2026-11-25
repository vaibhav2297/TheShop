# Execute — Manage Homepage Featured

**Feature:** `005_homepage-featured` · **Date:** 2026-11-01 · **Branch:** `feature/005_homepage-featured`

## Phase evidence

| Phase | Production work | Build | Scope |
|---|---|---|---|
| Preflight | Resolved plan; existing artifact changes preserved | Solution: 0 errors | plan-gate pass |
| Domain | TASK-001: entity, immutable tiles, four invariant exceptions | Domain: 0 errors | domain pass |
| Application | TASK-002/003: normalized contracts, nested validation, image inspection, read/write handlers | Application: 0 errors | application pass |
| Infrastructure | TASK-004/005: SQL, RLS, private storage, RPC repository, DI | Infrastructure: 0 errors | infra pass |
| Web | TASK-006/007: editor, repeated tiles, heading, states, validation, bounded sidebar, resources | Web: 0 errors | web pass |
| Integration | TASK-010 build/design portion | Solution: 0 errors, 10 existing warnings | all four scope gates pass |

Scope gates received actual file arrays, including untracked files. Design-rule path check includes all new Featured C#/Razor files plus shared uploader. Resource XML valid; no duplicate keys. Whitespace diff check passes.

## Files by phase

### Domain — 6

- `src/TheShop.Domain/Entities/FeaturedSection.cs`
- `src/TheShop.Domain/ValueObjects/FeaturedTile.cs`
- `src/TheShop.Domain/Exceptions/FeaturedSectionTitleRequiredException.cs`
- `src/TheShop.Domain/Exceptions/FeaturedTileTitleRequiredException.cs`
- `src/TheShop.Domain/Exceptions/FeaturedImageRequiredException.cs`
- `src/TheShop.Domain/Exceptions/FeaturedButtonPairRequiredException.cs`

### Application — 21

- `src/TheShop.Application/Features/Homepage/DTOs/FeaturedImageDto.cs`
- `src/TheShop.Application/Features/Homepage/DTOs/FeaturedTileDto.cs`
- `src/TheShop.Application/Features/Homepage/DTOs/FeaturedDto.cs`
- `src/TheShop.Application/Features/Homepage/DTOs/FeaturedTileInput.cs`
- `src/TheShop.Application/Features/Homepage/Interfaces/IFeaturedRepository.cs`
- `src/TheShop.Application/Features/Homepage/FeaturedErrorKeys.cs`
- `src/TheShop.Application/Features/Homepage/Validation/FeaturedImageValidation.cs`
- `src/TheShop.Application/Features/Homepage/Validation/FeaturedTileInputValidator.cs`
- `src/TheShop.Application/Features/Homepage/Commands/CreateFeatured/CreateFeaturedCommand.cs`
- `src/TheShop.Application/Features/Homepage/Commands/CreateFeatured/CreateFeaturedCommandValidator.cs`
- `src/TheShop.Application/Features/Homepage/Commands/CreateFeatured/CreateFeaturedHandler.cs`
- `src/TheShop.Application/Features/Homepage/Commands/UpdateFeatured/UpdateFeaturedCommand.cs`
- `src/TheShop.Application/Features/Homepage/Commands/UpdateFeatured/UpdateFeaturedCommandValidator.cs`
- `src/TheShop.Application/Features/Homepage/Commands/UpdateFeatured/UpdateFeaturedHandler.cs`
- `src/TheShop.Application/Features/Homepage/Commands/FeaturedWrite.cs`
- `src/TheShop.Application/Features/Homepage/Queries/GetFeaturedForAdmin/GetFeaturedForAdminQuery.cs`
- `src/TheShop.Application/Features/Homepage/Queries/GetFeaturedForAdmin/GetFeaturedForAdminHandler.cs`
- `src/TheShop.Application/Features/Homepage/Queries/ValidateFeaturedImage/ValidateFeaturedImageQuery.cs`
- `src/TheShop.Application/Features/Homepage/Queries/ValidateFeaturedImage/ValidateFeaturedImageHandler.cs`
- `src/TheShop.Application/Features/Homepage/Queries/ValidateFeaturedImage/ValidateFeaturedImageQueryValidator.cs`
- `src/TheShop.Application/Common/Storage/StorageArea.cs`

### Infrastructure — 6

- `src/TheShop.Infrastructure/Persistence/Records/FeaturedSectionRecord.cs`
- `src/TheShop.Infrastructure/Persistence/Mappers/FeaturedSectionMapper.cs`
- `src/TheShop.Infrastructure/Persistence/Repositories/SupabaseFeaturedRepository.cs`
- `src/TheShop.Infrastructure/DependencyInjection.cs`
- `src/TheShop.Infrastructure/Storage/SupabaseFileStorage.cs`
- `supabase/migrations/0037_manage_featured.sql`

### Web — 20

- `src/TheShop.Web/Pages/Admin/FeaturedFormModel.cs`
- `src/TheShop.Web/Pages/Admin/FeaturedTileFormModel.cs`
- `src/TheShop.Web/Components/Admin/FeaturedTileEditor.razor`
- `src/TheShop.Web/Components/Admin/FeaturedTileEditor.razor.cs`
- `src/TheShop.Web/Pages/Admin/ManageFeatured.razor`
- `src/TheShop.Web/Pages/Admin/ManageFeatured.razor.cs`
- `src/TheShop.Web/wwwroot/js/featuredEditor.js`
- `src/TheShop.Web/Styles/layouts/_featured.scss`
- `src/TheShop.Web/Common/Routes.cs`
- `src/TheShop.Web/Common/BusyKeys.cs`
- `src/TheShop.Web/Pages/Admin/ManageHomepage.razor`
- `src/TheShop.Web/Styles/TheShop.scss`
- `src/TheShop.Web/Resources/Strings.resx`
- `src/TheShop.Web/Components/Common/ShopImageUpload.razor.cs`
- `src/TheShop.Web/Styles/components/_imageupload.scss`
- `src/TheShop.Web/Components/Common/ShopImageUpload.razor`
- `src/TheShop.Web/Styles/components/_image.scss`
- `src/TheShop.Web/Styles/abstracts/_image-presets.scss`
- `src/TheShop.Web/Pages/Admin/ManageHeroBanner.razor`
- `src/TheShop.Web/Styles/layouts/_hero-banner.scss`

## Database

Literal Section 10 migration applied successfully to isolated PostgreSQL 17 with minimal Supabase schema stubs. Temporary container removed afterward.

Same additive migration applied through Supabase MCP to configured **TheShop-Dev** (`uwfltzgecsepvlaplktv`). Inspection confirms Featured table RLS, three table policies, read/insert storage policies, private bucket, 5,242,880-byte limit, PNG/JPEG/WebP allowlist, and zero seeded rows. No production database mutation.

Full permission/RPC behavior proof remains Test-stage work; successful migration application is not RLS test evidence.

## Decisions and stage boundaries

- Contracts frozen as planned. No schema or permission deviation.
- Final user correction applies across ShopImageUpload. One shared Sass map supplies every preset ratio to ShopImage and ShopImageUpload. Empty drop zone, preview frames, and grid add tiles use that value. Generic preset/responsive classes replace the former C# square/portrait branches. Removed fixed portrait widths, fixed empty-area heights, and layout-specific ratios. BrandLogo retains its existing square upload allocation because the preset has no intrinsic ratio. No new parameter.
- Short upload/add frames hide decorative icons through container queries; text/hints remain visible. Grid items align at their start so primary-row spanning cannot stretch image ratios.
- Correction validation: final solution build passes with 0 errors/3 existing warnings; Web build passes with 0 warnings/errors. Web scope, design rules, plan gate, and whitespace checks pass.
- Headless Chrome geometry check: 880 shape checks pass across all 8 presets, widths 320/767/768/1024/1440, responsive on/off, and SinglePreview/Grid/primary-tile layouts. Isolated markup using generated CSS; no Figma screenshots or claim of full application E2E coverage. No ratio failures or visible-content overflow.
- Existing shared component tests: 55 pass. Four pending-upload tests fail identically with original HEAD ShopImageUpload C#/Razor files; current files restored and 55 remaining tests rerun successfully. Failure locations: NativeImageUploadTests.cs:148/165/180/193. Their GatedStream overrides CopyToAsync, while the existing uploader reads through ReadAsync. No upload-processing logic or test code changed; baseline failures remain for Test-stage follow-up.
- Follow-up consumer regression check: 140 more existing tests pass across ShopImageUpload, ProductForm, Brand/Category Add/Edit, ManageHeroBanner, and VariantImageDialog. One ProductForm pending-upload test fails identically with original HEAD uploader files (ProductFormTests.cs:744); same CopyToAsync/ReadAsync test-double mismatch. Combined coverage: 195 passing existing tests, 5 confirmed baseline failures. Current component files restored after baseline checks. No new functional regression found within this coverage; full application browser journeys and manual appearance review remain pending.
- User identified a Hero mobile width impact outside the earlier test coverage. Figma frame 3176:51656 contains mobile placeholder I3176:51657;2985:16708;3176:52498 at 216×270. ManageHeroBanner now wraps only its mobile uploader in a page-owned div with full available width capped at 13.5rem. Shared 4:5 preset remains; helper/error regions remain outside the wrapper. Empty and uploaded states stay 216×270, shrinking to 180×225 in a 180px container. Web build, design/scope checks, all 8 existing ManageHeroBanner tests, and 10 isolated browser geometry checks pass. No shared component changes for this fix.
- Status typography correction: Featured and Hero Banner legends now use existing subtitle-1 token (Space Grotesk 16px Medium/500, automatic line height, 0.25px tracking, secondary text color). Removed body-1 Regular/400 and forced field line height from both page SCSS rules. Figma inspected: Featured desktop/Add/tablet/phone Status nodes; Hero Add desktop node I3176:51657;2985:16708;3176:51670. Web build/scope pass; 6 computed-style checks pass across both labels at 1440/834/390 widths. No Razor or status-selection logic change.
- Pending image callbacks remain accepted while Save awaits both upload controls. Disabled uploader owns prevention of new selection.
- Sidebar observers rebind after reload, measure panel/root resize, clear mobile offsets, and dispose on navigation. Import/mount task is awaited during disposal.
- Existing controls supply focus, hover, disabled and error appearance where Figma omits states.
- Gate's supported parameter is `-Phase infra`; used instead of canonical example's unsupported `infrastructure`.
- No waiver. Execute skill explicitly says: **“Do not write or run feature tests here; `$theshop-test` and `$theshop-e2e` own them.”**
- TASK-001–005 test portions, TASK-008/009, and TASK-010 test/AC proof remain pending. Production completion does not mark those mixed tasks fully checked.

## Manual review

Route: `/admin/homepage/featured`; entry: `/admin/homepage`.

Review Add/Edit, image replacement/removal, dynamic headings, desktop/tall Section behavior, tablet and phone layouts against plan Figma nodes. Appearance and browser behavior are not claimed approved or test-proven during Execute.

## Graph

Final AST refresh completed during Status typography fixes: 11,614 nodes, 28,956 edges, 608 communities. SQL parser unavailable (`tree_sitter_sql`); 38 SQL files absent from graph. 39 metadata/source files produced zero nodes. Community names use hub fallback where saved labels drifted; no semantic extraction used.

**Next:** `$theshop-test 005_homepage-featured`.
