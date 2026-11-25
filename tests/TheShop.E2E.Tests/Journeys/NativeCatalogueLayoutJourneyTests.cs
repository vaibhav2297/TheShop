using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeCatalogueLayoutJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private const string CategoryId = "56f735c3-527e-44b6-a4f2-c03c1c611101";
    private const string BrandId = "56f735c3-527e-44b6-a4f2-c03c1c611102";
    private readonly List<string> _unexpected = [];
    private bool _empty;
    private bool _fail;

    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(834, true)]
    [InlineData(390, true)]
    public async Task Listing_ResponsiveLayoutAndQueryControls_WorkWithoutVendorCss(int width, bool withoutVendorCss)
    {
        var release = new TaskCompletionSource();
        await InterceptAsync(release.Task);
        await Page.SetViewportSizeAsync(width, 1000);
        await Page.GotoAsync(WebRoutes.Products);
        var surface = Page.Locator(".shop-catalogue-page");
        await Assertions.Expect(surface.Locator(".shop-product-card-skeleton")).ToHaveCountAsync(12);
        await PrepareAppearanceAsync(withoutVendorCss);
        await SaveAsync($"catalogue-loading-{width}-{withoutVendorCss}");
        release.SetResult();

        var cards = surface.Locator("[data-testid=catalogue-grid] > .shop-product-card");
        await Assertions.Expect(cards).ToHaveCountAsync(12);
        await Assertions.Expect(surface.Locator("h1")).ToHaveTextAsync(Strings.Nav_Products);
        await Assertions.Expect(surface.Locator("h1")).ToHaveCSSAsync("font-size", "60px");
        await Assertions.Expect(surface.Locator(".shop-catalogue-count")).ToContainTextAsync(
            string.Format(Strings.Catalogue_ResultCount, 1, 12, 60));
        await Assertions.Expect(surface.Locator(".shop-expander-trigger[aria-expanded=true]")).ToHaveCountAsync(0);
        var columns = width < 768 ? 2 : 3;
        var first = (await cards.Nth(0).BoundingBoxAsync())!;
        var second = (await cards.Nth(1).BoundingBoxAsync())!;
        second.Y.Should().BeApproximately(first.Y, 1);
        (second.X - first.X - first.Width).Should().BeApproximately(16, 1);
        (await cards.Nth(columns).BoundingBoxAsync())!.Y.Should().BeGreaterThan(first.Y + first.Width);
        first.Width.Should().BeApproximately(width == 1440 ? 347.73f : width == 834 ? 246 : 171, 1);
        await Assertions.Expect(cards.Nth(1).Locator(".shop-badge-error")).ToHaveCSSAsync("min-height", "32px");
        await SaveAsync($"catalogue-list-{width}-{withoutVendorCss}");

        ILocator filters;
        if (width < 1024)
        {
            await surface.GetByRole(AriaRole.Button, new() { Name = Strings.Products_Filters, Exact = true }).ClickAsync();
            filters = Page.Locator("dialog[open]");
            await Assertions.Expect(filters).ToBeVisibleAsync();
            (await filters.BoundingBoxAsync())!.Width.Should().BeApproximately(width < 768 ? width : 400, 1);
            await Assertions.Expect(filters.Locator(".shop-expander-trigger[aria-expanded=false]")).ToHaveCountAsync(3);
            await Assertions.Expect(filters.Locator(".shop-catalogue-filter")).ToHaveCSSAsync("padding", "16px");
        }
        else filters = surface.Locator("aside");

        await filters.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Brand, Exact = true }).ClickAsync();
        await filters.GetByRole(AriaRole.Checkbox, new() { Name = "Puffco", Exact = true }).CheckAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("brand=" + BrandId));
        await Assertions.Expect(filters.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Brand, Exact = true }))
            .ToHaveAttributeAsync("aria-expanded", "true");
        await SaveAsync($"catalogue-filter-{width}-{withoutVendorCss}");
        if (width < 1024)
        {
            await Page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(Page.Locator("dialog[open]")).ToHaveCountAsync(0);
            await Assertions.Expect(surface.Locator(".shop-catalogue-filter-button")).ToBeFocusedAsync();
        }

        await surface.GetByRole(AriaRole.Combobox, new() { Name = Strings.Sort_Label, Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = Strings.Sort_NameAZ, Exact = true }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("sort=name-asc"));
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Pagination_Next, Exact = true }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("page=2"));
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("brand=" + BrandId));
        await Page.GoBackAsync();
        await Assertions.Expect(Page).Not.ToHaveURLAsync(new Regex("page=2"));
        await Page.GoForwardAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("page=2"));
        await AssertNoOverflowAsync();

        if (width == 390)
        {
            foreach (var probeWidth in new[] { 320, 767, 768, 1023, 1024 })
            {
                await Page.SetViewportSizeAsync(probeWidth, 1000);
                await AssertNoOverflowAsync();
            }
            await Page.SetViewportSizeAsync(width, 1000);
            await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
            await AssertNoOverflowAsync();
            await SaveAsync("catalogue-large-text");
            await Page.EvaluateAsync("document.documentElement.style.fontSize=''");
        }

        _empty = true;
        await Page.GotoAsync(WebRoutes.Products);
        await Assertions.Expect(surface.Locator(".shop-catalogue-empty")).ToBeVisibleAsync();
        await PrepareAppearanceAsync(withoutVendorCss);
        await Assertions.Expect(surface.Locator(".shop-catalogue-count")).ToContainTextAsync(
            string.Format(Strings.Catalogue_ResultCount, 0, 0, 0));
        await Assertions.Expect(surface.GetByRole(AriaRole.Combobox)).ToBeVisibleAsync();
        await Assertions.Expect(surface.Locator(".shop-pagination")).ToHaveCountAsync(0);
        await SaveAsync($"catalogue-empty-{width}-{withoutVendorCss}");
        await AssertNoOverflowAsync();

        _fail = true;
        await Page.GotoAsync(WebRoutes.Products);
        await Assertions.Expect(Page.GetByTestId("notification-dismiss").First).ToBeVisibleAsync();
        await Assertions.Expect(surface.GetByRole(AriaRole.Combobox)).ToBeEnabledAsync();
        _unexpected.Should().BeEmpty("all data is intercepted; no real API operations are allowed");
    }

    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(390, false)]
    [InlineData(390, true)]
    public async Task Listing_ScrollingToFooter_LeavesDocumentAtViewportHeight(int width, bool withoutVendorCss)
    {
        await InterceptAsync(Task.CompletedTask);
        await Page.SetViewportSizeAsync(width, 675);
        await Page.GotoAsync(WebRoutes.Products);
        await Assertions.Expect(Page.Locator(".shop-product-card")).ToHaveCountAsync(12);
        await PrepareAppearanceAsync(withoutVendorCss);
        var main = Page.Locator("main[data-shop-scroll-root]");

        foreach (var fontSize in new[] { "100%", "200%" })
        {
            await Page.EvaluateAsync("size => document.documentElement.style.fontSize = size", fontSize);
            var headerY = (await Page.Locator(".shop-appbar").BoundingBoxAsync())!.Y;
            var height = await Page.EvaluateAsync<double>("() => document.scrollingElement.scrollHeight");
            height.Should().BeApproximately(675, 1, "only the main content should scroll, including at enlarged text sizes");
            await main.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
            (await main.EvaluateAsync<double>("el => el.scrollTop")).Should().BeGreaterThan(0);
            var footerBottom = await Page.Locator(".shop-footer").EvaluateAsync<double>("el => el.getBoundingClientRect().bottom");
            footerBottom.Should().BeApproximately(675, 1, "the footer should end at the viewport without an outer blank region");
            await Page.EvaluateAsync("() => window.scrollTo(0, 10000)");
            (await Page.EvaluateAsync<double>("() => document.scrollingElement.scrollTop")).Should().Be(0);
            (await Page.Locator(".shop-appbar").BoundingBoxAsync())!.Y.Should().BeApproximately(headerY, 1);
            await SaveAsync($"catalogue-scroll-footer-{width}-{withoutVendorCss}-{fontSize.TrimEnd('%')}");
            await main.EvaluateAsync("el => el.scrollTop = 0");
        }
        _unexpected.Should().BeEmpty();
    }

    private Task InterceptAsync(Task release) => Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
    {
        var path = new Uri(route.Request.Url).AbsolutePath;
        if (path.EndsWith("/rpc/get_catalogue_filters") && route.Request.Method == "POST")
        {
            await release;
            await route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new
                {
                    categories = new[] { new { id = CategoryId, name = "Vape Pens" } },
                    brands = new[] { new { id = BrandId, name = "Puffco" } },
                    price_min = 20,
                    price_max = 130
                })
            });
        }
        else if (path.EndsWith("/rest/v1/products") && route.Request.Method == "GET")
        {
            await release;
            if (_fail)
            {
                await route.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{\"message\":\"Fixture failure\"}" });
                return;
            }
            await route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Content-Range"] = _empty ? "*/0" : "0-11/60", ["Access-Control-Expose-Headers"] = "Content-Range" },
                Body = JsonSerializer.Serialize(_empty ? [] : Enumerable.Range(1, 12).Select(i => new
                {
                    id = $"56f735c3-527e-44b6-a4f2-c03c1c6121{i:00}",
                    name = "Canadian Smooth - Smokeless & Super Strong",
                    description = "",
                    sku = $"PEN-{i}",
                    image_path = "fixture/pen.svg",
                    original_price = i == 2 ? (decimal?)null : 45.57m,
                    sale_price = i == 2 ? (decimal?)null : 34.57m,
                    min_variant_price = i == 2 ? (decimal?)25 : null,
                    has_sellable_variant = false,
                    currency = "CAD",
                    category_id = CategoryId,
                    brand_id = BrandId,
                    is_published = true,
                    created_at = "2026-01-01T00:00:00Z",
                    categories = new { id = CategoryId, name = "Vape Pens" },
                    brands = new { id = BrandId, name = "PUFFCO" }
                }))
            });
        }
        else if (path.Contains("/storage/v1/object/public/") && route.Request.Method == "GET")
            await route.FulfillAsync(new() { ContentType = "image/svg+xml", Body = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 320 320'><rect x='142' y='35' width='36' height='250' rx='10' fill='#222'/><rect x='147' y='75' width='26' height='42' fill='#aaa'/></svg>" });
        else
        {
            _unexpected.Add(route.Request.Method + " " + path);
            await route.AbortAsync();
        }
    });

    private Task PrepareAppearanceAsync(bool withoutVendorCss) => Page.EvaluateAsync("""
        async withoutVendorCss => {
            if (withoutVendorCss) for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                if (/MudBlazor|CodeBeam/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');
            await document.fonts.load('800 60px "Barlow Condensed"');
            await document.fonts.load('500 16px "Space Grotesk"');
            await document.fonts.ready;
            if (![...document.fonts].some(f => f.family.includes('Barlow Condensed') && f.status === 'loaded'))
                throw new Error('Figma comparison requires loaded web fonts');
        }
        """, withoutVendorCss);

    private async Task AssertNoOverflowAsync() =>
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();

    private async Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), Animations = ScreenshotAnimations.Disabled });
        if (name.StartsWith("catalogue-filter", StringComparison.Ordinal) && await Page.Locator("dialog[open]").CountAsync() > 0)
            await Page.Locator("dialog[open]").ScreenshotAsync(new() { Path = Path.Combine(directory, name + "-drawer.png"), Animations = ScreenshotAnimations.Disabled });
    }
}
