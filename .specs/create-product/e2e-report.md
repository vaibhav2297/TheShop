# E2E report — create-product

_Run: 2026-09-06 · commit `253433f` · verdict 🔴 NOT VERIFIED_
_Snapshot of one run — regenerate with `/theshop.e2e create-product`._

## Coverage classification

| Bucket | ACs | Count |
|---|---|---|
| Browser-proven (`e2e`) | AC-1, AC-2, AC-3, AC-4, AC-6, AC-7, AC-8, AC-9, AC-10 (incl. AC-10a), AC-11, AC-13, AC-14, AC-17, AC-19, AC-20, AC-25, AC-26, AC-29, AC-30, AC-31, AC-33, AC-34 | 22 |
| Proven below the browser (`unit`) | AC-5, AC-12, AC-15, AC-16, AC-18, AC-21, AC-22, AC-23, AC-24, AC-27, AC-28, AC-32, AC-35 | 13 |
| Human-only (`manual`) | AC-36, AC-37 | 2 |

- **AC-36** (all feature text follows the active EN/FR language): needs a human pass, or the `/theshop.review` French-localization completeness gate — exhaustive text-parity across every surface isn't a browser assertion.
- **AC-37** (full keyboard/screen-reader operability): needs a human with assistive technology to judge announcement quality and focus handling; Playwright can drive keys but not judge what was heard.

Classification and journey are unchanged from the prior run (2026-09-05) — this run re-executes the same `e2e-manifest.json` against the current commit (no test-code edits were needed).

## Journey run

Three test classes, 22 `[Fact]` methods total (20 in `CreateProductJourneyTests`, 1 each in the view-only and access-denied classes) — discovered count (22) matched the manifest's declared count, so no orphan or phantom method.

Environment repairs made before this run (none of them test-code changes):
1. Docker Desktop was not running — started it and waited for the daemon to accept connections.
2. Ran `start-e2e-env.ps1` for a full `supabase db reset` against the now-live Docker daemon (all 27 migrations applied cleanly, including `0022`-`0027`).
3. Cleared `.auth-states/*.json` since the database had just been reset, forcing fresh OTP sign-ins for all three personas.

With the environment healthy, the suite ran clean in a single pass — **no repair rounds were needed.** Both failures below were immediately classifiable as pre-existing, already-documented product defects with a confirmed root cause in `src/`, not test-code locator or timing issues, so no test edit was attempted and the `e2e` gate did not need re-running.

**Final: 22 discovered · 20 passed · 2 failed · 0 skipped.**

Notably, **AC-30 and AC-34 — reported unresolved after 3 repair rounds in the 2026-09-05 run — both passed cleanly this run**, with no test-code changes since. This confirms that run's hypothesis: the prior timeouts were transient local resource contention (the report noted 7 lingering `dotnet.exe` processes and an 8m28s wait against a 30s timeout), not a defect in `UpdateProductHandler` or `ProductForm.SaveAsync`'s edit branch.

## Acceptance criteria

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | e2e | ✅ Passed | `CreateProductJourneyTests.AC1_Admin_sees_published_and_unpublished_products_with_pagination_and_no_search_controls` |
| AC-2 | e2e | ✅ Passed | `CreateProductJourneyTests.AC2_Moving_to_the_next_page_shows_the_next_set_in_the_same_order` |
| AC-3 | e2e | ✅ Passed | `CreateProductViewOnlyJourneyTests.AC3_View_only_staff_see_no_add_or_edit_controls_and_direct_links_are_denied` |
| AC-4 | e2e | ✅ Passed | `CreateProductJourneyTests.AC4_Admin_creates_a_published_product_and_it_appears_in_the_catalogue` |
| AC-5 | unit | ⚠️ Unverified | `AddProductTests.Render_Always_PassesCreateModeAndNoInitialDataToTheForm` — uncorroborated, Test row is `Failing` |
| AC-6 | e2e | ✅ Passed | `CreateProductJourneyTests.AC6_Edit_form_opens_prefilled_with_everything_saved` |
| AC-7 | e2e | ✅ Passed | `CreateProductJourneyTests.AC7_Gallery_images_can_be_ordered_and_removed_and_a_removed_image_stops_being_offered` |
| AC-8 | e2e | ✅ Passed | `CreateProductJourneyTests.AC8_An_invalid_image_is_refused_while_a_large_batch_of_valid_images_is_accepted` |
| AC-9 | e2e | ✅ Passed | `CreateProductJourneyTests.AC9_Two_option_types_generate_exactly_four_labelled_variants_with_skus` |
| AC-10 (incl. AC-10a) | e2e | ✅ Passed | `CreateProductJourneyTests.AC10_Variant_rows_show_price_sale_price_pinned_image_and_unavailable_status` |
| AC-11 | e2e | ✅ Passed | `CreateProductJourneyTests.AC11_Product_and_variant_skus_regenerate_live_with_no_editable_sku_control` |
| AC-12 | unit | ⚠️ Unverified | `ProductTests.ApplyVariantConfiguration_WhenAValueIsAdded_KeepsEveryExistingVariantsConfiguration` — uncorroborated, Test row is `Failing` |
| AC-13 | e2e | ✅ Passed | `CreateProductJourneyTests.AC13_Removing_an_option_value_confirms_the_discard_count_and_leaves_the_gallery_untouched` |
| AC-14 | e2e | ✅ Passed | `CreateProductJourneyTests.AC14_Removing_the_last_option_type_restores_the_product_level_price_field` |
| AC-15 | unit | ⚠️ Unverified | `ProductTests.ApplyVariantConfiguration_WithTwoOptionTypesOfTwoValuesEach_GeneratesFourVariants` — uncorroborated, Test row is `Failing` |
| AC-16 | unit | ⚠️ Unverified | `ProductMapperTests.ToDomain_WithNoMinVariantPriceColumnAndNoEmbeddedVariants_MapsHasVariantsFalse` — uncorroborated, Test row is `Failing`; also does not exercise RULE-16's stock clause (see Findings) |
| AC-17 | e2e | ✅ Passed | `CreateProductJourneyTests.AC17_The_form_states_customers_pay_the_lowest_variant_price_once_variants_exist` |
| AC-18 | unit | ⚠️ Unverified | `ProductTests.Product_CarriesNoDedicatedFlavourOrNicotineProperty` — uncorroborated, Test row is `Failing` |
| AC-19 | e2e | ❌ Failed | `CreateProductJourneyTests.AC19_A_minimal_draft_saves_as_unpublished_and_reopens_exactly_as_left` — real product defect, see Findings |
| AC-20 | e2e | ✅ Passed | `CreateProductJourneyTests.AC20_Publishing_an_incomplete_product_is_refused_listing_what_is_missing` |
| AC-21 | unit | ⚠️ Unverified | `CreateProductCommandValidatorTests.Validate_WithBlankName_HasNameRequiredError` — uncorroborated, Test row is `Failing` |
| AC-22 | unit | ⚠️ Unverified | `CreateProductHandlerTests.Handle_WithADuplicateName_FailsWithNameAlreadyExists` — uncorroborated, Test row is `Failing` |
| AC-23 | unit | ⚠️ Unverified | `AddProductTests.Render_LoadsActiveCategoriesAndBrandsIntoTheForm` — uncorroborated, Test row is `Failing` |
| AC-24 | unit | ⚠️ Unverified | `EditProductTests.Render_WhenTheAssignedCategoryHasSinceBeenDeactivated_StillOffersItInTheForm` — uncorroborated, Test row is `Failing` |
| AC-25 | e2e | ✅ Passed | `CreateProductJourneyTests.AC25_A_sale_price_at_or_above_the_price_is_refused_and_a_valid_one_shows_the_discount` |
| AC-26 | e2e | ❌ Failed | `CreateProductJourneyTests.AC26_No_stock_quantity_control_exists_on_the_product_or_variant_rows` — real product defect, see Findings |
| AC-27 | unit | ⚠️ Unverified | `SupabaseProductAdminSchemaTests.InsertSkuRegistryRow_WithASkuAlreadyUsedByAnotherProduct_ThrowsUniqueViolation` — uncorroborated, Test row is `Failing` |
| AC-28 | unit | ⚠️ Unverified | `ProductOptionTypeTests.Create_WithDuplicateValuesIgnoringCaseAndSpaces_ThrowsDuplicateOptionValueException` — uncorroborated, Test row is `Failing` |
| AC-29 | e2e | ✅ Passed | `CreateProductJourneyTests.AC29_A_published_product_whose_every_variant_is_unavailable_shows_out_of_stock` |
| AC-30 | e2e | ✅ Passed | `CreateProductJourneyTests.AC30_Unpublishing_hides_a_product_from_customers_while_keeping_it_listed_for_staff` (previously unresolved after 3 repair rounds — now passes cleanly, see Findings) |
| AC-31 | e2e | ✅ Passed | `CreateProductJourneyTests.AC31_Leaving_the_form_with_unsaved_changes_warns_before_discarding_them` |
| AC-32 | unit | ⚠️ Unverified | `ProductFormTests.Save_WhenTheSaveFailsForATechnicalReason_ShowsAnErrorSnackbarAndDoesNotNavigate` — uncorroborated, Test row is `Failing` |
| AC-33 | e2e | ✅ Passed | `CreateProductAccessDeniedJourneyTests.AC33_A_user_without_the_view_permission_cannot_reach_the_list` |
| AC-34 | e2e | ✅ Passed | `CreateProductJourneyTests.AC34_A_stale_edit_link_shows_not_found_while_a_renamed_products_link_still_opens_it` (previously unresolved after 3 repair rounds — now passes cleanly, see Findings) |
| AC-35 | unit | ⚠️ Unverified | `ManageProductsTests.Render_WhenNoProductsExistYet_ShowsTheEmptyStateMessage` — uncorroborated, Test row is `Failing` |
| AC-36 | manual | ⚠️ Unverified | Needs a human / the review step's French-completeness gate |
| AC-37 | manual | ⚠️ Unverified | Needs a human with assistive technology |

**AC status:** 18 passed · 2 failed · 17 unverified (13 `unit` ACs uncorroborated because the `4. Test` ledger row is `Failing`; AC-36/AC-37 manual)

## Failures and findings

### AC-19 — real product defect (RULE-15 / FR-8 violated), unchanged from the 2026-09-05 run
`CreateProductJourneyTests.AC19_...`. The "Save" button stays permanently disabled when only name, category, and brand are filled and the status is left Unpublished — RULE-15 requires exactly four values at every save (name, SKU, category, brand) and makes price optional for a draft. Re-confirmed by source this run: `src/TheShop.Web/Components/Products/ProductForm.razor` line 162-163 still declares the product's own `ShopMoneyField` `Required="true"` unconditionally, with no gate on publish status or on whether the product has variants. `MudForm`'s validity therefore never turns true for a priceless draft. Fix belongs in `ProductForm.razor` (make `Required` follow RULE-14/RULE-15's "required only when publishing, and only for a no-variant product"), not in this journey. Trace: `AC19_A_minimal_draft_saves_as_unpublished_and_reopens_exactly_as_left.zip`.

### AC-26 — real product defect (FR-8 / RULE-6 / RULE-8 / RULE-14 / RULE-16 entirely unimplemented), unchanged from the 2026-09-05 run
`CreateProductJourneyTests.AC26_...`. No stock-quantity input exists anywhere on the product form or a variant row — re-confirmed by both the running assertion (0 stock controls found) and a direct source read this run: `src/TheShop.Domain/Entities/Product.cs` still carries no `StockQuantity` property, and `supabase/migrations/0026_remove_product_stock.sql` (still present, unreverted) dropped `stock_quantity` from both `products` and `product_variants`. This is the same already-documented gap named in `.specs/create-product/status.md`'s Test and Verify rows from the prior run. Fix requires re-adding stock tracking through Domain → Application → Infrastructure → Web, not a test change.

### AC-16 — unit evidence doesn't actually cover RULE-16's stock clause (adjacent to the AC-26 gap, unchanged)
Not a failure in itself. `Product.IsInStock => !HasVariants || HasSellableVariant` means a no-variant product is unconditionally `true` — there is no code path by which such a product could ever be "out of stock" now that the backing stock quantity is gone. Same root defect as AC-26.

### AC-30 and AC-34 — now pass; prior 3-round-unresolved timeouts confirmed as transient, not a product defect
Both tests failed with a save-button/navigation timeout in the 2026-09-05 run after three repair rounds, with a hypothesis that the edit-path save (`UpdateProductHandler` / `ProductForm.SaveAsync`'s edit branch) was slow or stuck. This run — same test code, same commit, freshly reset environment with Docker cold-started — **both passed in 34s and 42s respectively**, with no changes to `src/` or the tests between runs. This corroborates the prior report's own hypothesis that the failures were caused by transient local resource contention (that run recorded 7 lingering `dotnet.exe` processes and an 8m28s wait against a 30s timeout) rather than a defect in the edit save path. No further action needed on this pair; if it recurs, capture a trace under a clean environment before suspecting the handler.

## Environment

- Stack: **cold-started this run** — Docker Desktop was not running at the start of this invocation; started it, waited for the daemon, then ran `start-e2e-env.ps1` (full `supabase db reset`, all 27 migrations applied cleanly) · app: launched by `AppHostFixture` · port 5218 was left LISTENING by an orphaned `dotnet.exe` (PID 23812) after the run — stopped manually; confirmed free afterward
- `.auth-states/*.json` cleared proactively before the run since the database had just been reset (stale sessions would otherwise force a mid-run re-auth)
- Supabase stack left running — `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1` to stop it

## Verdict

**🔴 NOT VERIFIED**

Two independent reasons, either one sufficient on its own: (1) two `e2e` ACs failed for real — AC-19 and AC-26 are confirmed, unchanged product defects, both re-verified against current source this run; and (2) `.specs/create-product/status.md`'s `4. Test` row is still `Failing` (AC-26 uncovered by the unit suite), which per this gate's own rule makes every `unit`-bucketed AC's corroboration unavailable regardless of how green the browser run is.

The good news this run: the two previously-unresolved edit-path timeouts (AC-30, AC-34) are no longer reproducing and are very likely a resolved false alarm from environment contention, not a code defect — no further investigation of `UpdateProductHandler` is warranted on that basis alone.

**Next step:** re-add stock-quantity tracking (Domain → Application → Infrastructure → Web, per `status.md`'s own next-step note) to resolve AC-19 and AC-26 at the root, then fix `ProductForm.razor`'s unconditional `Required="true"` on the product price field once stock/price rules are back in place; then re-run `/theshop.test-merged create-product` (flips the Test row to `Passing`, corroborating the 13 unit ACs) followed by `/theshop.e2e create-product` again.
