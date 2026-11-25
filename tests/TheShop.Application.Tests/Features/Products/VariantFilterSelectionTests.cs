using FluentAssertions;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Filtering;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products;

public class VariantFilterSelectionTests
{
    private static FilterGroupDto Group(string key, params string[] values) =>
        new(key, "Filter_Variant", FilterKind.MultiSelect,
            [.. values.Select(v => new FilterOptionDto(v, v, null))], null, key);

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Reconcile_RepeatedGroupsAndValues_MergesWithoutDroppingOtherGroups()
    {
        var result = VariantFilterSelection.Reconcile(
            [new("variant:colour", ["red", "blue"]), new("variant:colour", ["red"]), new("variant:size", ["l"])],
            [Group("variant:colour", "red", "blue"), Group("variant:size", "l")]);

        result.Filters.Should().HaveCount(2);
        result.Filters.Single(f => f.Key == "variant:colour").Values.Should().BeEquivalentTo(["red", "blue"]);
        result.Filters.Single(f => f.Key == "variant:size").Values.Should().Equal("l");
        result.RemovedSelections.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Reconcile_UnknownGroupAndRemovedValue_KeepsValidChoicesAndReportsRemoval()
    {
        var result = VariantFilterSelection.Reconcile(
            [new("variant:colour", ["red", "green"]), new("variant:removed", ["x"]), new("brand", ["brand-id"])],
            [Group("variant:colour", "red", "blue")]);

        result.Filters.Should().ContainSingle().Which.Values.Should().Equal("red");
        result.RemovedSelections.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Reconcile_EmptyGroupAndFixedFilters_ImposesNoVariantRestriction()
    {
        var result = VariantFilterSelection.Reconcile(
            [new("variant:colour", []), new("brand", ["id"])], [Group("variant:colour", "red")]);

        result.Filters.Should().BeEmpty();
        result.RemovedSelections.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Reconcile_DatabaseTokens_UsesExactKeysWithoutSecondNormalization()
    {
        var result = VariantFilterSelection.Reconcile(
            [new("variant:colour", ["Red", " red ", "red"]), new("variant:color", ["blue"])],
            [Group("variant:colour", "red"), Group("variant:color", "blue")]);

        result.Filters.Single(f => f.Key == "variant:colour").Values.Should().Equal("red");
        result.Filters.Single(f => f.Key == "variant:color").Values.Should().Equal("blue");
        result.RemovedSelections.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Reconcile_NoEligibleGroups_RemovesOldSelections()
    {
        var result = VariantFilterSelection.Reconcile([new("variant:size", ["l"])], []);

        result.Filters.Should().BeEmpty();
        result.RemovedSelections.Should().BeTrue();
    }

    [Theory]
    [InlineData("variant:colour", true)]
    [InlineData("variant:brand", true)]
    [InlineData("brand", false)]
    [InlineData("colour", false)]
    [InlineData("Variant:colour", false)]
    [InlineData(null, false)]
    [Trait("Feature", "002_variant-filters")]
    public void IsVariantKey_PrefixSeparatesFixedCriteria(string? key, bool expected) =>
        ProductFilterKeys.IsVariantKey(key).Should().Be(expected);
}
