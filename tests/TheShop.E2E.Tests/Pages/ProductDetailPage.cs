using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Public product-detail navigation and browser observations for .specs/001_product-detail/spec.md.</summary>
public sealed class ProductDetailPage(IPage page, Guid id) : ShopPage(page)
{
    protected override string Route => WebRoutes.ProductDetail(id);
    public ILocator Root => Page.Locator("article.shop-product-detail");
    public ILocator Price => Page.GetByTestId("product-detail-price");
    public ILocator Gallery => Page.GetByTestId("product-detail-gallery");
    public ILocator Viewport => Gallery.Locator(".shop-swipe-carousel-viewport");
    public ILocator Slides => Gallery.Locator("[data-carousel-index]");
    public ILocator Options => Page.GetByTestId("product-detail-options");
    public ILocator Bag => Page.GetByTestId("product-detail-add-to-bag");
    public ILocator Favourite => Page.GetByTestId("product-detail-favourite");
    public ILocator Description => Page.GetByTestId("product-detail-description");
    public ILocator Specifications => Page.GetByTestId("product-detail-specifications");
    public ILocator Scroll => Page.Locator("main[data-shop-scroll-root]");
    public ILocator More => Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductDetail_ShowMore, Exact = true });
    public ILocator Less => Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductDetail_ShowLess, Exact = true });
    public async Task OpenAsync(int width = 1440, int height = 900)
    {
        await Page.SetViewportSizeAsync(width, height);
        await GotoAsync();
        await Page.Locator("[data-testid='product-detail-ready']").WaitForAsync(new() { Timeout = 30_000 });
        await Page.EvaluateAsync("() => document.fonts.ready.then(() => true)");
        await AssertHealthyAsync();
    }
    public async Task OpenUnavailableAsync()
    {
        await GotoAsync();
        await Assertions.Expect(Page.GetByTestId("product-detail-unavailable")).ToBeVisibleAsync();
        await Assertions.Expect(Root).ToHaveCountAsync(0);
    }
    public ILocator Choice(Guid value) => Page.GetByTestId($"product-detail-option-{value}");
    public async Task ChooseAsync(Guid first, Guid second)
    {
        await Choice(first).ClickAsync(); await Choice(second).ClickAsync();
    }
    public async Task PositionAsync(int oneBased)
    {
        await Assertions.Expect(Gallery).ToHaveAttributeAsync("data-active-image", oneBased.ToString());
        await Assertions.Expect(Gallery.Locator("[aria-live='polite']"))
            .ToHaveTextAsync(string.Format(Strings.ProductDetail_ImagePosition, oneBased, await Slides.CountAsync()));
        await Page.WaitForFunctionAsync("({position}) => { const v=document.querySelector('.shop-product-detail-grid'); const slide=v.children[position-1]; return Math.abs(slide.getBoundingClientRect().left-v.getBoundingClientRect().left)<2; }", new { position = oneBased });
    }
    public async Task ScrollToAsync(int offset)
    {
        await Scroll.EvaluateAsync("(el,y) => el.scrollTo({top:y,behavior:'instant'})", offset);
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
    }
    public Task<int> ScrollTopAsync() => Scroll.EvaluateAsync<int>("el => Math.round(el.scrollTop)");
    public async Task AssertHealthyAsync()
    {
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        (await Page.Locator("link[href*='MudBlazor'],script[src*='MudBlazor'],.mud-layout").CountAsync()).Should().Be(0);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1")).Should().BeTrue();
    }
    public async Task AssertBodyStyleAsync(ILocator locator)
    {
        var style = await locator.EvaluateAsync<JsonElement>("el => {const s=getComputedStyle(el);return {size:s.fontSize,weight:s.fontWeight,spacing:s.letterSpacing,line:s.lineHeight,color:s.color,family:s.fontFamily,expected:getComputedStyle(el).getPropertyValue('--shop-color-text-primary').trim()};}");
        style.GetProperty("size").GetString().Should().Be("16px");
        style.GetProperty("weight").GetString().Should().Be("400");
        style.GetProperty("spacing").GetString().Should().Be("0.25px");
        style.GetProperty("line").GetString().Should().Be("normal");
        style.GetProperty("family").GetString().Should().Contain("Space Grotesk");
        style.GetProperty("color").GetString().Should().Be("rgb(23, 23, 23)");
    }
    public async Task OpenCatalogueAsync(string name)
    {
        await GotoAsync(WebRoutes.Products);
        await Page.GetByRole(AriaRole.Link).Filter(new() { HasText = name }).First.WaitForAsync(new() { Timeout = 30_000 });
    }
    public ILocator Tile(string name) => Page.Locator("article.shop-product-card").Filter(new() { HasText = name });
}
