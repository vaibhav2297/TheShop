using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Guest journeys for .specs/007_variant-urls-and-galleries/spec.md acceptance criteria.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "007_variant-urls-and-galleries")]
public sealed class VariantGalleryPublicJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC1_ExactAddressesShowTheirOwnPurchaseDetailsAndPhotos()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await detail.OpenVariantAsync(1); await detail.ExpectVariantAsync(1, 3);
    }

    [Fact]
    public async Task AC2_SelectionChangesAddressAndGalleryWithoutReloadOrScroll()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0);
        await detail.Scroll.EvaluateAsync("el => el.scrollTo({top:300,behavior:'instant'})");
        var before = await detail.Scroll.EvaluateAsync<int>("el => el.scrollTop");
        await Page.EvaluateAsync("() => window.galleryNavigationMarker = 73");
        await detail.Choice(1).ClickAsync();
        await detail.ExpectVariantAsync(1, 3);
        (await detail.Scroll.EvaluateAsync<int>("el => el.scrollTop")).Should().Be(before);
        (await Page.EvaluateAsync<int>("() => window.galleryNavigationMarker")).Should().Be(73);
    }

    [Fact]
    public async Task AC3_DesktopAndMobileGalleriesKeepSavedImageOrder()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductDetail_ShowMore }).ClickAsync();
        await Assertions.Expect(detail.Slides).ToHaveCountAsync(5);
        await Page.SetViewportSizeAsync(390, 844);
        await Assertions.Expect(detail.Gallery).ToHaveAttributeAsync("data-swipe-enabled", "true");
        await detail.Gallery.Locator(".shop-swipe-carousel-viewport")
            .EvaluateAsync("el => {el.dispatchEvent(new Event('pointerdown'));el.scrollTo({left:el.clientWidth,behavior:'instant'});}");
        await Assertions.Expect(detail.Gallery).ToHaveAttributeAsync("data-active-image", "2");
        await detail.Choice(1).ClickAsync(); await detail.ExpectVariantAsync(1, 3);
    }

    [Fact]
    public async Task AC8_ParentAddressSelectsFirstAvailableOrFirstUnavailable()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await data.PatchAsync("product_variants", $"id=eq.{data.Variants[0]}", new { is_available = false });
        data.Available[0] = false;
        await detail.OpenParentAsync(); await detail.ExpectVariantAsync(1, 3);
        await data.PatchAsync("product_variants", $"product_id=eq.{data.Id}", new { is_available = false });
        Array.Fill(data.Available, false);
        await detail.OpenParentAsync(); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await Assertions.Expect(detail.Bag).ToBeDisabledAsync();
    }

    [Fact]
    public async Task AC9_HistoryRefreshAndCopiedAddressRestoreExactVariant()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0); await detail.Choice(1).ClickAsync(); await detail.ExpectVariantAsync(1, 3);
        await Page.EvaluateAsync("() => history.back()"); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await Page.EvaluateAsync("() => history.forward()"); await detail.ExpectVariantAsync(1, 3);
        await Page.ReloadAsync(); await detail.ExpectVariantAsync(1, 3);
        await detail.OpenVariantAsync(1); await detail.ExpectVariantAsync(1, 3);
    }

    [Fact]
    public async Task AC10_EmptyAndBrokenAssignedImagesNeverShowSiblingPhotos()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(3); await detail.ExpectVariantAsync(3);
        await data.PatchAsync("product_images", $"id=eq.{data.Images[1]}", new { object_key = "missing/e2e-gallery.png" });
        await detail.OpenVariantAsync(0);
        await Assertions.Expect(detail.Slides).ToHaveCountAsync(5);
        await Assertions.Expect(detail.Slides.First.Locator(".shop-image-placeholder")).ToBeVisibleAsync();
        await Assertions.Expect(detail.Slides.Nth(1).Locator("img")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex("/2\\.png"));
        await Assertions.Expect(detail.Gallery.Locator("img[src*='/3.png']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC11_LegacyProductLinkAndNonvariantGalleryRemainUsable()
    {
        await using var data = await ProductDetailData.CreateAsync(variants: false);
        await data.SeedSlugAsync();
        var detail = new ProductDetailPage(Page, data.Id);
        await detail.OpenAsync();
        await Assertions.Expect(detail.Slides).ToHaveCountAsync(data.Images.Length);
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == TheShop.Web.Common.Routes.ProductBySlug(data.Name));
        await Assertions.Expect(detail.Price).ToContainTextAsync(TheShop.Web.Common.CurrencyFormatter.Format(25m, "CAD"));
    }

    [Fact]
    public async Task AC13_UnknownWrongParentAndUnpublishedAddressesAreUnavailable()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenUnavailableAsync(TheShop.Web.Common.Routes.ProductVariant(data.Slug, "missing"));
        await detail.OpenUnavailableAsync(TheShop.Web.Common.Routes.ProductVariant("wrong-parent", data.VariantSlugs[0]));
        await data.PatchAsync("products", $"id=eq.{data.Id}", new { is_published = false });
        await detail.OpenUnavailableAsync(data.VariantUrl(0));
        await Assertions.Expect(Page.GetByText(data.Name, new() { Exact = true })).ToHaveCountAsync(0);
        await data.PatchAsync("products", $"id=eq.{data.Id}", new { is_published = true });
        await data.DeleteAsync("product_variants", $"id=eq.{data.Variants[0]}");
        await detail.OpenUnavailableAsync(data.VariantUrl(0));
    }

    [Fact]
    public async Task AC14_UnavailableExactVariantKeepsIdentityAndGallery()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(2); await detail.ExpectVariantAsync(2, 4);
    }

    [Fact]
    public async Task AC17_RenamedLabelsKeepSavedVariantAddress()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await data.PatchAsync("products", $"id=eq.{data.Id}", new { name = data.Name + " renamed" });
        await data.PatchAsync("product_option_values", $"id=eq.{data.Values[0]}", new { value = "Scarlet" });
        await detail.OpenVariantAsync(0); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = data.Name + " renamed" })).ToBeVisibleAsync();
        await Assertions.Expect(detail.Choice(0)).ToContainTextAsync("Scarlet");
    }

    [Fact]
    public async Task AC18_KeyboardAndTouchKeepSelectedIdentityAndGalleryPosition()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0, 390, 844);
        var blue = detail.Choice(1); await blue.FocusAsync(); await Page.Keyboard.PressAsync("Space");
        await detail.ExpectVariantAsync(1, 3); await Assertions.Expect(blue).ToBeFocusedAsync();
        await detail.Choice(0).ClickAsync(); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await Assertions.Expect(detail.Gallery.Locator("[aria-live='polite']"))
            .ToHaveTextAsync(string.Format(Strings.ProductDetail_ImagePosition, 1, 5));
        await Page.SetViewportSizeAsync(1440, 900); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);

        var touch = await ShopBrowser.NewContextAsync(Playwright.Browser, options: new BrowserNewContextOptions
        { HasTouch = true, IsMobile = true, ViewportSize = new() { Width = 390, Height = 844 } });
        try
        {
            var mobile = await touch.NewPageAsync(); var mobileDetail = new VariantGalleryPage(mobile, data);
            await mobileDetail.OpenVariantAsync(0, 390, 844);
            await mobileDetail.Choice(1).TapAsync();
            await mobileDetail.ExpectVariantAsync(1, 3);
        }
        finally { await touch.CloseAsync(); }
    }

    [Fact]
    public async Task AC19_RapidSelectionsLeaveLatestVariantAligned()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0);
        await Page.EvaluateAsync("""
            ids => {
                for (const id of ids) document.querySelector(`[data-testid="product-detail-option-${id}"]`).click();
            }
            """, new[] { data.Values[1], data.Values[0] });
        await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
    }

    [Fact]
    public async Task AC22_NonexistentCombinationCannotCommitPurchaseState()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var detail = new VariantGalleryPage(Page, data);
        await data.AddExtraSizeAsync();
        await detail.OpenVariantAsync(1); await detail.Choice(4).ClickAsync();
        await Assertions.Expect(Page.GetByText(Strings.ProductDetail_CombinationUnavailable)).ToBeVisibleAsync();
        await detail.ExpectVariantAsync(1, 3);
    }
}
