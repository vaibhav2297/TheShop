# E2E report — product-description

_Run: 2026-09-12 · commit `1b51324` · verdict ✅ VERIFIED_
_Snapshot of one run — regenerate with `/theshop-e2e product-description`._

## Coverage classification

| Bucket | ACs | Count |
|---|---|---|
| Browser-proven (`e2e`) | AC-1, AC-2, AC-6, AC-10, AC-11, AC-13 | 6 |
| Proven below the browser (`unit`) | AC-3, AC-4, AC-5, AC-7, AC-8, AC-9, AC-12, AC-14, AC-15 | 9 |
| Human-only (`manual`) | — | 0 |

## Journey run

`TheShop.E2E.Tests.Journeys.ProductDescriptionJourneyTests` — expected 6 · discovered 6 · passed 6 · failed 0 · skipped 0 · repair rounds used 3 · total time ≈ 9 min across all runs this session.

Round 1 (environment): full run, all 6 fail at page-load locators. Cause: `.auth-states/e2e-admin.json` cached from before this session's `supabase db reset`. Cleared it. No `src/`, test, or migration edit.

Round 2 (verification rerun, no edits): full suite rerun. AC-11 now passes for real — migration `supabase/migrations/0030_product_description_bounds.sql` (plan §10, TASK-023), missing in the prior report, has since been authored and applies clean on `db reset`. AC-2, AC-6, AC-10, AC-13 pass. AC-1 fails on the closing locator: `Timeout 15000ms exceeded … waiting for Locator(".shop-rich-text-editor .ql-editor").Locator("ul li a[href='https://example.com/vape-care']")…`.

Round 3 (locator fix, in `tests/TheShop.E2E.Tests/Journeys/ProductDescriptionJourneyTests.cs` only — no `src/` or migration edit): user manually reopened the same product and saw the bullet-list link render correctly, contradicting the prior report's "product defect" classification. Reran AC1 with a temporary diagnostic dump of the editor's actual `innerHTML` on failure, which surfaced the real cause: **test bug, not a product defect.** Quill 2.x renders every list — ordered or bullet — as a single `<ol>`, distinguishing them only by `data-list="ordered"|"bullet"` on the `<li>` (CSS then draws the glyph); there is never a real `<ul>` in the DOM. The stored data and the reopened render were both correct the whole time (`<li data-list="bullet">…<a href="https://example.com/vape-care" rel="noopener noreferrer" target="_blank">Bullet item with a link</a></li>`) — the test's `ul li a[...]` locator could never match. Fixed the locator to `li[data-list='bullet'] a[href='...']` and the ordered-item locator to `li[data-list='ordered']` for consistency; removed the diagnostic scaffolding. Reran the full 6-test suite: **6/6 pass.**

## Acceptance criteria

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | e2e | ✅ Passed | `ProductDescriptionJourneyTests.AC1_Creating_with_every_supported_format_plus_specification_rows_reopens_with_the_same_content` |
| AC-2 | e2e | ✅ Passed | `ProductDescriptionJourneyTests.AC2_Editing_description_and_specification_rows_reopens_with_the_edits_and_leaves_unrelated_details_unchanged` |
| AC-3 | unit | ✅ Passed | `ProductDescriptionTests.Rehydrate_WithLiteralAngleBracketsSurvivingAsEscapedText_KeepsThemAsText` |
| AC-4 | unit | ✅ Passed | `SupabaseProductDescriptionSchemaTests.UpdateProductDescription_AtExactly200000Bytes_Succeeds` |
| AC-5 | unit | ✅ Passed | `SupabaseProductDescriptionSchemaTests.UpdateProductDescription_Over200000Bytes_ThrowsPostgresException` |
| AC-6 | e2e | ✅ Passed | `ProductDescriptionJourneyTests.AC6_Removing_all_description_text_and_every_specification_row_persists_as_empty_after_reopening` |
| AC-7 | unit | ✅ Passed | `SupabaseProductDescriptionSchemaTests.InsertSpecification_WithABlankName_ThrowsPostgresException` |
| AC-8 | unit | ✅ Passed | `SupabaseProductDescriptionSchemaTests.InsertSpecification_WithADuplicateNameIgnoringCaseAndSpaces_ThrowsUniqueViolation` |
| AC-9 | unit | ✅ Passed | `SupabaseProductDescriptionSchemaTests.InsertSpecification_AsAuthenticatedUserWithoutProductsCreateOrEdit_IsDeniedByRls` |
| AC-10 | e2e | ✅ Passed | `ProductDescriptionJourneyTests.AC10_Pasting_styled_text_keeps_supported_emphasis_drops_unsupported_styling_and_shows_the_notice` |
| AC-11 | e2e | ✅ Passed | `ProductDescriptionJourneyTests.AC11_Calling_save_product_directly_with_markup_outside_the_grammar_is_rejected_and_leaves_no_row_changed` |
| AC-12 | unit | ✅ Passed | `ProductDescriptionLocalizationTests.UiString_ForEveryNewKey_IsAvailableInEnglishAndFrench` |
| AC-13 | e2e | ✅ Passed | `ProductDescriptionJourneyTests.AC13_Formatting_and_specification_controls_are_keyboard_operable_and_row_removal_moves_focus_and_announces` |
| AC-14 | unit | ✅ Passed | `ProductFormTests.EditingTheDescription_ConfirmsExternalNavigation` |
| AC-15 | unit | ✅ Passed | `ProductFormTests.Save_WhenTheSaveFails_PreservesTheEnteredDescriptionAndSpecificationRows` |

**AC status:** 15 passed · 0 failed · 0 unverified

## Failures and findings

None. Prior finding on migration `0030` resolved (authored, confirmed enforced). Prior finding on the reopened bullet-list link was a test locator bug (`ul li a[...]` against Quill's real `li[data-list='bullet'] a[...]` DOM), not a product defect — fixed in test code this run, no `src/` change needed. No missing `data-testid` hooks. e2e static gate (`check-sdd-gates.ps1 e2e`) clean after the fix.

## Environment

- Stack: reused (already running from a prior session) · `supabase db reset` run this session via `tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1` · app: launched by `dotnet test`'s `AppHostFixture` · port 5218 free after every run this session
- Supabase stack left running — `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1` to stop it.
- `.auth-states/e2e-admin.json` cleared mid-run (stale from before this session's `db reset`), re-minted through the real OTP flow. Expected, no follow-up needed.

## Verdict

**✅ VERIFIED**

6/6 browser-proven ACs pass, all 9 unit ACs corroborated by the passing unit suite (`status.md` Test row: Passing), no manual ACs, e2e gate clean. Next step: `/theshop-review product-description`.
