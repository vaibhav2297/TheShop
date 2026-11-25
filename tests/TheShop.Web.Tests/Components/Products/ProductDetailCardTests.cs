using Bunit;
using FluentAssertions;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

public class ProductDetailCardTests : TestContext
{
    public ProductDetailCardTests() => JSInterop.Mode = JSRuntimeMode.Loose;
    private static ProductSummaryDto Product() => new(Guid.Parse("00000000-0000-0000-0000-000000000001"),
        "Peach Ice", null, 25, null, false, "CAD", true, "Test Brand", null, true);

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_LinkedTile_PreservesNativeDestinationAndSeparateActions()
    {
        var bag = 0; var favourite = 0; var selected = 0;
        var product = Product();
        var cut = Render<ProductCard>(p => p.Add(c => c.Product, product)
            .Add(c => c.Href, Routes.ProductDetail(product.Id))
            .Add(c => c.OnAddToCart, () => bag++)
            .Add(c => c.OnToggleWishlist, () => favourite++)
            .Add(c => c.OnSelect, () => selected++));
        cut.Find("a").GetAttribute("href").Should().Be(Routes.ProductDetail(product.Id));
        cut.Find("a").TextContent.Should().Contain("Peach Ice");
        cut.FindAll("a button").Should().BeEmpty();
        cut.Find($"[aria-label='{Strings.AddToCart}']").Click();
        cut.Find($"[aria-label='{Strings.Wishlist_Add}']").Click();
        bag.Should().Be(1); favourite.Should().Be(1); selected.Should().Be(0);
        cut.Find(".shop-image-label").TextContent.Should().Be("Peach Ice");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_LoadingTile_HasNoNavigationOrActions()
    {
        var cut = Render<ProductCardSkeleton>();
        cut.FindAll("a, button, [tabindex]").Should().BeEmpty();
        cut.Find(".shop-product-card").ClassList.Should().NotContain("shop-product-card-interactive");
    }
}
