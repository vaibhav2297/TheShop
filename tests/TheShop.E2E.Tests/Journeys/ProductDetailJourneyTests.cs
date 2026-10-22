using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Public storefront browser acceptance for .specs/001_product-detail/spec.md, using isolated local product rows.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "001_product-detail")]
public sealed class ProductDetailJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private static string Money(decimal amount) => CurrencyFormatter.Format(amount, "CAD");
    private static string Range => string.Format(Strings.ProductDetail_PriceRange, Money(20), Money(35));
    private ProductDetailPage Detail(ProductDetailData data) => new(Page, data.Id);
    private static ILocator Current(ProductDetailPage detail) => detail.Price.Locator(".shop-product-detail-price");
    private static ILocator Selected(ProductDetailPage detail) => detail.Options.Locator("[aria-pressed='true']");
    private static async Task<decimal> NumberAsync(ILocator locator, string expression) => await locator.EvaluateAsync<decimal>(expression);

    [Fact]
    public async Task AC1_OpensWithUnselectedVariantsRangeAndPrimaryFirst()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data);
        await detail.OpenAsync();
        await Assertions.Expect(detail.Root.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(data.Name);
        await Assertions.Expect(detail.Root.Locator(".shop-product-detail-brand")).Not.ToBeEmptyAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Range);
        await Assertions.Expect(Selected(detail)).ToHaveCountAsync(0);
        await Assertions.Expect(detail.Slides.First.Locator("img")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex(data.Id.ToString("N")));
        var rows = await data.PublicRowsAsync("products", $"id=eq.{data.Id}");
        rows[0].GetProperty("original_price").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AC3_DesktopInitiallyShowsFourPrimaryFirstWithShowMore()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        await Assertions.Expect(detail.Gallery.Locator("[data-carousel-index]:visible")).ToHaveCountAsync(4);
        await Assertions.Expect(detail.More).ToBeVisibleAsync();
        (await detail.Viewport.EvaluateAsync<string>("el=>getComputedStyle(el).display")).Should().Be("grid");
        (await detail.Slides.CountAsync()).Should().Be(8);
        await Assertions.Expect(detail.Gallery.Locator(".shop-swipe-carousel-indicator")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC4_ManualNarrowScrollUpdatesIndicatorWithoutChangingSelectionOrPrice()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(390, 844);
        await detail.ChooseAsync(data.Values[0], data.Values[2]); await detail.PositionAsync(2);
        await detail.Viewport.EvaluateAsync("el=>{el.dispatchEvent(new Event('pointerdown'));el.scrollTo({left:el.clientWidth*2,behavior:'instant'});}");
        await detail.PositionAsync(3);
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(20));
        await Assertions.Expect(Selected(detail)).ToHaveCountAsync(2);
        await detail.Viewport.FocusAsync(); await Page.Keyboard.PressAsync("ArrowRight"); await detail.PositionAsync(4);
    }

    [Fact]
    public async Task AC5_GroupedSelectionUpdatesExactPriceAndImageOnlyWhenComplete()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(834, 1112);
        await detail.Choice(data.Values[0]).ClickAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Range); await detail.PositionAsync(1);
        await detail.Choice(data.Values[3]).ClickAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(25)); await detail.PositionAsync(6);
        await detail.Choice(data.Values[1]).ClickAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(35)); await detail.PositionAsync(8);
        (await detail.Slides.CountAsync()).Should().Be(8);
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.SetViewportSizeAsync(390, 844); await detail.PositionAsync(8);
    }

    [Fact]
    public async Task AC6_VariantWithoutImageFallsBackToProductPrimary()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(390, 844);
        await detail.ChooseAsync(data.Values[0], data.Values[3]); await detail.PositionAsync(6);
        await detail.ChooseAsync(data.Values[1], data.Values[2]); await detail.PositionAsync(1);
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(30));
    }

    [Fact]
    public async Task AC7_VariantFreeProductUsesOwnSalePriceAndBagIsNoOp()
    {
        await using var data = await ProductDetailData.CreateAsync(variants: false); var detail = Detail(data); await detail.OpenAsync();
        await Assertions.Expect(detail.Options).ToHaveCountAsync(0);
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(25));
        await Assertions.Expect(detail.Price.Locator("s")).ToHaveTextAsync(Money(30));
        var url = Page.Url; await detail.Bag.ClickAsync();
        Page.Url.Should().Be(url); await Assertions.Expect(detail.Bag).ToBeEnabledAsync();
        await Assertions.Expect(detail.Root.Locator("[data-detail-announcement]")).ToBeEmptyAsync();
        await Assertions.Expect(detail.Snackbar).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC8_DisclosuresStartCollapsedAndExpandIndependently()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        var description = detail.Description.GetByRole(AriaRole.Button); var specifications = detail.Specifications.GetByRole(AriaRole.Button);
        await Assertions.Expect(description).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(specifications).ToHaveAttributeAsync("aria-expanded", "false");
        await description.ClickAsync(); await specifications.ClickAsync(); await description.ClickAsync();
        await Assertions.Expect(description).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(specifications).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(detail.Specifications.Locator("dd")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AC9_BlankContentOmitsBothOptionalSections()
    {
        await using var data = await ProductDetailData.CreateAsync(content: false); var detail = Detail(data); await detail.OpenAsync();
        await Assertions.Expect(detail.Description).ToHaveCountAsync(0); await Assertions.Expect(detail.Specifications).ToHaveCountAsync(0);
        await Assertions.Expect(detail.Bag).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AC10_ActionButtonsStayEnabledWithoutPersistenceOrSuccessFeedback()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        var writes = new List<string>(); Page.Request += (_, request) => { if (request.Method is "POST" or "PATCH" or "DELETE" or "PUT") writes.Add(request.Url); };
        var url = Page.Url; await detail.Favourite.ClickAsync();
        await Assertions.Expect(detail.Options).ToHaveAttributeAsync("data-attention", "false");
        await detail.ChooseAsync(data.Values[0], data.Values[2]); await detail.Bag.ClickAsync(); await detail.Favourite.ClickAsync();
        await Assertions.Expect(detail.Bag).ToBeEnabledAsync(); await Assertions.Expect(detail.Favourite).ToBeEnabledAsync();
        Page.Url.Should().Be(url); writes.Should().BeEmpty(); await Assertions.Expect(detail.Snackbar).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC11_KeyboardControlsExposeNamesFocusAndState()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        await detail.Choice(data.Values[0]).FocusAsync(); await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(detail.Choice(data.Values[0])).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.Keyboard.PressAsync("Tab");
        (await Page.EvaluateAsync<bool>("()=>document.activeElement.matches('button:focus-visible')")).Should().BeTrue();
        await detail.More.FocusAsync(); await Page.Keyboard.PressAsync("Enter"); await Assertions.Expect(detail.Less).ToBeVisibleAsync();
        await detail.Description.GetByRole(AriaRole.Button).FocusAsync(); await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(detail.Description.GetByRole(AriaRole.Button)).ToHaveAttributeAsync("aria-expanded", "true");
        await Page.SetViewportSizeAsync(390, 844);
        await detail.Viewport.FocusAsync(); await Page.Keyboard.PressAsync("End"); await detail.PositionAsync(8);
        await Page.Keyboard.PressAsync("Home"); await detail.PositionAsync(1);
        await detail.Favourite.FocusAsync(); await Page.Keyboard.PressAsync("Space"); await Assertions.Expect(detail.Favourite).ToBeEnabledAsync();
        await Assertions.Expect(detail.Gallery).ToHaveAttributeAsync("aria-label", Strings.ProductDetail_Gallery);
    }

    [Fact]
    public async Task AC12_SingleImageHasNoFalseNavigationAtAllBreakpoints()
    {
        await using var data = await ProductDetailData.CreateAsync(images: 1); var detail = Detail(data);
        foreach (var width in new[] { 1440, 834, 390 })
        {
            await detail.OpenAsync(width); await Assertions.Expect(detail.Slides).ToHaveCountAsync(1);
            await Assertions.Expect(detail.More).ToHaveCountAsync(0);
            await Assertions.Expect(detail.Gallery.Locator(".shop-swipe-carousel-indicator")).ToHaveCountAsync(0);
            (await NumberAsync(detail.Viewport, "el=>el.scrollWidth-el.clientWidth")).Should().BeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public async Task AC13_MissingPrimaryEmptyAndBrokenImagesUseNamedSubtitlePlaceholder()
    {
        foreach (var state in new[] { "no-primary", "empty", "broken" })
        {
            await using var data = await ProductDetailData.CreateAsync(images: state == "empty" ? 0 : 1, primary: false, broken: state == "broken");
            var detail = Detail(data); await detail.OpenAsync(390, 844);
            if (state == "no-primary") { await Assertions.Expect(detail.Slides.First.Locator("img")).ToBeVisibleAsync(); continue; }
            var label = detail.Root.Locator(".shop-image-label"); await Assertions.Expect(label).ToHaveTextAsync(data.Name);
            (await label.EvaluateAsync<string>("el=>getComputedStyle(el).fontSize")).Should().Be("16px");
            (await label.EvaluateAsync<string>("el=>getComputedStyle(el).fontWeight")).Should().Be("500");
            await Assertions.Expect(detail.Bag).ToBeEnabledAsync();
        }
    }

    [Fact]
    public async Task AC14_MissingUnpublishedAndPrivilegedRoutesShowUnavailableWithoutLeakingChildren()
    {
        await using var data = await ProductDetailData.CreateAsync(published: false);
        await new ProductDetailPage(Page, data.Id).OpenUnavailableAsync();
        foreach (var table in new[] { "products", "product_images", "product_option_types", "product_variants", "product_specifications" })
            (await data.PublicRowsAsync(table, $"{(table == "products" ? "id" : "product_id")}=eq.{data.Id}")).GetArrayLength().Should().Be(0);
        await new ProductDetailPage(Page, Guid.NewGuid()).OpenUnavailableAsync();
        var state = await AuthStateFactory.EnsureSignedInAsync(Playwright.Browser, AuthStateFactory.AdminEmail);
        await using var staff = await ShopBrowser.NewContextAsync(Playwright.Browser, state);
        var staffPage = await staff.NewPageAsync();
        await new ProductDetailPage(staffPage, data.Id).OpenUnavailableAsync();
    }

    [Fact]
    public async Task AC15_MissingSelectionFocusesRevealsAndBrieflyShakesWithoutVisibleError()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(390, 844);
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference });
        await detail.Bag.ClickAsync();
        await Assertions.Expect(detail.Choice(data.Values[0])).ToBeFocusedAsync();
        await Assertions.Expect(detail.Options).ToHaveAttributeAsync("data-attention", "true");
        await Assertions.Expect(detail.Root.Locator("[data-detail-announcement]")).ToHaveTextAsync(Strings.ProductDetail_SelectVariant);
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Range); await Assertions.Expect(Selected(detail)).ToHaveCountAsync(0);
        (await detail.Options.EvaluateAsync<bool>("el=>el.getAnimations().every(a=>a.effect.getTiming().duration===300 && a.effect.getTiming().iterations===1)")).Should().BeTrue();
        await Page.WaitForFunctionAsync("()=>document.querySelector('[data-testid=product-detail-options]').getAnimations().length===0");
        await detail.Bag.FocusAsync(); await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(detail.Choice(data.Values[0])).ToBeFocusedAsync();
        await Assertions.Expect(detail.Root.Locator("[role='alert']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC16_CompleteSelectionClearsAttentionAndLaterBagDoesNothing()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(390, 844);
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await detail.Bag.ClickAsync(); await detail.ChooseAsync(data.Values[0], data.Values[2]);
        await Assertions.Expect(detail.Options).ToHaveAttributeAsync("data-attention", "false");
        await Assertions.Expect(detail.Root.Locator("[data-detail-announcement]")).ToBeEmptyAsync();
        (await detail.Options.EvaluateAsync<string>("el=>getComputedStyle(el).outlineStyle")).Should().Be("none");
        await detail.Bag.ClickAsync(); await Assertions.Expect(detail.Options).ToHaveAttributeAsync("data-attention", "false");
        await detail.PositionAsync(2); await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(20));
    }

    [Fact]
    public async Task AC17_NarrowGalleryExposesEveryImageWithPrimaryAndPositionIndicator()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data);
        foreach (var width in new[] { 834, 390, 320 })
        {
            await detail.OpenAsync(width); await detail.PositionAsync(1);
            await detail.Viewport.FocusAsync(); await Page.Keyboard.PressAsync("End"); await detail.PositionAsync(8);
            await Page.Keyboard.PressAsync("ArrowRight"); await detail.PositionAsync(8);
            await Assertions.Expect(Current(detail)).ToHaveTextAsync(Range);
            (await detail.Viewport.EvaluateAsync<string>("el=>getComputedStyle(el).columnGap")).Should().Be("0px");
            await detail.AssertHealthyAsync();
        }
    }

    [Fact]
    public async Task AC18_ShowMoreAndShowLessPreserveSelectionOrderAndToggleFocus()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        await detail.ChooseAsync(data.Values[0], data.Values[2]);
        await detail.More.ClickAsync(); await Assertions.Expect(detail.Gallery.Locator("[data-carousel-index]:visible")).ToHaveCountAsync(8);
        await detail.Less.ClickAsync(); await Assertions.Expect(detail.Gallery.Locator("[data-carousel-index]:visible")).ToHaveCountAsync(4);
        await Assertions.Expect(detail.More).ToBeFocusedAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(20)); await Assertions.Expect(Selected(detail)).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task AC19_VisibleDesktopVariantImageDoesNotScrollOrHighlight()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        var before = await detail.ScrollTopAsync(); await detail.ChooseAsync(data.Values[0], data.Values[2]);
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(20));
        (await detail.ScrollTopAsync()).Should().Be(before);
        await Assertions.Expect(detail.Slides.Locator("[aria-selected='true'],.selected")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC20_HiddenDesktopTargetExpandsAndRevealsWithoutReordering()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        await detail.ChooseAsync(data.Values[0], data.Values[3]);
        await Assertions.Expect(detail.Gallery.Locator("[data-carousel-index]:visible")).ToHaveCountAsync(8);
        await Page.WaitForFunctionAsync("()=>{const r=document.querySelector('[data-carousel-index=\"5\"]').getBoundingClientRect();const v=document.querySelector('main[data-shop-scroll-root]').getBoundingClientRect();return r.top<v.bottom && r.bottom>v.top}");
        var order = await detail.Slides.EvaluateAllAsync<int[]>("slides=>slides.map(s=>Number(s.dataset.carouselIndex))");
        order.Should().Equal(Enumerable.Range(0, 8));
        await Assertions.Expect(detail.Less).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AC21_AtMostFourDesktopImagesHaveNoExpansionControl()
    {
        await using var data = await ProductDetailData.CreateAsync(images: 4); var detail = Detail(data); await detail.OpenAsync();
        await Assertions.Expect(detail.Gallery.Locator("[data-carousel-index]:visible")).ToHaveCountAsync(4);
        await Assertions.Expect(detail.More).ToHaveCountAsync(0); await Assertions.Expect(detail.Less).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC22_EqualVariantPricesShowOneAmountWithNoInitialChoice()
    {
        await using var data = await ProductDetailData.CreateAsync(equal: true); var detail = Detail(data); await detail.OpenAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(25)); await Assertions.Expect(Selected(detail)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC23_SelectionUsesSalePriceThenOriginalPrice()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(390, 844);
        await detail.ChooseAsync(data.Values[0], data.Values[2]);
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(20)); await Assertions.Expect(detail.Price.Locator("s")).ToHaveTextAsync(Money(30));
        await detail.Choice(data.Values[3]).ClickAsync();
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Money(25)); await Assertions.Expect(detail.Price.Locator("s")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC24_ReducedMotionUsesStaticOutlineAndFocusWithoutShake()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(390, 844);
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce }); await detail.Bag.ClickAsync();
        await Assertions.Expect(detail.Choice(data.Values[0])).ToBeFocusedAsync();
        await Assertions.Expect(detail.Root.Locator("[data-detail-announcement]")).ToHaveTextAsync(Strings.ProductDetail_SelectVariant);
        (await detail.Options.EvaluateAsync<int>("el=>el.getAnimations().length")).Should().Be(0);
        (await detail.Options.EvaluateAsync<string>("el=>getComputedStyle(el).outlineStyle")).Should().Be("solid");
        await Assertions.Expect(Current(detail)).ToHaveTextAsync(Range); await Assertions.Expect(Selected(detail)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC25_BuyBoxScrollsNaturallyThenSticksWithClearanceAndReleasesAtBoundary()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync(1440, 600);
        await detail.More.ClickAsync(); await detail.ScrollToAsync(0);
        var box = detail.Root.Locator(".shop-product-detail-buybox");
        var initial = await box.BoundingBoxAsync(); var viewport = await detail.Scroll.BoundingBoxAsync();
        var threshold = initial!.Y - viewport!.Y - Math.Min(0, viewport.Height - initial.Height);
        await detail.ScrollToAsync((int)(threshold / 2)); var natural = await box.BoundingBoxAsync();
        natural!.Y.Should().BeLessThan(initial.Y);
        await detail.ScrollToAsync((int)threshold + 100); var stuck = await box.BoundingBoxAsync();
        var slideBefore = await detail.Slides.First.BoundingBoxAsync();
        await detail.ScrollToAsync((int)threshold + 250); var next = await box.BoundingBoxAsync();
        next!.Y.Should().BeApproximately(stuck!.Y, 2);
        (await detail.Slides.First.BoundingBoxAsync())!.Y.Should().BeLessThan(slideBefore!.Y);
        var actions = await detail.Bag.BoundingBoxAsync();
        (viewport.Y + viewport.Height - actions!.Y - actions.Height).Should().BeApproximately(24, 2);
        await detail.ScrollToAsync(100000); var section = await detail.Root.BoundingBoxAsync(); var released = await box.BoundingBoxAsync();
        (released!.Y + released.Height).Should().BeLessThanOrEqualTo(section!.Y + section.Height + 1);
        await Page.SetViewportSizeAsync(1440, 1100); await detail.ScrollToAsync(200);
        (await NumberAsync(box, "el=>parseFloat(getComputedStyle(el).top)")).Should().Be(0);
        await Page.SetViewportSizeAsync(834, 1112);
        (await box.EvaluateAsync<string>("el=>getComputedStyle(el).position")).Should().Be("static");
    }

    [Fact]
    public async Task AC26_LoadingSkeletonHasResponsiveStaticGalleryAndMeaningfulStatus()
    {
        await using var data = await ProductDetailData.CreateAsync();
        foreach (var width in new[] { 1440, 834, 390 })
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var intercepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task Hold(IRoute route) { intercepted.TrySetResult(); await release.Task; await route.ContinueAsync(); }
            await Context.RouteAsync("**/rest/v1/products?*", Hold);
            var detail = Detail(data); await Page.SetViewportSizeAsync(width, 900); await detail.GotoAsync(); await intercepted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                var loading = Page.GetByTestId("product-detail-loading"); await Assertions.Expect(loading).ToBeVisibleAsync();
                await Assertions.Expect(loading).ToHaveAttributeAsync("role", "status"); await Assertions.Expect(loading).ToHaveAttributeAsync("aria-label", Strings.Loading);
                await Assertions.Expect(loading.Locator("button,a,input")).ToHaveCountAsync(0);
                await Assertions.Expect(loading.Locator(".shop-product-detail-skeleton-image:visible")).ToHaveCountAsync(width >= 1024 ? 4 : 1);
                var image = (await loading.Locator(".shop-product-detail-skeleton-image").First.BoundingBoxAsync())!;
                image.Width.Should().BeApproximately(image.Height, 1);
            }
            finally { release.TrySetResult(); await Context.UnrouteAsync("**/rest/v1/products?*", Hold); }
            await Page.Locator("[data-testid='product-detail-ready']").WaitForAsync();
        }
    }

    [Fact]
    public async Task AC27_CatalogueUsesGalleryThumbnailAndDetailsUseBodyTypographyAndEightPixelGridGap()
    {
        await using var data = await ProductDetailData.CreateAsync(); var detail = Detail(data); await detail.OpenAsync();
        await detail.Description.GetByRole(AriaRole.Button).ClickAsync(); await detail.Specifications.GetByRole(AriaRole.Button).ClickAsync();
        await detail.AssertBodyStyleAsync(detail.Description.Locator("p")); await detail.AssertBodyStyleAsync(detail.Specifications.Locator("dd"));
        (await detail.Viewport.EvaluateAsync<string>("el=>getComputedStyle(el).gap")).Should().Be("8px");
        await detail.OpenCatalogueAsync(data.Name);
        var image = detail.Tile(data.Name).Locator("img"); await Assertions.Expect(image).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex(data.Id.ToString("N")));
        await Page.WaitForFunctionAsync("name=>Array.from(document.querySelectorAll('article.shop-product-card')).find(t=>t.textContent.includes(name))?.querySelector('img')?.naturalWidth>0", data.Name);
    }

    [Fact]
    public async Task AC28_TileTypographyHoverNativeNavigationAndIndependentActionsMatchContract()
    {
        await using var data = await ProductDetailData.CreateAsync(variants: false); var detail = Detail(data); await Page.SetViewportSizeAsync(1440, 900); await detail.OpenCatalogueAsync(data.Name);
        var tile = detail.Tile(data.Name); await tile.ScrollIntoViewIfNeededAsync(); var before = await tile.BoundingBoxAsync(); await tile.HoverAsync();
        var after = await tile.BoundingBoxAsync(); after.Should().BeEquivalentTo(before);
        var hover = await tile.EvaluateAsync<JsonElement>("el=>{let s=getComputedStyle(el,'::before');return {inset:s.top,border:s.borderTopWidth,color:s.borderTopColor};}");
        hover.GetProperty("inset").GetString().Should().Be("-8px"); hover.GetProperty("border").GetString().Should().Be("1px"); hover.GetProperty("color").GetString().Should().Be("rgb(23, 23, 23)");
        foreach (var (selector, size, weight) in new[] { (".shop-product-card-brand", "14px", "500"), (".shop-product-card-title", "16px", "400"), (".shop-product-card-price", "20px", "500"), (".shop-product-card-original-price", "16px", "500") })
        {
            (await tile.Locator(selector).EvaluateAsync<string>("el=>getComputedStyle(el).fontSize")).Should().Be(size);
            (await tile.Locator(selector).EvaluateAsync<string>("el=>getComputedStyle(el).fontWeight")).Should().Be(weight);
        }
        var url = Page.Url; await tile.GetByRole(AriaRole.Button, new() { Name = Strings.AddToCart, Exact = true }).ClickAsync();
        await tile.GetByRole(AriaRole.Button, new() { Name = Strings.Wishlist_Add, Exact = true }).ClickAsync(); Page.Url.Should().Be(url);
        var link = tile.GetByRole(AriaRole.Link); await Assertions.Expect(link).ToHaveAttributeAsync("href", Routes.ProductDetail(data.Id));
        foreach (var point in new[] { new Position { X = 3, Y = 3 }, new Position { X = 3, Y = before!.Height - 3 } })
        {
            await tile.ClickAsync(new() { Position = point }); await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(data.Id.ToString()));
            await detail.OpenCatalogueAsync(data.Name); tile = detail.Tile(data.Name);
        }
        await tile.GetByRole(AriaRole.Link).FocusAsync(); await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(data.Id.ToString()));
    }
}
