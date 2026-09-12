# E2E report — product-description

_Run: 2026-09-12 · commit `1b51324` · verdict 🔴 NOT VERIFIED_
_Snapshot of one run — regenerate with `/theshop-e2e product-description`._

## Coverage classification

| Bucket | ACs | Count |
|---|---|---|
| Browser-proven (`e2e`) | AC-1, AC-2, AC-6, AC-10, AC-11, AC-13 | 6 |
| Proven below the browser (`unit`) | AC-3, AC-4, AC-5, AC-7, AC-8, AC-9, AC-12, AC-14, AC-15 | 9 |
| Human-only (`manual`) | — | 0 |

## Journey run

`TheShop.E2E.Tests.Journeys.ProductDescriptionJourneyTests` — expected 6 · discovered 6 · passed 5 · failed 1 · skipped 0 · repair rounds used 3 · total time ≈ 6 min across all runs this session.

Round 1 (environment): full run, all 6 fail at page-load locators (`GetByTestId("product-name")` / `.shop-rich-text-editor .ql-editor`). Cause: `.auth-states/e2e-admin.json` cached from before this session's `supabase db reset`. Cleared it. No `src/`, test, or migration edit.

Round 2 (verification rerun, no edits): full suite rerun. **AC-11 now passes for real.** Migration `supabase/migrations/0030_product_description_bounds.sql` (plan §10, TASK-023), missing in prior run's report, now authored, applies clean on `db reset`. Direct RPC replay of prior day's rejected payload now returns expected constraint violation instead of `200`. AC-2, AC-6, AC-10, AC-13 pass. **AC-1 fails again**, same symptom as prior day's report: `Timeout 15000ms exceeded … waiting for Locator(".shop-rich-text-editor .ql-editor").Locator("ul li a[href='https://example.com/vape-care']").Filter(new() { HasText = "Bullet item with a link" }) to be visible`, on reopen only. Five locator checks right before it in same test (h2 heading, plain paragraph, bold, italic, ordered-list item) pass. `src/TheShop.Web/Components/Common/ShopRichTextEditor.razor.cs` and `.razor` new since prior run (untracked, no diff to inspect); `src/TheShop.Web/wwwroot/js/shop-rich-text-editor.js` unchanged. No test-code edit possible or made — assertion already matches spec's grammar exactly.

Round 3 (isolated retry, no further edits): reran `AC1` alone twice via `--filter "FullyQualifiedName~AC1_..."`. First retry hit unrelated one-off flake (`GetByRole(AriaRole.Option, new() { Name = "Accessories" })` timeout during category selection, before editor ever touched — cold-app-boot render timing, not reproduced next attempt). Second retry reproduced exact same anchor-visibility failure as Round 2, confirming not a flake. Repair-round budget (3) spent. Not retried further.

## Acceptance criteria

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | e2e | ❌ Failed | `ProductDescriptionJourneyTests.AC1_Creating_with_every_supported_format_plus_specification_rows_reopens_with_the_same_content` |
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

**AC status:** 14 passed · 1 failed · 0 unverified

## Failures and findings

**1. Product defect (tie-break — classification uncertain, defaulting per skill rule) — reopened bullet-list link not visible to AC1 journey after `Save` → reopen, though saved data correct (blocks AC-1). Confirmed second time this session on fresh `supabase db reset`, same symptom as prior day's report.**
Classification: ambiguous between Locator/timing and Product defect; recorded Product defect per step 8's explicit tie-break ("unclear locator versus product defect: classify as product defect"). Symptom, reproduced twice this session (Round 2 full run, Round 3 isolated retry) and twice prior day: `Timeout 15000ms exceeded … waiting for Locator(".shop-rich-text-editor .ql-editor").Locator("ul li a[href='https://example.com/vape-care']").Filter(new() { HasText = "Bullet item with a link" }) to be visible`. Every other supported format (heading, paragraph, bold, italic, ordered list) on same reopened row resolves fine; only anchor inside bullet item fails to become visible. This command cannot query running database mid-test to re-confirm stored HTML this session (no live RPC call made), but prior day's report already confirmed byte-for-byte correct storage (`<ul><li><a href="https://example.com/vape-care" rel="noopener noreferrer" target="_blank">Bullet&nbsp;item&nbsp;with&nbsp;a&nbsp;link</a></li></ul>`) for identical journey run. Nothing about reload path changed since — `shop-rich-text-editor.js` byte-identical; `ShopRichTextEditor.razor(.cs)` new untracked files with no prior version to diff, so no regression attributable to an edit between runs. Standing hypothesis (unverified — still hypothesis): `ShopRichTextEditor`'s reload path (`quill.clipboard.dangerouslyPasteHTML(html)` in `init()`, `wwwroot/js/shop-rich-text-editor.js`) reparses stored HTML through Quill's clipboard matchers on every fresh mount; `link` format's matcher may not reconstruct `target`/`rel` attributes, or anchor's visibility, symmetrically with however toolbar's link dialog created them originally. Worth inspecting `shop-rich-text-editor.js`'s Quill `formats`/matcher setup, confirming with live reopen inspection — this command's own attempt to reproduce interactively via manually injected admin session hit unrelated session-validation rejection in standalone browser, not pursued further to stay inside repair-round budget. Fix belongs to `/theshop-implement` or direct Web-layer fix — `src/TheShop.Web/` out of `/theshop-e2e`'s edit rights. Repair-round budget (3) spent. Not retried further this run.

No missing `data-testid` hooks. e2e static gate (`check-sdd-gates.ps1 e2e`) clean.

## Environment

- Stack: reused (already running from prior session) · `supabase db reset` run this session via `tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1` · app: launched by `dotnet test`'s `AppHostFixture` · port 5218 free after every run this session
- Supabase stack left running — `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1` to stop it.
- `.auth-states/e2e-admin.json` cleared mid-run (stale from before this session's `db reset`), re-minted through real OTP flow. Expected, no follow-up needed.
- This command briefly ran `dotnet run --project src/TheShop.Web` standalone on port 5218 to attempt interactive reopen inspection outside test harness. Manually injected session rejected by app ("Please sign in again") — no interactive reproduction obtained. Process stopped, port confirmed free before final test runs above.

## Verdict

**🔴 NOT VERIFIED**

1 of 6 browser-proven ACs fails: AC-1, on reopened bullet-list link not becoming visible despite correct stored data (classified product defect by skill's tie-break rule, root cause still unconfirmed — same open finding as prior report). AC-11 now passes: migration `0030_product_description_bounds.sql` authored, grammar/size CHECK confirmed enforced against real local database. Next step: investigate `ShopRichTextEditor`'s reload path (`shop-rich-text-editor.js`, Quill `link` format's clipboard matcher) for anchor rendering on reopen, then re-run `/theshop-e2e product-description` — no test-side change expected.
