using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Domain.Enums;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopSortSelect{TSort}"/> — the sort-order picker (FR-7, AC-7; spec
/// constraint: "The catalogue can be sorted by: Newest (the default), Price: low → high,
/// Price: high → low, Name: A → Z, and Name: Z → A."). Originally
/// <c>ProductSortControlTests</c>; moved here when the control was extracted into a
/// feature-agnostic, generic shared component (manage-brands plan §5 Decision 4, TASK-020) —
/// assertions are unchanged by that move.
/// <see href=".specs/product-catalogue/spec.md"/>
/// <see href=".specs/manage-brands/plan.md"/>
/// </summary>
public class ShopSortSelectTests : TestContext
{
    // Labels reach the control as resource keys and are resolved through the localizer at render
    // (mirroring ShopFilterPanel), so the options carry keys rather than display text.
    private static readonly IReadOnlyList<(ProductSortOption Value, string LabelKey)> FiveProductOptions =
    [
        (ProductSortOption.NewestFirst, nameof(Strings.Sort_Newest)),
        (ProductSortOption.PriceLowToHigh, nameof(Strings.Sort_PriceLowHigh)),
        (ProductSortOption.PriceHighToLow, nameof(Strings.Sort_PriceHighLow)),
        (ProductSortOption.NameAToZ, nameof(Strings.Sort_NameAZ)),
        (ProductSortOption.NameZToA, nameof(Strings.Sort_NameZA)),
    ];

    public ShopSortSelectTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
        Services.AddSingleton(localizer);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_Always_OffersAllFiveSpecSortOptions()
    {
        var cut = Render<ShopSortSelect<ProductSortOption>>(p => p
            .Add(c => c.Sort, ProductSortOption.NewestFirst)
            .Add(c => c.Options, FiveProductOptions));

        cut.FindAll("[role='option']").Should().HaveCount(5);
        cut.FindAll("[role='option'] span").Select(option => option.TextContent)
            .Should().Equal(FiveProductOptions.Select(option => option.LabelKey));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithNewestFirst_SelectsNewestFirstAsCurrentValue()
    {
        var cut = Render<ShopSortSelect<ProductSortOption>>(p => p
            .Add(c => c.Sort, ProductSortOption.NewestFirst)
            .Add(c => c.Options, FiveProductOptions));

        cut.FindComponent<ShopSelect<ProductSortOption>>().Instance.Value.Should().Be(ProductSortOption.NewestFirst);
        cut.Find("[aria-selected='true']").TextContent.Should().Contain(nameof(Strings.Sort_Newest));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ChangeSort_WhenUserSelectsADifferentOption_InvokesSortChangedWithTheNewOption()
    {
        ProductSortOption? received = null;
        var cut = Render<ShopSortSelect<ProductSortOption>>(p => p
            .Add(c => c.Sort, ProductSortOption.NewestFirst)
            .Add(c => c.Options, FiveProductOptions)
            .Add(c => c.SortChanged, sort => received = sort));

        var select = cut.FindComponent<ShopSelect<ProductSortOption>>();
        await cut.InvokeAsync(() => select.Instance.SelectAsync(cut.FindAll("[role='option']")[1].Id));

        received.Should().Be(ProductSortOption.PriceLowToHigh);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7: Render_Always_OffersAllFiveSpecSortOptions, ChangeSort_WhenUserSelectsADifferentOption_*
