using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Shop Image journeys for staff administration screens (.specs/shop-image/spec.md §6 AC-2, AC-8).
/// Seeded categories and brands carry placeholder image URLs, which the browser context answers with
/// deterministic tall and wide artwork. Constrained-row and resized-allocation cases use
/// <see cref="ShopImageCompositionPage"/>. No database rows are written.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "shop-image")]
public sealed class ShopImageAdminJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await ShopImageArtwork.RouteAsync(Context);
    }

    [Fact]
    public async Task AC2_Admin_thumbnails_stay_square_whole_and_inside_their_row_space()
    {
        // Disposables gets a tall source, every other seeded category a wide one.
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, route =>
            ShopImageArtwork.SeededLabel(route.Request) == "Disposables"
                ? ShopImageArtwork.FulfillAsync(route, 300, 900)
                : ShopImageArtwork.FulfillAsync(route, 900, 300));

        var categories = new ManageCategoriesPage(Page);
        await categories.GotoAsync();
        foreach (var width in ShopImageExpectations.Widths)
        {
            await Page.SetViewportSizeAsync(width, 900);
            foreach (var category in new[] { "Disposables", "Pod Systems" })
            {
                var frame = ShopImageFrame.Containing(Page,
                    Page.GetByRole(AriaRole.Img, new() { Name = category, Exact = true }));
                await frame.WaitForImageAsync();
                var m = await frame.MeasureAsync();
                var subject = $"{category}'s admin thumbnail at {width}px";

                m.SourceRatioDiffersFromFrame.Should().BeTrue($"{category}'s fixture source must not already be square");
                m.ShouldHaveRatio((1, 1), subject);
                m.ShouldShowTheWholeImage(subject);
                m.ShouldStayInsideItsAllocation(subject);
            }
        }

        // Product rows without a primary image still reserve the same square thumbnail space.
        await Page.SetViewportSizeAsync(1440, 900);
        var products = new ManageProductsPage(Page);
        await products.GotoAsync();
        var productThumb = new ShopImageFrame(Page.Locator("tbody").Locator(ShopImageFrame.FrameSelector).First);
        await productThumb.Root.WaitForAsync(new() { Timeout = 15_000 });
        var p = await productThumb.MeasureAsync();
        p.ShouldHaveRatio((1, 1), "a product row thumbnail");
        p.ShouldStayInsideItsAllocation("a product row thumbnail");

        // Cart and order thumbnails have no destination screen yet: the preset shrinks with its row.
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();
        foreach (var rowWidth in new[] { 96, 56, 32 })
            foreach (var (w, h) in new[] { (300, 900), (900, 300) })
            {
                var host = $"position:absolute;left:16px;top:16px;z-index:10000;display:flex;width:{rowWidth}px";
                var thumb = await composition.MountAsync("thumbnail-row", host, ShopImagePreset.SquareContain, ShopImageArtwork.Url(w, h));
                await thumb.WaitForImageAsync();
                var m = await thumb.MeasureAsync();
                var subject = $"a thumbnail in a {rowWidth}px row with a {w}×{h} source";

                m.Width.Should().BeApproximately(rowWidth, 0.5, $"{subject} takes the row's width");
                m.ShouldHaveRatio((1, 1), subject);
                m.ShouldShowTheWholeImage(subject);
                m.ShouldStayInsideItsAllocation(subject);
            }
    }

    [Fact]
    public async Task AC8_Wide_and_tall_brand_logos_stay_whole_in_original_proportions_within_their_space()
    {
        await Context.RouteAsync(ShopImageArtwork.SeededPlaceholderGlob, route =>
            route.Request.Url.Contains("Geek")
                ? ShopImageArtwork.FulfillAsync(route, 200, 800)
                : ShopImageArtwork.FulfillAsync(route, 800, 200));

        var brands = new ManageBrandsPage(Page);
        await brands.GotoAsync();
        foreach (var brand in new[] { "Elf Bar", "Geek Bar" })
        {
            var frame = ShopImageFrame.Containing(Page, Page.GetByRole(AriaRole.Img, new() { Name = brand, Exact = true }));
            await frame.WaitForImageAsync();
            var m = await frame.MeasureAsync();

            m.NaturalWidth.Should().NotBe(m.NaturalHeight, $"{brand}'s fixture logo must not be square");
            m.ShouldShowTheWholeImage($"{brand}'s logo");
            m.ShouldStayInsideItsAllocation($"{brand}'s logo");
            (m.Width, m.Height).Should().Be((m.AllocationWidth, m.AllocationHeight),
                $"{brand}'s logo fills the avatar space its row reserves");
        }

        // The storefront logo in the app bar keeps its reserved space too.
        var appBarLogo = new ShopImageFrame(Page.Locator(ShopImageFrame.FrameSelector)
            .Filter(new() { Has = Page.GetByRole(AriaRole.Img, new() { Name = Strings.AppName, Exact = true }) }).First);
        await appBarLogo.WaitForImageAsync();
        var bar = await appBarLogo.MeasureAsync();
        bar.ShouldShowTheWholeImage("the app bar logo");
        bar.ShouldStayInsideItsAllocation("the app bar logo");

        // When the allocated logo space changes shape, the frame follows it and the logo stays whole.
        var composition = new ShopImageCompositionPage(Page);
        await composition.GotoAsync();
        var logo = await composition.MountAsync("logo", LogoHost(240, 80), ShopImagePreset.BrandLogo,
            ShopImageArtwork.Url(800, 200, "logo"), alt: "Elf Bar", placeholderLabel: "Elf Bar");
        foreach (var (w, h) in new[] { (240, 80), (60, 180), (120, 120) })
        {
            await composition.ResizeHostAsync("logo", LogoHost(w, h));
            await logo.WaitForImageAsync();
            var m = await logo.MeasureAsync();

            (m.Width, m.Height).Should().Be(((double)w, (double)h), $"the logo frame follows its {w}×{h} allocation");
            m.ShouldShowTheWholeImage($"a wide logo in a {w}×{h} space");
        }
    }

    private static string LogoHost(int width, int height) =>
        $"position:absolute;left:16px;top:16px;z-index:10000;width:{width}px;height:{height}px";
}
