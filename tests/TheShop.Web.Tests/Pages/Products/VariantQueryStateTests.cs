using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using TheShop.Application.Common.Filtering;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Pages.Products;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

public class VariantQueryStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "002_variant-filters")]
    public void QueryState_DynamicRepeatedKeys_RoundTripsWithExistingCriteria(bool admin)
    {
        var query = new Dictionary<string, StringValues>
        {
            ["variant:colour"] = new StringValues(["red", "blue"]),
            ["variant:nicotine strength"] = "20mg",
            ["brand"] = "brand-id",
            ["price_min"] = "10.25",
            ["price_max"] = "50",
            ["sort"] = "price-desc",
            ["page"] = "3",
            ["search"] = "needle",
            ["status"] = "inactive",
        };
        if (admin)
        {
            var state = ProductQueryState.FromQuery(query);
            ProductQueryState.FromQuery(Serialize(state)).Should().BeEquivalentTo(state);
            state.Search.Should().Be("needle");
            state.Status.Should().Be(ProductStatusFilter.Inactive);
            state.Filters.Single(f => f.Key == "variant:colour").Values.Should().Equal("red", "blue");
        }
        else
        {
            var state = CatalogueQueryState.FromQuery(query);
            CatalogueQueryState.FromQuery(Serialize(state)).Should().BeEquivalentTo(state);
            state.Filters.Single(f => f.Key == "variant:colour").Values.Should().Equal("red", "blue");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "002_variant-filters")]
    public void Toggle_OneValueThenFinalValue_RemovesOnlyThatRestrictionAndResetsPage(bool admin)
    {
        IReadOnlyList<AppliedFilterDto> filters = [new("variant:colour", ["red", "blue"]), new("variant:size", ["l"]), new("brand", ["id"])];
        IReadOnlyList<AppliedFilterDto> remaining;
        int page;
        if (admin)
        {
            var initial = ProductQueryState.Default with { Filters = filters, Page = 3 };
            var first = initial.ToggleFilter("variant:colour", "red", false);
            first.Filters.Single(f => f.Key == "variant:colour").Values.Should().Equal("blue");
            var final = first.ToggleFilter("variant:colour", "blue", false);
            remaining = final.Filters; page = final.Page;
        }
        else
        {
            var initial = CatalogueQueryState.Default with { Filters = filters, Page = 3 };
            var first = initial.ToggleFilter("variant:colour", "red", false);
            first.Filters.Single(f => f.Key == "variant:colour").Values.Should().Equal("blue");
            var final = first.ToggleFilter("variant:colour", "blue", false);
            remaining = final.Filters; page = final.Page;
        }
        remaining.Select(f => f.Key).Should().BeEquivalentTo(["variant:size", "brand"]);
        page.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "002_variant-filters")]
    public void QueryState_EmptyAndUnrecognizedUrlFields_LeavesExistingControlsUsable(bool admin)
    {
        var query = new Dictionary<string, StringValues> { ["variant:size"] = new StringValues(["", " "]), ["unrelated"] = "x", ["page"] = "-1" };
        var filters = admin ? ProductQueryState.FromQuery(query).Filters : CatalogueQueryState.FromQuery(query).Filters;
        filters.Should().BeEmpty();
        ProductQueryState.FromQuery(query).Page.Should().Be(1);
        CatalogueQueryState.FromQuery(query).Page.Should().Be(1);
    }

    private static Dictionary<string, StringValues> Serialize<T>(T state) where T : IUrlQueryState<T> =>
        state.ToQueryParameters().ToDictionary(p => p.Key, p => p.Value is string[] values
            ? new StringValues(values) : new StringValues(Convert.ToString(p.Value, CultureInfo.InvariantCulture)));
}
