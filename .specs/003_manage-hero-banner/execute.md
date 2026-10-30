# Execute — Manage Hero Banner

**Feature:** `003_manage-hero-banner`
**Date:** 2026-10-28

## Checklist

- [x] Pre-flight: resolved plan gate; baseline solution build (0 errors, existing package/analyzer warnings).
- [x] Domain: TASK-001; Domain build and scope gate pass.
- [x] Application: TASK-002–006; Application build and scope gate pass.
- [x] Infrastructure: TASK-007–010; build and scope gate pass. Migration 0034 applied to existing local Supabase only, without reset. Inspected permissions, RLS policies, private bucket and 5,242,880-byte limit. Manual authenticated read without Homepage View correctly raises access denied.
- [ ] Web: TASK-011–015; build, scope and reviewed visual evidence.
- [ ] Integration: TASK-016; solution build, design gate, visual gate, graph update.
- TASK-017–018 reserved for explicitly invoked Test/E2E stages.

## Contracts

Domain: `HeroBanner.Create/Rehydrate(heading, description?, buttonLabel?, buttonLink?, desktopImageKey, mobileImageKey?, isVisible)`, `ChangeContent`, `ChangeImages`, `SetVisibility`; singleton `Id = 1`. Homepage View/Create/Edit constants and permission entries.

Application: Section 4 request/interface signatures compiled. Create desktop upload nullable at transport boundary so validator reports required-field failure; valid requests require artwork. User-directed correction restores the original single-permission pipeline. Create requires Create only; Update requires Edit only. Existing undecorated/single-attribute semantics remain unchanged; regression tests reserved for Test stage. Update replacement wins over removal for supplied slot, matching effective-image validation.

Files: Domain entity, three exceptions, permission catalogue; Application Homepage DTOs/interfaces/queries/commands/validation, Admin module/catalogue. Shared authorization attribute/behavior restored to their original single-permission implementation.

## Deviations

None affecting confirmed scope. No feature tests authored or run during Execute.

Scope script accepts `infra`, while canonical Execute spells `infrastructure`; used supported `infra` value after argument rejection. Infrastructure files: migration 0034, persistence record/mapper/repository, private storage and browser inspector adapters, DI, dashboard count branch. SDK calls honor cancellation before dispatch and await issued side effects.

Web start: exact nine Figma frame identities refreshed on 2026-10-28. Desktop Add/Edit screenshots inspected. REST export returns 429; Desktop Bridge remains available. Use Bridge for exact reference exports.

Browser correction: exported exact nine references at 1x; image-only fixture exports 1920×1080 and 864×1080. Seeded through real admin Save; private upload, atomic write and reload succeed. Refreshed local Admin/Support/Customer sessions through existing OTP factory. Existing local auth-state files backed up.

Application reopened once: button-pair failures now target missing counterpart; reject malformed percent escapes. Application build/scope pass.

Shared capability additions from browser comparison: native textarea grows with content through `Lines`. User-directed field revision retains original sizing and validation spacing, removing the temporary compact parameter, modifier and usages. UploadHint enables design browsing guidance; reads enforce streaming byte ceiling. Exact phone reference corrects initial plan: mobile preview remains 216px. Existing source references remain unchanged. User-directed button revision removes the temporary sizing parameter and CSS override; Replace/Cancel use original ShopButton size behavior, accepting their height difference from Figma. Original text-field sizing similarly takes precedence over Figma's shorter fields.

Capture correction: element screenshots previously clipped lower form at scroll-root boundary. VisualCapture now permits ancestor overflow during crop capture; application source remains responsible for layout. Initial captures are diagnostics, not reviewed proof. Rebuild/restart host after source changes; stale asset manifests caused transient framework 404s and failed readiness.

## User-directed storage consolidation — 2026-10-28

- Reused shared `IFileStorage`/`SupabaseFileStorage`; removed banner-only storage interface, adapter and DI registration.
- Added `StorageArea.HeroBannerImages` and generic `CreateSignedUrlAsync(area, key, expiresInSeconds, ct)`. Private area rejects `GetPublicUrl`; existing public areas keep current behavior.
- Banner read signs previews for 900 seconds. Write handlers derive owner from `ICurrentUserService.Id`; image extensions come from validated MIME type. Shared upload/delete preserve immutable keys and existing storage RLS cleanup rules.
- Application and Infrastructure builds/scope gates pass. Full solution build passes: 0 errors; existing warnings. Plan, status and design-rule gates pass. No remaining live references to removed storage types. Graph updated through AST extraction; SQL parser dependency remains unavailable.
- Fresh local OTP session on isolated localhost:5227: replaced desktop artwork using PNG bytes with misleading .jpg filename; Save created a new .png key; private signed previews and both image slots survived reload. Initial cached-session attempt correctly failed with HTTP 401. Post-save browser reload reported an aborted request after commit; persisted replacement and both image loads were independently confirmed after reload.
- Feature tests remain reserved for Test stage. Full Execute remains pending final visual evidence; storage consolidation itself is complete.

## User-directed permission correction — 2026-10-28

- Follow Brand flow: Create declares only Homepage Create; Update declares only Homepage Edit. Restore original single-use permission attribute and pipeline. Page routes and management queries retain View.
- Remove page-level `AccessDeniedView` and `_denied`. App route handles page denial; save failures use existing notifications, load failures retain retry. Mutation controls check only the relevant action permission.
- Add `IHeroBannerRepository.GetForWriteAsync(create, ct)` and action-authorized `get_hero_banner_for_write` RPC. Save preparation no longer adds a hidden View prerequisite.
- Add migration 0035 without rewriting already-applied 0034. SQL writes require one action; immutable artwork upload/compensation allows Create OR Edit. Writer SELECT access supports UPDATE visibility, object checks and guarded cleanup. Management read/count RPCs remain View-gated; no public access added.
- Local migration applied without reset. Manual rolled-back SQL fixture proves Create-only upload/create, Edit-only upload/update, cross-action denial, View-only read with upload/save denial, management-read denial without View, and live revocation. Original banner checksum matches after rollback; temporary role/grants/object rows roll back. Direct SQL image deletion was blocked by Supabase Storage's API-only guard; omitted that unrelated check and reran permission proof successfully.
- Before/after solution builds pass: 0 errors, 56 existing warnings. Application and Infrastructure/Web scope, design, spec, plan, status and whitespace gates pass. No feature tests authored or run; full Execute still awaits final visual evidence.

## User-directed button sizing restoration — 2026-10-28

- Removed the temporary button sizing parameter, CSS modifier and both usages. ShopButton code-behind and button SCSS match the original implementation. Replace Image and Cancel use existing minimum heights.
- Web build passes with 0 warnings/errors; Web scope, plan, design and whitespace gates pass. Full feature visual evidence remains pending; plan records the user-directed height difference from Figma.

## User-directed text-field sizing restoration — 2026-10-28

- Removed compact parameter, wrapper modifier, CSS overrides and all four banner usages. Original field sizing, line height and validation spacing restored. `Lines` and textarea behavior retained for Description.
- Web build passes with 0 warnings/errors; Web scope, plan, design and whitespace gates pass. Existing typography token remains in use by the banner status label. Full feature visual evidence remains pending; plan records the user-directed field height difference from Figma.
