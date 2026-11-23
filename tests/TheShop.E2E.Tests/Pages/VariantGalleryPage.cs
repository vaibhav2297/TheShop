using FluentAssertions;
using Microsoft.Playwright;
using TheShop.Web.Resources;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Browser route and gallery observations for .specs/007_variant-urls-and-galleries/spec.md.</summary>
internal sealed class VariantGalleryPage(IPage page, VariantGalleryData data) : ShopPage(page)
{
    protected override string Route => data.ParentUrl;
    internal ILocator Ready => Page.Locator("[data-testid='product-detail-ready']");
    internal ILocator Gallery => Page.GetByTestId("product-detail-gallery");
    internal ILocator Slides => Gallery.Locator("[data-carousel-index]");
    internal ILocator Price => Page.GetByTestId("product-detail-price").Locator(".shop-product-detail-price");
    internal ILocator Sku => Page.Locator(".shop-product-detail-sku");
    internal ILocator Bag => Page.GetByTestId("product-detail-add-to-bag");
    internal ILocator Scroll => Page.Locator("main[data-shop-scroll-root]");
    internal ILocator Choice(int value) => Page.GetByTestId($"product-detail-option-{data.Values[value]}");
    internal ILocator Placeholder => Page.Locator(".shop-product-detail-no-image");
    internal ILocator Unavailable => Page.GetByTestId("product-detail-unavailable");

    internal async Task OpenVariantAsync(int index, int width = 1440, int height = 900)
    {
        await Page.SetViewportSizeAsync(width, height);
        await GotoAsync(data.VariantUrl(index));
        await Ready.WaitForAsync(new() { Timeout = 30_000 });
    }

    internal async Task OpenParentAsync()
    {
        await GotoAsync();
        await Ready.WaitForAsync(new() { Timeout = 30_000 });
    }

    internal async Task OpenUnavailableAsync(string url)
    {
        await GotoAsync(url);
        await Assertions.Expect(Unavailable).ToBeVisibleAsync();
    }

    internal async Task ExpectVariantAsync(int index, params int[] imageIndexes)
    {
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == data.VariantUrl(index));
        var colour = index is 1 or 3 ? 1 : 0;
        var size = index >= 2 ? 3 : 2;
        await Assertions.Expect(Choice(colour)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Choice(size)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Sku).ToContainTextAsync(data.Skus[index]);
        await Assertions.Expect(Price).ToContainTextAsync(TheShop.Web.Common.CurrencyFormatter.Format(20m + index * 5m, "CAD"));
        if (!data.Available[index]) await Assertions.Expect(Bag).ToBeDisabledAsync();
        else await Assertions.Expect(Bag).ToBeEnabledAsync();
        if (imageIndexes.Length == 0)
        {
            await Assertions.Expect(Placeholder).ToBeVisibleAsync();
            await Assertions.Expect(Gallery).ToHaveCountAsync(0);
        }
        else
        {
            await Assertions.Expect(Slides).ToHaveCountAsync(imageIndexes.Length);
            for (var i = 0; i < imageIndexes.Length; i++)
            {
                var image = Slides.Nth(i).Locator("img");
                await Assertions.Expect(image).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/{imageIndexes[i]}\\.png"));
                if (i == 0)
                {
                    await Assertions.Expect(image).ToHaveJSPropertyAsync("complete", true);
                    (await image.EvaluateAsync<int>("el => el.naturalWidth")).Should().BeGreaterThan(0);
                }
            }
        }
    }
}
