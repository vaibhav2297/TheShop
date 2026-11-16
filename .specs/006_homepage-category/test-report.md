# Homepage Category Section test report

**Date:** 2026-11-16  
**Commit:** `c37c9c4`  
**Filter:** `Feature=006_homepage-category`  
**Verdict:** ✅ PASSING

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| `TheShop.Domain.Tests` | 5 | 5 | 5 | 0 | 0 |
| `TheShop.Application.Tests` | 11 | 11 | 11 | 0 | 0 |
| `TheShop.Infrastructure.Tests` | 6 | 6 | 6 | 0 | 0 |
| `TheShop.Web.Tests` | 10 | 10 | 10 | 0 | 0 |
| **Total** | **32** | **32** | **32** | **0** | **0** |

**Gates:** manifest clean; all four manifested projects built with `--no-restore`; four `Feature=006_homepage-category` filtered runs passed. PostgreSQL schema tests ran with Testcontainers through local Docker access outside the filesystem sandbox.

## Acceptance evidence

| AC | Non-browser proof | Remaining boundary |
|---|---|---|
| AC-1 | Add defaults, Visible and empty selection in bUnit | — |
| AC-2, AC-3 | Add/Edit live heading and empty-title fallback in bUnit | — |
| AC-4 | Three selected disables Add and shows removal guidance in bUnit | — |
| AC-5 | Exact-three validation and inline error in Application/bUnit | — |
| AC-6 | Picker selects three from empty and disables a fourth; move/remove order, saved image/name restoration and ordered RPC parameters | Save/reopen and pointer drag in E2E |
| AC-7 | Blank button fields rejected by validator and Domain | — |
| AC-8 | Edit dispatches changed fields; saved values render in Edit | Save/reopen in E2E |
| AC-9 | Hidden Edit and ordered RPC round trip checks, including PostgreSQL schema | Save/reopen in E2E |
| AC-10 | Cancel link and no update dispatch | Persisted cancellation in E2E |
| AC-11 | Repository maps singleton conflict; PostgreSQL schema rejects duplicate create | Conflict navigation in E2E |
| AC-12 | Named controls and move/remove handlers in bUnit | Responsive widths, keyboard focus and assistive behavior in E2E |

Full AC-to-test FQNs and boundary reasons are in `test-manifest.json`. Shopper-facing homepage display is out of scope.

## Resolved run issue

The first Docker-backed run found a test expectation mismatch in `TheShop.Infrastructure.Tests.Persistence.HomepageCategorySchemaTests.Schema_SingletonRequiredFieldsAndExactThreeActiveIds_RejectInvalidRows`: the test expected SQLSTATE `23514` for invalid category IDs, while the active-selection trigger rejects them first with `P0001`. The invalid rows were rejected. The test now asserts the observed trigger contract, and all six Infrastructure tests passed on rerun.

**Open failures:** none. Browser journeys and Figma visual proof remain for `$theshop-e2e 006_homepage-category`. No production code was changed in this Test stage.
