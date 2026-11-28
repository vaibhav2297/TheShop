# Homepage Shop by Brand — Test Report

**Feature:** `008_homepage-brand`  
**Date:** 2026-11-28  
**Commit:** `abf3292`  
**Verdict:** ✅ PASSING

## Run

Filter: `Feature=008_homepage-brand`; logger: `console;verbosity=normal`; `--no-build`. Manifest gate passed. All four manifested projects built.

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| `TheShop.Domain.Tests` | 5 | 5 | 5 | 0 | 0 |
| `TheShop.Application.Tests` | 11 | 11 | 11 | 0 | 0 |
| `TheShop.Infrastructure.Tests` | 6 | 6 | 6 | 0 | 0 |
| `TheShop.Web.Tests` | 15 | 15 | 15 | 0 | 0 |
| **Total** | **37** | **37** | **37** | **0** | **0** |

## AC evidence

| AC | Test evidence |
|---|---|
| AC-1 | `Add_OpensWithEmptySelectionAndEditablePrefilledFields` |
| AC-2 | `Edit_RestoresSavedFieldsOrderLogoAndHiddenStatus` |
| AC-3 | `Picker_ApplyChangesDraftButDismissKeepsPreviousSelection`; `Choices_ActiveBrandsKeepLogoAndNameOnlyChoice` |
| AC-4 | `FullSelection_DisablesAddUntilOneRemoved`; `Picker_AtSixSelections_DisablesSeventhUntilOneRemoved`; domain boundary test |
| AC-5 | `Edit_MoveAndSave_SendsAllChangedFieldsInNewOrder`; parameterized RPC test. Reopen: E2E. |
| AC-6 | Hidden/visible domain and validator tests; inactive handler rejection; schema selection rules; hidden Create component test |
| AC-7 | Required/unsafe domain and validator tests; inline error component test |
| AC-8 | Changed Edit command and PostgreSQL RPC tests. Reopen: E2E. |
| AC-9 | `Cancel_UnsavedChange_OffersNavigationWithoutSaving`. Persisted cancellation: E2E. |
| AC-10 | `Edit_PendingLoadShowsSkeletonThenFailureAllowsRetry` |
| AC-11 | Authorization behavior, PostgreSQL RLS, and view-only component tests. Guest journey: E2E. |
| AC-12 | Component control state and actions. Responsive, focus, keyboard, and overflow: E2E. |
| AC-13 | Active-only picker, search, logo/name, and choices handler tests |
| AC-14 | Picker apply/dismiss component tests |
| AC-15 | No-results, pending skeleton, and retry picker tests |

## Failures

None in final runs. The first infrastructure run inside the sandbox could not access Docker's `npipe://./pipe/docker_engine` (`UnauthorizedAccessException`) for three `HomepageBrandSchemaTests`. The authorized rerun passed all six infrastructure tests.

**Next:** `$theshop-e2e 008_homepage-brand`
