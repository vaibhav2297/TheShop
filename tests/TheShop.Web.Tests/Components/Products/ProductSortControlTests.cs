using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Domain.Enums;
using TheShop.Web.Components.Products;
using Xunit;
using MudBlazor.Extensions;

namespace TheShop.Web.Tests.Components.Products;

/// <summary>
/// Tests for <see cref="ProductSortControl"/> — the sort-order picker (FR-7, AC-7; spec
/// constraint: "The catalogue can be sorted by: Newest (the default), Price: low → high,
/// Price: high → low, Name: A → Z, and Name: Z → A.").
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductSortControlTests : TestContext
{
    public ProductSortControlTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();

        var popoverService = Substitute.For<IPopoverService>();
        popoverService.PopoverOptions.Returns(new PopoverOptions());
        Services.Replace(ServiceDescriptor.Singleton(popoverService));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_Always_OffersAllFiveSpecSortOptions()
    {
        var cut = Render<ProductSortControl>(p => p.Add(c => c.Sort, ProductSortOption.NewestFirst));

        cut.FindComponents<MudSelectItem<ProductSortOption>>().Should().HaveCount(5);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithNewestFirst_SelectsNewestFirstAsCurrentValue()
    {
        var cut = Render<ProductSortControl>(p => p.Add(c => c.Sort, ProductSortOption.NewestFirst));

        cut.FindComponent<MudSelect<ProductSortOption>>().Instance.GetState(x => x.Value).Should().Be(ProductSortOption.NewestFirst);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ChangeSort_WhenUserSelectsADifferentOption_InvokesSortChangedWithTheNewOption()
    {
        ProductSortOption? received = null;
        var cut = Render<ProductSortControl>(p => p
            .Add(c => c.Sort, ProductSortOption.NewestFirst)
            .Add(c => c.SortChanged, sort => received = sort));

        var select = cut.FindComponent<MudSelect<ProductSortOption>>();
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(ProductSortOption.PriceLowToHigh));

        received.Should().Be(ProductSortOption.PriceLowToHigh);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7: Render_Always_OffersAllFiveSpecSortOptions, ChangeSort_WhenUserSelectsADifferentOption_*
