using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

/// <summary>
/// Tests for <see cref="ProductCard"/> — the product tile rendered on the catalogue grid.
/// Covers FR-2, FR-3, FR-4, FR-5, FR-10; AC-2, AC-3, AC-4, AC-5, AC-11, AC-12, AC-14, and the
/// spec's out-of-stock, placeholder-image, and long-name edge cases.
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductCardTests : TestContext
{
    public ProductCardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    private static ProductSummaryDto BuildDto(
        string name = "Elf Bar BC5000",
        string? imageUrl = "https://example.com/photo.webp",
        decimal originalPrice = 24.99m,
        decimal? salePrice = null,
        bool isDiscounted = false,
        bool isInStock = true) =>
        new(
            Guid.NewGuid(), name, imageUrl, originalPrice, salePrice, isDiscounted, "CAD",
            isInStock, "Elf Bar", null, isInStock);

    // =========================================================================
    // Happy path — image, name, price (AC-1, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithImageUrl_RendersImageWithThatSource()
    {
        var dto = BuildDto(imageUrl: "https://example.com/photo.webp");
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Find("img").GetAttribute("src").Should().Be("https://example.com/photo.webp");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_Always_ShowsProductName()
    {
        var dto = BuildDto(name: "Elf Bar BC5000");
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Markup.Should().Contain("Elf Bar BC5000");
    }

    // =========================================================================
    // Discount pricing (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithDiscountedProduct_ShowsSalePriceAndStruckThroughOriginalPrice()
    {
        var dto = BuildDto(originalPrice: 24.99m, salePrice: 19.99m, isDiscounted: true);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Markup.Should().Contain(CurrencyFormatter.Format(19.99m));
        cut.Markup.Should().Contain(CurrencyFormatter.Format(24.99m));
        cut.Find(".text-decoration-line-through").TextContent.Should().Contain(CurrencyFormatter.Format(24.99m));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithoutDiscount_ShowsOnlyASinglePrice()
    {
        var dto = BuildDto(originalPrice: 24.99m, salePrice: null, isDiscounted: false);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Markup.Should().Contain(CurrencyFormatter.Format(24.99m));
        cut.FindAll(".text-decoration-line-through").Should().BeEmpty(
            "a non-discounted product must show only the single price, no struck-through original");
    }

    // =========================================================================
    // Add-to-Cart button (FR-4, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WhenInStock_ShowsAddToCartButtonWithAccessibleLabel()
    {
        var dto = BuildDto(isInStock: true);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Find($"[aria-label='{Strings.AddToCart}']").Should().NotBeNull();
    }

    // =========================================================================
    // Out-of-stock edge case (AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WhenOutOfStock_HidesAddToCartButton()
    {
        var dto = BuildDto(isInStock: false);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.FindAll($"[aria-label='{Strings.AddToCart}']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WhenOutOfStock_ShowsOutOfStockIndicator()
    {
        var dto = BuildDto(isInStock: false);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Markup.Should().Contain(Strings.OutOfStock);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WhenOutOfStock_StillShowsWishlistButton()
    {
        var dto = BuildDto(isInStock: false);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Find($"[aria-label='{Strings.Wishlist_Add}']").Should().NotBeNull();
    }

    // =========================================================================
    // Wishlist button (FR-5, AC-4, AC-14 accessible label)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WhenInStock_ShowsWishlistButtonWithAccessibleLabel()
    {
        var dto = BuildDto(isInStock: true);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Find($"[aria-label='{Strings.Wishlist_Add}']").Should().NotBeNull();
    }

    // =========================================================================
    // Placeholder image (AC-12)
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithoutImageUrl_ShowsPlaceholderImage(string? blankImageUrl)
    {
        var dto = BuildDto(imageUrl: blankImageUrl);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        cut.Find("img").GetAttribute("src").Should().Be(ShopIcons.ImageAssets.LogoPrimary);
    }

    // =========================================================================
    // Behavior 2 — buttons are display-only: activating them performs no action (FR-10, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ClickAddToCartButton_WhenActivated_InvokesOnAddToCartCallback()
    {
        var invoked = false;
        var dto = BuildDto(isInStock: true);
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, dto)
            .Add(c => c.OnAddToCart, EventCallback.Factory.Create(this, () => invoked = true)));

        cut.Find($"[aria-label='{Strings.AddToCart}']").Click();

        invoked.Should().BeTrue("the Add-to-Cart button must raise OnAddToCart so a future Cart feature can wire it");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ClickAddToCartButton_WhenActivated_DoesNotThrowWithoutACallbackBound()
    {
        // AC-5: activating the button performs no action within this feature — it must not
        // throw, navigate, or error even when nothing is listening (as on the real catalogue
        // page today, where Add-to-Cart is delivered by a separate feature).
        var dto = BuildDto(isInStock: true);
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, dto));

        var act = () => cut.Find($"[aria-label='{Strings.AddToCart}']").Click();

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ClickWishlistButton_WhenActivated_InvokesOnToggleWishlistCallback()
    {
        var invoked = false;
        var dto = BuildDto();
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, dto)
            .Add(c => c.OnToggleWishlist, EventCallback.Factory.Create(this, () => invoked = true)));

        cut.Find($"[aria-label='{Strings.Wishlist_Add}']").Click();

        invoked.Should().BeTrue("the Wishlist button must raise OnToggleWishlist so a future Wishlist feature can wire it");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ClickCardBody_WhenActivated_InvokesOnSelectCallback()
    {
        // AC-9 (accepted deviation, plan §11): the seam exists even though no page currently
        // wires navigation — the product-detail feature will bind OnSelect.
        var invoked = false;
        var dto = BuildDto();
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, dto)
            .Add(c => c.OnSelect, EventCallback.Factory.Create(this, () => invoked = true)));

        cut.Find(".content-section").Click();

        invoked.Should().BeTrue("selecting the card body must raise OnSelect so the product-detail feature can wire navigation");
    }

    // =========================================================================
    // Long product name edge case
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithVeryLongProductName_RendersWithoutThrowing()
    {
        // The grid layout's alignment/truncation is a CSS concern bUnit cannot verify visually;
        // this guards the functional contract that a long name doesn't break rendering.
        var longName = new string('A', 300);
        var dto = BuildDto(name: longName);

        var act = () => Render<ProductCard>(p => p.Add(c => c.Product, dto));

        act.Should().NotThrow();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Render_WithImageUrl_RendersImageWithThatSource, Render_Always_ShowsProductName
// AC-2: Render_WithDiscountedProduct_ShowsSalePriceAndStruckThroughOriginalPrice,
//        Render_WithoutDiscount_ShowsOnlyASinglePrice
// AC-3: Render_WhenInStock_ShowsAddToCartButtonWithAccessibleLabel
// AC-4: Render_WhenInStock_ShowsWishlistButtonWithAccessibleLabel
// AC-5: ClickAddToCartButton_WhenActivated_DoesNotThrowWithoutACallbackBound,
//        ClickAddToCartButton_WhenActivated_InvokesOnAddToCartCallback,
//        ClickWishlistButton_WhenActivated_InvokesOnToggleWishlistCallback
// AC-9: ClickCardBody_WhenActivated_InvokesOnSelectCallback (seam only — see plan §11 accepted
//        deviation; full navigation awaits the product-detail feature)
// AC-11: Render_WhenOutOfStock_HidesAddToCartButton, Render_WhenOutOfStock_ShowsOutOfStockIndicator,
//         Render_WhenOutOfStock_StillShowsWishlistButton
// AC-12: Render_WithoutImageUrl_ShowsPlaceholderImage
// AC-14: Render_WhenInStock_ShowsAddToCartButtonWithAccessibleLabel,
//         Render_WhenInStock_ShowsWishlistButtonWithAccessibleLabel (accessible-label part only;
//         keyboard operability and visible-focus are not verifiable in bUnit — see summary)
