# Manage Hero Banner — Test Report

**Feature:** `003_manage-hero-banner`  
**Date:** 2026-10-28  
**Commit:** `fe8dc54` plus current feature changes  
**Verdict:** ✅ PASSING

## Results

| Filtered project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| TheShop.Domain.Tests | 4 | 4 | 4 | 0 | 0 |
| TheShop.Application.Tests | 19 | 19 | 19 | 0 | 0 |
| TheShop.Infrastructure.Tests | 4 | 4 | 4 | 0 | 0 |
| TheShop.Web.Tests | 8 | 8 | 8 | 0 | 0 |
| **Total** | **35** | **35** | **35** | **0** | **0** |

Filter for each project: `Feature=003_manage-hero-banner`. Manifest gate clean. All four manifested projects built. Tests ran with `--no-build --logger "console;verbosity=normal"`; no full suite ran. PostgreSQL tests applied migrations 0034 and 0035 inside an isolated Testcontainers database, then rolled back test rows. Docker access required an unsandboxed rerun; the first sandboxed attempt could not access `npipe://./pipe/docker_engine`. Final rerun passed.

## Acceptance evidence

Exact test files, FQNs and method counts: [test-manifest.json](test-manifest.json).

| AC | Filtered evidence | Remaining boundary |
|---|---|---|
| AC-1 | `ManageHeroBannerTests.Render_AbsentAndCreatePermission_ShowsAddFormWithVisibleDefault` | — |
| AC-2 | `ManageHeroBannerTests.Render_ExistingAndEditPermission_LoadsValuesAndHiddenStatus` | — |
| AC-3 | `HeroBannerSchemaTests.ViewOnlyUser_CanReadButCannotSave`; `HeroBannerSchemaTests.CreateOnlyUser_CanSaveButCannotReadManagementOrEdit` | Guest redirect and denied route: E2E |
| AC-4 | `ManageHeroBannerTests` existing and absent View-only tests; `HeroBannerSchemaTests` action-only tests | — |
| AC-5 | — | Native file selection, drag/drop and previews: E2E |
| AC-6 | `HeroBannerValidationTests` size, ratio, MIME/decode and 5 MB tests | Browser decode and guidance: E2E |
| AC-7 | `HeroBannerValidationTests.Create_MissingHeadingAndDesktop_ReturnsBothFieldErrors`; `HeroBannerTests.Create_MissingRequiredContent_RejectsIt` | — |
| AC-8 | `HeroBannerTests.ChangeContent_IncompleteButtonPair_PreservesPreviousValues`; validation test | — |
| AC-9 | `HeroBannerValidationTests.Create_UnsafeDestinationsAndIncompleteButtonPair_RejectsFields`; editor hydration test | — |
| AC-10 | `ManageHeroBannerTests` Create/Edit save tests; create/read handlers | Persisted browser reopen: E2E |
| AC-11 | `HeroBannerTests.ChangeImagesAndVisibility_RemovingMobile_PreservesDesktopAndContent`; update handler | — |
| AC-12 | Cancel href asserted in `ManageHeroBannerTests.Render_AbsentAndCreatePermission_ShowsAddFormWithVisibleDefault` | Draft disposal/navigation: E2E |
| AC-13 | `HeroBannerValidationTests.Update_RemoveDesktopWithoutReplacementRejected_ReplacementAccepted`; update handler | — |
| AC-14 | Upload/commit compensation handler tests; `ManageHeroBannerTests` load/save failure tests | — |
| AC-15 | Hidden-banner duplicate handler test; singleton schema test | — |
| AC-16 | — | Viewports, keyboard, focus and assistive technology: E2E |

No final failures, skips or count drift. Browser and visual evidence remains for `$theshop-e2e 003_manage-hero-banner`; the Execute visual gate is still pending.
