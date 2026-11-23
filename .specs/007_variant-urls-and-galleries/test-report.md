# Variant URLs and Galleries test report

**Date:** 2026-11-17  
**Commit:** `e9ebf2b`  
**Filter:** `Feature=007_variant-urls-and-galleries`  
**Verdict:** ✅ PASSING

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| `TheShop.Domain.Tests` | 5 | 5 | 5 | 0 | 0 |
| `TheShop.Application.Tests` | 9 | 9 | 9 | 0 | 0 |
| `TheShop.Infrastructure.Tests` | 4 | 4 | 4 | 0 | 0 |
| `TheShop.Web.Tests` | 5 | 5 | 5 | 0 | 0 |
| **Total** | **23** | **23** | **23** | **0** | **0** |

**Gates:** manifest clean; four manifested projects built with `--no-restore`; four filtered runs passed. PostgreSQL migration tests ran in Testcontainers with local Docker access.

## Acceptance evidence

| AC | Non-browser proof | Remaining boundary |
|---|---|---|
| AC-1, AC-8, AC-11 | Exact, parent, and legacy GUID queries select the expected variant; gallery IDs stay variant-specific | Visible URL, SKU, price, and nonvariant gallery in E2E |
| AC-2, AC-3, AC-9, AC-18, AC-19 | — | URL, scroll, history, gallery surfaces, keyboard, touch, focus, and rapid changes in E2E |
| AC-4, AC-7, AC-23 | Dialog order controls, replacement handler, and aggregate removal derive cover from the first image; sibling and product primary stay intact | Saved storefront gallery and placeholder in E2E |
| AC-5, AC-6 | Explicit bulk target result and handler append missing IDs while preserving existing order and unselected variants | Persisted admin flow in E2E |
| AC-10 | Exact variant with no assignment has empty image IDs | Placeholder and broken image fallback in E2E |
| AC-12 | PostgreSQL migration backfills a valid pin as the sole ordered assignment and creates readable routes | — |
| AC-13 | Unknown and wrong-parent slugs fail; public RLS hides unpublished route | Deleted-link browser state in E2E |
| AC-14 | Direct unavailable variant remains selected with its own image IDs | Disabled purchase control in E2E |
| AC-15 | PostgreSQL RPC rejects an unprivileged caller | — |
| AC-16 | Duplicate assignment and unknown bulk target are rejected | Foreign-image admin guidance in E2E |
| AC-17 | PostgreSQL keeps saved slugs after name edits and suffixes a collision | Saved-link browser journey in E2E |
| AC-20 | Deleting a shared product image prunes both variant assignments without deleting variants | — |
| AC-21, AC-24 | Variant upload stays in pending product gallery; dialog Cancel discards assignments; failed form Save retains staged content | Persisted retry and shared-library reuse in E2E |
| AC-22 | Resolver refuses a nonexistent completed combination | Visible feedback in E2E |

Full AC-to-test FQNs and E2E reasons are in `test-manifest.json`.

The migration tests exercise the real `0039` SQL against PostgreSQL with a minimal compatible `save_product` stub. They prove schema/backfill, route allocation, read policy, and permission rejection; the full `0032` save body and browser flows remain for integration/E2E.

**Resolved environment issue:** Docker Desktop was initially stopped, then the sandbox denied its named pipe. After starting Docker and granting Testcontainers access, all four Infrastructure tests passed.

**Open filtered-test failures:** none. This stage did not run the full suite or the E2E project. Its legacy `VariantImageDialog.CurrentImageId` reference still requires update in `$theshop-e2e`; the full solution build remains outside this stage's passing gate. No production source was changed here.
