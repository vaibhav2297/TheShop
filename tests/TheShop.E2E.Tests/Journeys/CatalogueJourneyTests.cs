using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Storefront product catalogue journeys (.specs/product-catalogue/spec.md §6). Anonymous —
/// browsing the catalogue requires no persona.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "product-catalogue")]
[Trait("Suite", "Smoke")]
public sealed class CatalogueJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC1_Catalogue_renders_seeded_products()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public async Task Product_cards_render_square_images_with_contain_treatment()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
        var card = catalogue.ProductCards.First;
        await card.WaitForAsync(new() { Timeout = 15_000 });
        (await card.InnerHTMLAsync()).Should().Contain("shop-image-product");
        var frame = catalogue.ProductImageFrames.First;
        await frame.WaitForAsync(new() { Timeout = 15_000 });

        var bounds = await frame.BoundingBoxAsync();
        bounds.Should().NotBeNull();
        bounds!.Width.Should().BeApproximately(bounds.Height, 1);
        var image = frame.Locator("img");
        await image.EvaluateAsync<object?>(
            "(element, source) => { element.src = source; return element.decode(); }",
            ShopIcons.ImageAssets.LogoPrimary);
        (await image.EvaluateAsync<int>("element => element.naturalWidth")).Should().BeGreaterThan(0);
        var objectFit = await image.EvaluateAsync<string>("element => getComputedStyle(element).objectFit");
        objectFit.Should().Be("contain");

        var screenshotDirectory = Path.Combine(AppContext.BaseDirectory, "playwright-screenshots");
        Directory.CreateDirectory(screenshotDirectory);
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(screenshotDirectory, "reusable-image-treatments-product-card.png"),
            FullPage = true
        });
    }

    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public async Task Preset_styles_compute_confirmed_aspect_ratios()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await Page.SetContentAsync("""
            <link rel="stylesheet" href="/css/TheShop.css">
            <div id="product" class="shop-image-product"></div>
            <div id="thumbnail" class="shop-image-thumbnail"></div>
            <div id="category-tile" class="shop-image-category-tile"></div>
            <div id="category-banner" class="shop-image-category-banner"></div>
            <div id="hero-desktop" class="shop-image-hero-desktop"></div>
            <div id="hero-mobile" class="shop-image-hero-mobile"></div>
            <div id="editorial" class="shop-image-editorial"></div>
            <div id="brand-logo" class="shop-image-brand-logo"></div>
            <div id="social-sharing" class="shop-image-social-sharing"></div>
            """);

        var ratios = new Dictionary<string, string>
        {
            ["product"] = "1 / 1",
            ["thumbnail"] = "1 / 1",
            ["category-tile"] = "1 / 1",
            ["category-banner"] = "16 / 5",
            ["hero-desktop"] = "16 / 9",
            ["hero-mobile"] = "4 / 5",
            ["editorial"] = "4 / 3",
            ["brand-logo"] = "auto",
            ["social-sharing"] = "40 / 21"
        };

        foreach (var (id, ratio) in ratios)
            await Assertions.Expect(Page.Locator($"#{id}")).ToHaveCSSAsync("aspect-ratio", ratio);
    }

    [Fact]
    public async Task AC6_Applying_a_brand_filter_narrows_the_visible_products()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
        await catalogue.ProductName("Vaporesso XROS 3").WaitForAsync(new() { Timeout = 15_000 });

        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Brand);
        await catalogue.ToggleFilterOptionAsync("Elf Bar");

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
        (await catalogue.ProductName("Vaporesso XROS 3").CountAsync()).Should().Be(0,
            "the Vaporesso product must be filtered out once only the Elf Bar brand is selected");
    }
}
