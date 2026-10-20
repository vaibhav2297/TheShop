using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Shop Image journeys for anonymous shoppers (.specs/shop-image/spec.md §6). Production placements
/// (catalogue product cards, app bar and footer logos) are exercised through real navigation with
/// deterministic image responses; presets that have no production placement yet are measured through
/// <see cref="ShopImageCompositionPage"/> inside the running app. No database rows are written.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "shop-image")]
public sealed class ShopImageJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private const string ElfBar = "Elf Bar";
    private const string TallProduct = "Elf Bar BC5000";
    private const string WideProduct = "Elf Bar BC5000 Ultra Peach Ice";
    private const string OtherBrandProduct = "Vaporesso XROS 3";

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await ShopImageArtwork.RouteAsync(Context);
    }

    [Fact]
    public async Task AC1_Product_images_stay_whole_in_square_frames_for_tall_and_wide_sources()
    {
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, route =>
            ShopImageArtwork.SeededLabel(route.Request) == WideProduct
                ? ShopImageArtwork.FulfillAsync(route, 900, 300)
                : ShopImageArtwork.FulfillAsync(route, 300, 900));

        await ShowElfBarProductsAsync();
        foreach (var product in new[] { TallProduct, WideProduct })
        {
            var frame = ProductFrame(product);
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.SourceRatioDiffersFromFrame.Should().BeTrue($"{product}'s fixture source must not already be square");
            m.ShouldHaveRatio((1, 1), $"{product}'s product card");
            m.ShouldShowTheWholeImage($"{product}'s product card");
            m.ShouldStayInsideItsAllocation($"{product}'s product card");
        }

        // Product detail has no destination screen yet (spec §1 out of scope); the preset is still proven.
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();
        foreach (var (w, h) in new[] { (300, 900), (900, 300) })
        {
            var detail = await composition.MountAsync("product-detail", ShopImageCompositionPage.FullWidthHost,
                ShopImagePreset.SquareContain, ShopImageArtwork.Url(w, h));
            await detail.WaitForImageAsync();
            var m = await detail.MeasureAsync();

            m.ShouldHaveRatio((1, 1), $"product detail with a {w}×{h} source");
            m.ShouldShowTheWholeImage($"product detail with a {w}×{h} source");
        }
    }

    [Fact]
    public async Task AC3_Category_tile_photography_fills_a_square_frame_with_a_centered_crop()
    {
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();

        foreach (var (w, h) in new[] { (900, 300), (300, 900) })
        {
            var src = ShopImageArtwork.Url(w, h, "category");
            var tile = await composition.MountAsync("category-tile", ShopImageCompositionPage.FullWidthHost,
                ShopImagePreset.SquareCover, src, alt: "Disposables");
            await tile.WaitForImageAsync();
            var m = await tile.MeasureAsync();

            m.SourceRatioDiffersFromFrame.Should().BeTrue("the source must differ from the frame for a crop to be needed");
            m.ShouldHaveRatio((1, 1), $"category tile with a {w}×{h} source");
            m.ShouldFillWithACenteredCrop($"category tile with a {w}×{h} source");
            m.Src.Should().Be(src, "presentation crops the view only — the original asset URL is untouched");
        }
    }

    [Fact]
    public async Task AC4_Desktop_category_banner_and_hero_use_16_5_and_16_9_frames_with_centered_crops()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();

        foreach (var (preset, ratio) in new[] { (ShopImagePreset.Banner, (16, 5)), (ShopImagePreset.Hero, (16, 9)) })
        {
            var frame = await composition.MountAsync("desktop-banner", ShopImageCompositionPage.FullWidthHost,
                preset, ShopImageArtwork.Url(800, 800, "desktop"));
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.Branch.Should().Be("desktop", $"{preset} must show its desktop presentation at 1440px");
            m.SourceRatioDiffersFromFrame.Should().BeTrue("a square source must be cropped to the banner frame");
            m.ShouldHaveRatio(ratio, $"desktop {preset}");
            m.ShouldFillWithACenteredCrop($"desktop {preset}");
        }
    }

    [Fact]
    public async Task AC5_Dedicated_mobile_artwork_fills_a_4_5_frame_while_desktop_keeps_its_treatment()
    {
        var composition = new ShopImageCompositionPage(Page);
        await Page.SetViewportSizeAsync(375, 812);
        await composition.GotoAsync();

        foreach (var (preset, desktopRatio) in new[] { (ShopImagePreset.Hero, (16, 9)), (ShopImagePreset.Banner, (16, 5)) })
        {
            var desktopSrc = ShopImageArtwork.Url(1600, 900, "desktop");
            var mobileSrc = ShopImageArtwork.Url(800, 1000, "mobile");
            var frame = await composition.MountAsync("responsive", ShopImageCompositionPage.FullWidthHost,
                preset, desktopSrc, mobileSrc);

            await Page.SetViewportSizeAsync(375, 812);
            await frame.WaitForImageAsync();
            var mobile = await frame.MeasureAsync();
            mobile.ShownCount.Should().Be(1, "only one presentation may show at a time");
            mobile.Branch.Should().Be("mobile", $"{preset} must use its mobile presentation below 600px");
            mobile.Src.Should().Be(mobileSrc, "supplied mobile artwork must be preferred on mobile");
            mobile.ShouldHaveRatio((4, 5), $"mobile {preset}");
            mobile.ShouldFillWithACenteredCrop($"mobile {preset}");

            await Page.SetViewportSizeAsync(1440, 900);
            await frame.WaitForImageAsync();
            var desktop = await frame.MeasureAsync();
            desktop.ShownCount.Should().Be(1, "only one presentation may show at a time");
            desktop.Branch.Should().Be("desktop", $"{preset} must return to its desktop presentation at 1440px");
            desktop.Src.Should().Be(desktopSrc, "desktop presentation keeps the desktop artwork");
            desktop.ShouldHaveRatio(desktopRatio, $"desktop {preset}");
            desktop.ShouldFillWithACenteredCrop($"desktop {preset}");
        }
    }

    [Fact]
    public async Task AC6_Without_mobile_artwork_desktop_artwork_fills_a_4_5_frame_with_a_centered_crop()
    {
        await Page.SetViewportSizeAsync(375, 812);
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();

        foreach (var preset in new[] { ShopImagePreset.Hero, ShopImagePreset.Banner })
        {
            var desktopSrc = ShopImageArtwork.Url(1600, 900, "desktop");
            var frame = await composition.MountAsync("fallback", ShopImageCompositionPage.FullWidthHost, preset, desktopSrc);
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.Branch.Should().Be("mobile", $"{preset} must use its mobile presentation below 600px");
            m.Src.Should().Be(desktopSrc, "absent mobile artwork falls back to the desktop artwork");
            m.SourceRatioDiffersFromFrame.Should().BeTrue("16:9 desktop artwork must be cropped into the 4:5 frame");
            m.ShouldHaveRatio((4, 5), $"mobile {preset} without mobile artwork");
            m.ShouldFillWithACenteredCrop($"mobile {preset} without mobile artwork");
        }
    }

    [Fact]
    public async Task AC7_Editorial_photography_fills_a_4_3_frame_without_stretching()
    {
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();

        foreach (var (w, h) in new[] { (800, 800), (1600, 600) })
        {
            var frame = await composition.MountAsync("editorial", ShopImageCompositionPage.FullWidthHost,
                ShopImagePreset.Editorial, ShopImageArtwork.Url(w, h, "editorial"));
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.ShouldHaveRatio((4, 3), $"editorial card with a {w}×{h} source");
            m.ShouldFillWithACenteredCrop($"editorial card with a {w}×{h} source");
        }
    }

    [Fact]
    public async Task AC9_A_prepared_social_sharing_image_keeps_its_40_21_composition()
    {
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();

        foreach (var (w, h) in new[] { (1200, 630), (800, 800) })
        {
            var frame = await composition.MountAsync("social", ShopImageCompositionPage.FullWidthHost,
                ShopImagePreset.SocialSharing, ShopImageArtwork.Url(w, h, "social"));
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.ShouldHaveRatio((40, 21), $"social sharing image with a {w}×{h} source");
            m.ShouldShowTheWholeImage($"social sharing image with a {w}×{h} source");
        }
    }

    [Fact]
    public async Task AC10_Every_ratio_frame_follows_available_width_across_phone_tablet_desktop_and_rotation()
    {
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, route => ShopImageArtwork.FulfillAsync(route, 400, 400));

        // Phone, tablet, desktop, both sides of the 600px breakpoint, and a phone rotated to landscape.
        var viewports = ShopImageExpectations.Widths.Select(w => (w, 900))
            .Concat([(599, 900), (600, 900), (812, 375)]);

        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();
        foreach (var (vw, vh) in viewports)
        {
            await Page.SetViewportSizeAsync(vw, vh);
            foreach (var preset in ShopImageExpectations.RatioPresets)
            {
                var frame = await composition.MountAsync("responsive-width", ShopImageCompositionPage.FullWidthHost,
                    preset, ShopImageArtwork.Url(1000, 700, "photo"));
                await frame.WaitForImageAsync();
                var m = await frame.MeasureAsync();
                var subject = $"{preset} at {vw}×{vh}";

                m.Width.Should().BeApproximately(m.AllocationWidth - 32, 1.0, $"{subject} must take the full available width");
                m.ShouldHaveRatio(ShopImageExpectations.ExpectedRatio(preset, vw), subject);
                m.ShouldStayInsideItsAllocation(subject);
            }
        }

        await ShowElfBarProductsAsync();
        foreach (var width in ShopImageExpectations.Widths)
        {
            await Page.SetViewportSizeAsync(width, 900);
            var frame = ProductFrame(TallProduct);
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.ShouldHaveRatio((1, 1), $"catalogue product card at {width}px");
            m.ShouldStayInsideItsAllocation($"catalogue product card at {width}px");
        }
    }

    [Fact]
    public async Task AC11_Missing_or_failed_images_show_named_placeholders_without_moving_the_card()
    {
        var arrival = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, async route =>
        {
            var label = ShopImageArtwork.SeededLabel(route.Request);
            if (label == WideProduct)
            {
                await route.AbortAsync(); // unviewable image
                return;
            }
            if (label == TallProduct)
                await arrival.Task; // image arrives only when the test releases it
            await ShopImageArtwork.FulfillAsync(route, 300, 900);
        });

        try
        {
            await ShowElfBarProductsAsync();

            // Arrival: the frame and the card's actions and text are placed before the image exists.
            var pending = ProductFrame(TallProduct);
            var wishlist = pending.Root.Locator("xpath=..").GetByRole(AriaRole.Button, new() { Name = Strings.Wishlist_Add });
            var name = Page.GetByText(TallProduct, new() { Exact = true });
            await name.WaitForAsync();
            var before = await pending.MeasureAsync();
            var wishlistBefore = await wishlist.BoundingBoxAsync();
            var nameBefore = await name.BoundingBoxAsync();
            before.ShouldHaveRatio((1, 1), "a product card whose image has not arrived");

            arrival.SetResult();
            await pending.WaitForImageAsync();
            var after = await pending.MeasureAsync();
            (after.Left, after.Top, after.Width, after.Height).Should().Be((before.Left, before.Top, before.Width, before.Height),
                "image arrival must not resize or move its frame");
            (await wishlist.BoundingBoxAsync()).Should().BeEquivalentTo(wishlistBefore, "image arrival must not move the card's actions");
            (await name.BoundingBoxAsync()).Should().BeEquivalentTo(nameBefore, "image arrival must not move the card's text");

            // Failure: the product name replaces the image inside the unchanged square frame.
            var failed = ProductFrame(WideProduct);
            await failed.WaitForPlaceholderAsync();
            var m = await failed.MeasureAsync();
            m.IsImage.Should().BeFalse("an unviewable image must be replaced, never shown broken");
            m.PlaceholderText.Should().Be(WideProduct, "the placeholder names the product");
            m.ShouldHaveRatio((1, 1), "a failed product card image");
            m.MediaFillsFrame.Should().BeTrue("the placeholder occupies the same frame the image would have");
            await Assertions.Expect(failed.Root.Locator("xpath=..").GetByRole(AriaRole.Button, new() { Name = Strings.Wishlist_Add }))
                .ToBeVisibleAsync();
        }
        finally
        {
            arrival.TrySetResult();
        }

        // Missing sources for every treatment keep the preset's proportions; a long label cannot grow the frame.
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();
        var longLabel = string.Join(' ', Enumerable.Repeat("Extremely long product name", 12));
        foreach (var preset in ShopImageExpectations.RatioPresets)
        {
            var frame = await composition.MountAsync("missing", ShopImageCompositionPage.FullWidthHost,
                preset, src: null, placeholderLabel: longLabel);
            await frame.WaitForPlaceholderAsync();
            var missing = await frame.MeasureAsync();

            missing.PlaceholderText.Should().Be(longLabel, $"missing {preset} names its content");
            missing.ShouldHaveRatio(ShopImageExpectations.ExpectedRatio(preset, (int)missing.ViewportWidth), $"missing {preset}");
            missing.MediaFillsFrame.Should().BeTrue($"missing {preset}'s placeholder fills the reserved frame");
        }

        const string logoHost = "width:160px;height:80px;position:absolute;left:16px;top:16px;z-index:10000";
        var logo = await composition.MountAsync("missing-logo", logoHost, ShopImagePreset.BrandLogo, src: null, placeholderLabel: ElfBar);
        await logo.WaitForPlaceholderAsync();
        var missingLogo = await logo.MeasureAsync();
        (missingLogo.Width, missingLogo.Height).Should().Be((160d, 80d), "a logo placeholder fits the allocated logo space");
        missingLogo.PlaceholderText.Should().Be(ElfBar);
    }

    [Fact]
    public async Task AC12_Image_descriptions_and_placeholder_labels_are_english_and_decorative_images_stay_silent()
    {
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, route =>
            ShopImageArtwork.SeededLabel(route.Request) == WideProduct ? route.AbortAsync() : ShopImageArtwork.FulfillAsync(route, 400, 400));

        await ShowElfBarProductsAsync();

        // Meaningful: English description built from the typed resource, product name unchanged.
        var loaded = ProductImage(TallProduct);
        await Assertions.Expect(loaded).ToHaveAttributeAsync("alt", string.Format(Strings.Product_ImageAlt, TallProduct));

        // Unavailable: the placeholder keeps the same description and shows the proper name as-is.
        var failed = ProductFrame(WideProduct);
        await failed.WaitForPlaceholderAsync();
        await Assertions.Expect(ProductImage(WideProduct)).ToBeVisibleAsync();
        (await failed.MeasureAsync()).PlaceholderText.Should().Be(WideProduct);

        // Decorative: neither the image nor its placeholder adds anything for assistive technology.
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();
        foreach (var src in new[] { ShopImageArtwork.Url(400, 300, "decor"), null })
        {
            var frame = await composition.MountAsync("decorative", ShopImageCompositionPage.FullWidthHost,
                ShopImagePreset.Editorial, src, alt: "", placeholderLabel: ElfBar);
            if (src is null) await frame.WaitForPlaceholderAsync(); else await frame.WaitForImageAsync();

            (await composition.Host("decorative").GetByRole(AriaRole.Img).CountAsync()).Should().Be(0,
                $"a decorative {(src is null ? "placeholder" : "image")} must add no screen-reader description");
        }
    }

    [Fact]
    public async Task AC13_Image_linked_actions_keep_keyboard_access_names_and_visible_focus()
    {
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, route => ShopImageArtwork.FulfillAsync(route, 400, 400));
        await ShowElfBarProductsAsync();

        var logoLinks = Page.GetByRole(AriaRole.Link, new() { Name = Strings.AppName, Exact = true });
        await Assertions.Expect(logoLinks).ToHaveCountAsync(2); // app bar + footer logos keep their accessible name
        foreach (var frame in await Page.Locator(ShopImageFrame.FrameSelector).AllAsync())
            (await new ShopImageFrame(frame).MeasureAsync()).FocusableCount.Should().Be(0, "an image frame adds no keyboard stop");

        // Walk the page's tab order from the top until the footer logo link: no stop lands inside an
        // image, and each logo link shows visible keyboard focus.
        await Page.Mouse.ClickAsync(1, 1);
        var reachedLogos = 0;
        var reachedWishlist = false;
        for (var stop = 0; stop < 150 && reachedLogos < 2; stop++)
        {
            await Page.Keyboard.PressAsync("Tab");
            var focus = await Page.EvaluateAsync<FocusedElement>("""
                () => {
                    const el = document.activeElement;
                    return {
                        InsideImage: !!el?.closest('[data-shop-image]'),
                        FocusVisible: !!el?.matches(':focus-visible'),
                        Label: el?.getAttribute('aria-label') ?? '',
                    };
                }
                """);
            focus.InsideImage.Should().BeFalse($"tab stop {stop} must not land inside an image");
            reachedWishlist |= focus.Label == Strings.Wishlist_Add;

            if (await logoLinks.Nth(reachedLogos).EvaluateAsync<bool>("el => el === document.activeElement"))
            {
                focus.FocusVisible.Should().BeTrue($"logo link {reachedLogos + 1} must show visible keyboard focus");
                reachedLogos++;
            }
        }
        reachedLogos.Should().Be(2, "both logo links must be reachable by keyboard");
        reachedWishlist.Should().BeTrue("product card actions beside the image stay reachable by keyboard");

        // Existing outcome: activating the focused footer logo link goes home.
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Home, new() { Timeout = 15_000 });
    }

    private async Task ShowElfBarProductsAsync()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Brand);
        await catalogue.ToggleFilterOptionAsync(ElfBar);
        // The image (or its named placeholder) is unique per card; the bare name also appears in a placeholder label.
        await ProductImage(WideProduct).WaitForAsync(new() { Timeout = 15_000 });
        // The filtered list has replaced the unfiltered one once another brand's product is gone.
        await Assertions.Expect(catalogue.ProductName(OtherBrandProduct)).ToHaveCountAsync(0, new() { Timeout = 15_000 });
    }

    private ILocator ProductImage(string product) =>
        Page.GetByRole(AriaRole.Img, new() { Name = string.Format(Strings.Product_ImageAlt, product), Exact = true });

    private ShopImageFrame ProductFrame(string product) => ShopImageFrame.Containing(Page, ProductImage(product));

    private sealed class FocusedElement
    {
        public bool InsideImage { get; set; }
        public bool FocusVisible { get; set; }
        public string Label { get; set; } = "";
    }
}
