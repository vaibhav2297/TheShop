using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductDetail;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Products;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

public class ProductDetailTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IJSObjectReference _controller = Substitute.For<IJSObjectReference>();
    private readonly IJSObjectReference _carousel = Substitute.For<IJSObjectReference>();
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    public ProductDetailTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var runtime = Substitute.For<IJSRuntime>();
        var detailModule = Substitute.For<IJSObjectReference>();
        var carouselModule = Substitute.For<IJSObjectReference>();
        runtime.InvokeAsync<IJSObjectReference>("import", Arg.Is<object?[]>(a => (string)a[0]! == "./js/productDetail.js"))
            .Returns(new ValueTask<IJSObjectReference>(detailModule));
        runtime.InvokeAsync<IJSObjectReference>("import", Arg.Is<object?[]>(a => (string)a[0]! == "./js/shopSwipeCarousel.js"))
            .Returns(new ValueTask<IJSObjectReference>(carouselModule));
        detailModule.InvokeAsync<IJSObjectReference>("mount", Arg.Any<object?[]>())
            .Returns(new ValueTask<IJSObjectReference>(_controller));
        carouselModule.InvokeAsync<IJSObjectReference>("mount", Arg.Any<object?[]>())
            .Returns(new ValueTask<IJSObjectReference>(_carousel));
        Services.AddSingleton(runtime);
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(Substitute.For<IStringLocalizer<Strings>>());
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        SetProduct(Product());
    }

    private void SetProduct(ProductDetailDto product) => _mediator.Send(Arg.Any<GetProductDetailQuery>(), Arg.Any<CancellationToken>())
        .Returns(Result.Ok(product));

    private static ProductDetailDto Product(int imageCount = 6) => new(Id(1), "Peach Ice", "Test Brand", true, "CAD",
        null, null, null, 20, 25, "<p>Fresh peach.</p>", imageCount > 0 ? Id(100) : null,
        Enumerable.Range(0, imageCount).Select(i => new ProductImageDto(Id(100 + i), $"https://images.example/{i}.png", i, i == 0)).ToArray(),
        [new(Id(10), "Flavour", 0, [new(Id(11), "Mango", 0), new(Id(12), "Peach", 1)]),
         new(Id(20), "Nicotine", 1, [new(Id(21), "10mg", 0), new(Id(22), "20mg", 1)])],
        [new(Id(50), "Capacity", "2 ml", 0)],
        [new(Id(30), "Mango / 10mg", 30, 20, 20, true, imageCount > 5 ? Id(105) : imageCount > 0 ? Id(100) : null, [Id(11), Id(21)]),
         new(Id(31), "Peach / 20mg", 25, null, 25, true, imageCount > 0 ? Id(100) : null, [Id(12), Id(22)])]);

    private IRenderedComponent<ProductDetail> Open() => Render<ProductDetail>(p => p.Add(c => c.Id, Id(1)));
    private static string Price(IRenderedComponent<ProductDetail> cut) => cut.Find(".shop-product-detail-price").TextContent;
    private static void Choose(IRenderedComponent<ProductDetail> cut, int value) =>
        cut.Find($"[data-testid='product-detail-option-{Id(value)}']").Click();
    private static string Range() => string.Format(Strings.ProductDetail_PriceRange, CurrencyFormatter.Format(20), CurrencyFormatter.Format(25));

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Render_PublicProduct_StartsUnselectedWithRangeAndPrimaryFirst()
    {
        var cut = Open();
        cut.Find("h1").TextContent.Should().Be("Peach Ice");
        cut.Find(".shop-product-detail-brand").TextContent.Should().Be("Test Brand");
        Price(cut).Should().Be(Range());
        cut.FindAll("[aria-pressed='true']").Should().BeEmpty();
        cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>().Instance.ActiveIndex.Should().Be(0);
        cut.FindAll(".shop-product-detail-slide img").First().GetAttribute("src").Should().Be("https://images.example/0.png");
        await _mediator.Received(1).Send(Arg.Is<GetProductDetailQuery>(q => q.Id == Id(1)), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Select_PartialThenCompleteCombination_KeepsRangeUntilExactPriceAndImageResolve()
    {
        var cut = Open();
        Choose(cut, 11);
        Price(cut).Should().Be(Range());
        cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>().Instance.ActiveIndex.Should().Be(0);
        Choose(cut, 21);
        Price(cut).Should().Be(CurrencyFormatter.Format(20));
        cut.Find("s").TextContent.Should().Be(CurrencyFormatter.Format(30));
        cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>().Instance.ActiveIndex.Should().Be(5);
        cut.FindAll("[aria-pressed='true']").Should().HaveCount(2);
        Choose(cut, 12);
        Price(cut).Should().Be(Range());
        cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>().Instance.ActiveIndex.Should().Be(5);
        Choose(cut, 22);
        Price(cut).Should().Be(CurrencyFormatter.Format(25));
        cut.FindAll("s").Should().BeEmpty();
        cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>().Instance.ActiveIndex.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Browse_ManualImageChange_PreservesVariantPriceAndChoices()
    {
        var cut = Open();
        Choose(cut, 11); Choose(cut, 21);
        var carousel = cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>();
        await cut.InvokeAsync(() => carousel.Instance.ActiveIndexChanged.InvokeAsync(2));
        Price(cut).Should().Be(CurrencyFormatter.Format(20));
        cut.FindAll("[aria-pressed='true']").Should().HaveCount(2);
        carousel.Instance.ActiveIndex.Should().Be(2);
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Select_DesktopHiddenTarget_ExpandsWithoutReorderingAndRequestsReveal()
    {
        var cut = Open();
        await cut.InvokeAsync(() => cut.Instance.OnDetailBreakpointChanged(true, 1));
        Choose(cut, 11); Choose(cut, 21);
        cut.Find("article").ClassList.Should().Contain("shop-product-detail-expanded");
        cut.FindAll(".shop-product-detail-slide img").Select(i => i.GetAttribute("src"))
            .Should().Equal(Enumerable.Range(0, 6).Select(i => $"https://images.example/{i}.png"));
        await _controller.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("revealImage", Arg.Is<object?[]>(a => (int)a[0]! == 5));
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToggleGallery_ExpandedThenCollapsed_PreservesSelectionAndPrice()
    {
        var cut = Open();
        Choose(cut, 11); Choose(cut, 21);
        cut.Find("[data-testid='product-detail-show-more']").Click();
        cut.Find("[data-testid='product-detail-show-less']").GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("[data-testid='product-detail-show-less']").Click();
        cut.Find("article").ClassList.Should().NotContain("shop-product-detail-expanded");
        cut.Find("[data-testid='product-detail-show-more']").GetAttribute("aria-expanded").Should().Be("false");
        Price(cut).Should().Be(CurrencyFormatter.Format(20));
        cut.FindAll("[aria-pressed='true']").Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [Trait("Feature", "001_product-detail")]
    public void Render_AtMostFourImages_OmitsGalleryToggle(int images)
    {
        SetProduct(Product(images));
        var cut = Open();
        cut.FindAll(".shop-product-detail-more").Should().BeEmpty();
        if (images == 0) cut.Find(".shop-image-label").TextContent.Should().Be("Peach Ice");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Render_FailedGalleryImage_ShowsProductNamePlaceholder()
    {
        var cut = Open();
        var image = cut.FindComponent<ShopImage>();
        await cut.InvokeAsync(() => image.Instance.OnImageFailed("desktop", image.Instance.Src!));
        image.Find(".shop-image-label").TextContent.Should().Be("Peach Ice");
        image.Find("[role='img']").GetAttribute("aria-label").Should().Be(string.Format(Strings.Product_ImageAlt, "Peach Ice"));
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_EqualVariantPrices_ShowsSingleAmountWithoutSelection()
    {
        SetProduct(Product() with { MinVariantPrice = 20, MaxVariantPrice = 20 });
        var cut = Open();
        Price(cut).Should().Be(CurrencyFormatter.Format(20));
        cut.FindAll("[aria-pressed='true']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_UnpricedProduct_ShowsUnavailableInsteadOfZero()
    {
        SetProduct(Product() with { MinVariantPrice = null, MaxVariantPrice = null });
        Price(Open()).Should().Be(Strings.NotAvailable);
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Disclosures_ExpandIndependently_AndStartCollapsed()
    {
        var cut = Open();
        var sections = cut.FindComponents<ShopExpander>();
        sections.Should().HaveCount(2);
        sections.Should().OnlyContain(s => !s.Instance.Expanded);
        sections[0].Find("button").Click();
        sections[0].Instance.Expanded.Should().BeTrue();
        sections[1].Instance.Expanded.Should().BeFalse();
        sections[1].Find("button").Click();
        sections[0].Instance.Expanded.Should().BeTrue();
        sections[1].Instance.Expanded.Should().BeTrue();
        sections[0].Find("button").Click();
        sections[0].Instance.Expanded.Should().BeFalse();
        sections[1].Instance.Expanded.Should().BeTrue();
        cut.Find("dd").TextContent.Should().Be("2 ml");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Feature", "001_product-detail")]
    public void Render_AbsentOptionalContent_OmitsOnlyEmptySection(bool description)
    {
        SetProduct(description ? Product() with { DescriptionHtml = null } : Product() with { Specifications = [] });
        var cut = Open();
        cut.FindComponents<ShopExpander>().Should().ContainSingle();
        cut.FindAll(description ? "[data-testid='product-detail-description']" : "[data-testid='product-detail-specifications']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task AddToBag_IncompleteSelection_RequestsAttentionThenCompleteSelectionClearsIt()
    {
        var cut = Open();
        var price = Price(cut);
        cut.Find("[data-testid='product-detail-add-to-bag']").Click();
        cut.Find("[data-testid='product-detail-options']").GetAttribute("data-attention").Should().Be("true");
        await _controller.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("attention",
            Arg.Is<object?[]>(a => (string)a[0]! == Id(10).ToString() && (string)a[1]! == Strings.ProductDetail_SelectVariant));
        Price(cut).Should().Be(price);
        cut.FindAll("[aria-pressed='true']").Should().BeEmpty();
        cut.FindAll("[role='alert']").Should().BeEmpty();
        Choose(cut, 11); Choose(cut, 21);
        cut.Find("[data-testid='product-detail-options']").GetAttribute("data-attention").Should().Be("false");
        _controller.ClearReceivedCalls();
        cut.Find("[data-testid='product-detail-add-to-bag']").Click();
        _controller.ReceivedCalls().Should().BeEmpty();
        _notifications.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Actions_VariantFreeProduct_AreEnabledAndHaveNoSideEffects()
    {
        SetProduct(Product(1) with { HasVariants = false, OptionTypes = [], Variants = [], CurrentPrice = 25, OriginalPrice = 30, SalePrice = 25 });
        var cut = Open();
        cut.FindAll("fieldset").Should().BeEmpty();
        _mediator.ClearReceivedCalls(); _controller.ClearReceivedCalls();
        foreach (var selector in new[] { "product-detail-add-to-bag", "product-detail-favourite" })
        {
            var button = cut.Find($"[data-testid='{selector}']");
            button.HasAttribute("disabled").Should().BeFalse();
            button.Click();
        }
        Price(cut).Should().Be(CurrencyFormatter.Format(25));
        _mediator.ReceivedCalls().Should().BeEmpty();
        _notifications.ReceivedCalls().Should().BeEmpty();
        _controller.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_MissingProductAfterNavigation_ClearsDetailsAndVariants()
    {
        var cut = Open();
        Choose(cut, 11); Choose(cut, 21);
        _mediator.Send(Arg.Any<GetProductDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ProductDetailDto>(ProductErrorKeys.DetailUnavailable));
        cut.Render(p => p.Add(c => c.Id, Id(2)));
        cut.Find("[data-testid='product-detail-unavailable'] h1").TextContent.Should().Be(Strings.ProductDetail_Unavailable);
        cut.FindAll("fieldset, .shop-product-detail-price, article").Should().BeEmpty();
        cut.Find("a").GetAttribute("href").Should().Be(Routes.Products);
        _notifications.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_PendingRequest_ShowsNamedStaticSkeletonWithoutControls()
    {
        var pending = new TaskCompletionSource<Result<ProductDetailDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator.Send(Arg.Any<GetProductDetailQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = Open();
        var loading = cut.Find("[data-testid='product-detail-loading']");
        loading.GetAttribute("aria-busy").Should().Be("true");
        loading.GetAttribute("aria-label").Should().Be(Strings.Loading);
        cut.FindAll("button, a, input").Should().BeEmpty();
        cut.FindAll(".shop-product-detail-skeleton-image").Should().HaveCount(4);
        pending.SetResult(Result.Ok(Product()));
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='product-detail-loading']").Should().BeEmpty());
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Resize_ToNarrowGallery_UsesSelectedTargetAndIgnoresOldGeneration()
    {
        var cut = Open();
        Choose(cut, 11); Choose(cut, 21);
        var carousel = cut.FindComponent<ShopSwipeCarousel<ProductImageDto>>();
        await cut.InvokeAsync(() => carousel.Instance.ActiveIndexChanged.InvokeAsync(2));
        await cut.InvokeAsync(() => cut.Instance.OnDetailBreakpointChanged(false, 0));
        carousel.Instance.ActiveIndex.Should().Be(2);
        await cut.InvokeAsync(() => cut.Instance.OnDetailBreakpointChanged(false, 1));
        carousel.Instance.ActiveIndex.Should().Be(5);
    }
}
