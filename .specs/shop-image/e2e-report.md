# E2E report — shop-image

_Run: 2026-09-30 · commit `0a81859` + uncommitted feature working tree · verdict ✅ VERIFIED_
_Snapshot of one run. Regenerate with `/theshop-e2e shop-image`._

## Coverage classification

| Bucket | ACs | Count |
|---|---|---|
| Browser-proven (`e2e`) | AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-12, AC-13, AC-14 | 14 |
| Proven below browser (`unit`) | — | 0 |
| Human-only (`manual`) | — | 0 |

Evidence technique:

- Production placements use real navigation: catalogue product cards, ManageCategories/ManageProducts/ManageBrands rows, app bar and footer logos, and the access-denied and sign-in redirect. Browser context answers seeded `placehold.co` URLs with deterministic tall, wide, held, or aborted SVGs. No DB rows are written.
- Six presets (ProductDetail, CategoryTile, CategoryBanner, Hero, Editorial, SocialSharing) plus MobileBanner and cart/order Thumbnail rows have no production screen (spec §1 out of scope). `ShopImageCompositionPage` renders real `ShopImage` markup with Blazor `HtmlRenderer` and mounts it into the running app page, so the app's own compiled CSS is measured. This is plan TASK-005's "test-only composition; no production demo route". No app service is resolved. Deviation: the E2E csproj comment says "constants-only reference", and this helper renders one Web component statically.

## Journey run

Expected 14 · discovered 14 · passed 14 · failed 0 · skipped 0 · repair rounds used 3 (two test defects, one product defect fixed in `src/`) · final full run ≈2 min.

## Acceptance criteria

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC1_Product_images_stay_whole_in_square_frames_for_tall_and_wide_sources` |
| AC-2 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageAdminJourneyTests.AC2_Admin_thumbnails_stay_square_whole_and_inside_their_row_space` |
| AC-3 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC3_Category_tile_photography_fills_a_square_frame_with_a_centered_crop` |
| AC-4 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC4_Desktop_category_banner_and_hero_use_16_5_and_16_9_frames_with_centered_crops` |
| AC-5 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC5_Dedicated_mobile_artwork_fills_a_4_5_frame_while_desktop_keeps_its_treatment` |
| AC-6 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC6_Without_mobile_artwork_desktop_artwork_fills_a_4_5_frame_with_a_centered_crop` |
| AC-7 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC7_Editorial_photography_fills_a_4_3_frame_without_stretching` |
| AC-8 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageAdminJourneyTests.AC8_Wide_and_tall_brand_logos_stay_whole_in_original_proportions_within_their_space` |
| AC-9 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC9_A_prepared_social_sharing_image_keeps_its_40_21_composition` |
| AC-10 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC10_Every_ratio_frame_follows_available_width_across_phone_tablet_desktop_and_rotation` |
| AC-11 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC11_Missing_or_failed_images_show_named_placeholders_without_moving_the_card` |
| AC-12 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC12_Image_descriptions_and_placeholder_labels_are_english_and_decorative_images_stay_silent` |
| AC-13 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC13_Image_linked_actions_keep_keyboard_access_names_and_visible_focus` |
| AC-14 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ShopImageAccessDeniedJourneyTests.AC14_Restricted_image_lists_keep_their_access_denied_and_sign_in_experiences` |

**AC status:** 14 passed · 0 failed · 0 unverified

## Failures and findings

### Product defect, fixed: logo links lost their accessible name (AC-13, FR-7)

- **FQN:** `TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC13_Image_linked_actions_keep_keyboard_access_names_and_visible_focus`
- **Class:** product defect. Assertion unchanged. Fixed in `src/` after the first report; AC-13 now passes.
- **Symptom (before fix):** `GetByRole(Link, Name = Strings.AppName)` expected count 2 (app bar and footer logo links). It was `0`.
- **Trace:** `tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ShopImageJourneyTests.AC13_Image_linked_actions_keep_keyboard_access_names_and_visible_focus.zip`. The snapshot shows `<a href="/"><div role="group" data-shop-image …><img alt="The Vape Shop Sarnia" …></div></a>`.
- **Corroboration:** A standalone Chromium probe queried the native AX tree with CDP `Accessibility.getPartialAXTree`. `<a><div role="group"><img alt></div></a>` gives link name `''`. `<a><div><img alt></div></a>` gives link name `'The Shop'`. Real screen readers get the empty name too. This is not Playwright strictness.
- **Cause (confirmed):** The `ShopImage` root `MudStack` emits `role="group"`. Name-from-content does not pass through a `group` descendant, so every `ShopImage` inside a link drops the link's name. Affected: `ShopAppBar` and `ShopFooter` logo links. It may also affect other placements that wrap `ShopImage` in a link. The bUnit test `ShopFooterTests.Render_Always_KeepsTheLogoLinkNamedAndTheLogoWholeInItsReservedSpace` checks markup only, so it cannot catch this.
- **Fix:** `ShopImage.RootAttributes` now adds `role="none"` to the frame root unless the consumer supplies its own `role`. `MudStack` lets `UserAttributes` override its default `group` role. New bUnit test `TheShop.Web.Tests.Components.Common.ShopImageTests.Render_Always_ExposesNoGroupRoleThatWouldHideTheAltTextFromASurroundingLink` is mapped to AC-13 in `test-manifest.json` (65 tests). Web suite: 845/845 pass. Solution build: 0 errors.
- **Related cleanup:** `AddBrand.razor` still used `Label` on two `MudTextField`s (Rule 17), although `status.md` recorded it as fixed. Both now use `Placeholder`. Locators use `data-testid`, so no journey changed. `check-design-rules -Changed` is clean.

### Test defects fixed during repair

- Round 1: Seeded catalogue image URLs come from `PlaceholderImage.For(name)`, with `text=Elf%20Bar%20…`, not the `image_url` column (`text=Elf+Bar+…`). Routes now match the decoded `text` value (`ShopImageArtwork.SeededLabel`). Before this fix, AC-1's wide case and AC-11's held/aborted cases were vacuous. They were rerun and now pass for real.
- Round 2: The failed-image placeholder label repeats the product name, so waiting on `GetByText(name)` became ambiguous. The filter wait now uses the product image's unique accessible name.

## Environment and teardown

- Docker Desktop was not running on first start (`dockerDesktopLinuxEngine` pipe missing). It was launched, and `tools/start-e2e-env.ps1` was rerun (local Supabase reset).
- Stale `.auth-states/*.json` (gitignored, minted before the reset) were cleared. Personas re-minted through real OTP sign-in.
- Fixture stopped the app. Port 5218 is free. Supabase is left running. Stop it explicitly with `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`.
- No Figma URL in plan. Visual gate not applicable.

## Verdict

✅ **VERIFIED.** Static gates are clean. All 14 E2E ACs pass with no skips. Visual gate is clean (no Figma).

**Next step:** optional `$theshop-document shop-image`, otherwise `$theshop-ship shop-image`.
