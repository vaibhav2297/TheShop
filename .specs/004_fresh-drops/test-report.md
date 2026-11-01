# Fresh Drops test report

**Date:** 2026-10-31  
**Commit:** `0732ce0`  
**Filter:** `Feature=004_fresh-drops`  
**Verdict:** ✅ PASSING

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| `TheShop.Domain.Tests` | 7 | 7 | 7 | 0 | 0 |
| `TheShop.Application.Tests` | 29 | 29 | 29 | 0 | 0 |
| `TheShop.Infrastructure.Tests` | 3 | 3 | 3 | 0 | 0 |
| `TheShop.Web.Tests` | 12 | 12 | 12 | 0 | 0 |
| **Total** | **51** | **51** | **51** | **0** | **0** |

**Gates:** manifest clean; compile clean; four filtered runs passed. PostgreSQL tests used Testcontainers with local Docker access.

## Acceptance evidence

| AC | Non-browser proof | Browser boundary |
|---|---|---|
| AC-1 | Add defaults and newest cards | — |
| AC-2 | Edit values, heading, manual table | — |
| AC-3 | View permission denial | Guest redirect and route denial |
| AC-4 | Combined permissions at pipeline/RPC; view-only UI | — |
| AC-5 | Published-only, newest ordering and table | Publish/unpublish refresh |
| AC-6 | Dialog confirm/cancel; unsaved mode restoration | — |
| AC-7 | Domain limit; dialog fourth disabled; move/remove order | Pointer drag |
| AC-8 | No-results guidance retains selection | — |
| AC-9 | Blank title, paired button, blank pair, heading fallback | — |
| AC-10 | Safe/unsafe destination validation | — |
| AC-11 | Update order/status and failure; RPC write | Save/reopen persistence |
| AC-12 | Cancel link and no Add dispatch | Navigation and Edit cancellation |
| AC-13 | Database singleton and hidden row | — |
| AC-14 | Failed load retry and denied read | Permission loss while page open |
| AC-15 | Table headers and accessible controls | Viewports, keyboard focus, overflow |

Full AC-to-test FQNs and boundary reasons are in `test-manifest.json`.

## Resolved run issues

- `FreshDropsHandlerTests.Read_DeniedRepositoryCall_DoesNotPretendConfigurationAbsent` first failed with `InvalidOperationException: Cannot access Value of a failed Result.` The test now checks the failure key; rerun passed.
- The first PostgreSQL run could not access the sandboxed Docker named pipe. The approved local Docker run passed all three tests.

**Open test failures:** none. Figma visual evidence and browser journeys remain outside this Test stage.
