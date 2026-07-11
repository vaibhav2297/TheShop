using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions;
using NSubstitute;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Enums;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

/// <summary>
/// Tests for <see cref="ProductFilterPanel"/> — renders one control per backend-driven
/// <see cref="FilterGroupDto"/> and raises the selection/price-range/clear callbacks that back
/// the catalogue's filtering (FR-6, AC-6, AC-10; plan §5 decision 4: filters are dynamic and
/// backend-driven — the UI hard-codes no filter set).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductFilterPanelTests : TestContext
{
    public ProductFilterPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));

        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
        Services.AddSingleton(localizer);
    }

    private static FilterGroupDto CategoryGroup() =>
        new(
            "category", "Filter_Category", FilterKind.MultiSelect,
            [new FilterOptionDto("cat-1", "Disposables", null), new FilterOptionDto("cat-2", "Pod Systems", null)],
            null);

    private static FilterGroupDto PriceGroup() =>
        new("price", "Filter_Price", FilterKind.Range, [], new PriceRangeDto(0m, 100m));

    // =========================================================================
    // Renders one control per backend group (plan §5 decision 4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithMultiSelectGroup_RendersOneCheckboxPerOption()
    {
        var cut = Render<ProductFilterPanel>(p => p.Add(c => c.Groups, [CategoryGroup()]));

        cut.FindComponents<MudCheckBox<bool>>().Should().HaveCount(2);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithRangeGroup_RendersRangeSliderWithBackendBounds()
    {
        var cut = Render<ProductFilterPanel>(p => p.Add(c => c.Groups, [PriceGroup()]));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        slider.Instance.Min.Should().Be(0m);
        slider.Instance.Max.Should().Be(100m);
    }

    // =========================================================================
    // Toggling a multi-select option (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ToggleOption_WhenUserChecksAnOption_RaisesSelectedFiltersChangedWithNewSelection()
    {
        IReadOnlyList<AppliedFilterDto>? received = null;
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFiltersChanged, (IReadOnlyList<AppliedFilterDto> f) => received = f));

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[0];
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));

        received.Should().ContainSingle(f => f.Key == "category" && f.Values.Contains("cat-1"));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ToggleOption_WhenUserUnchecksASelectedOption_RemovesItFromSelection()
    {
        IReadOnlyList<AppliedFilterDto>? received = null;
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])])
            .Add(c => c.SelectedFiltersChanged, (IReadOnlyList<AppliedFilterDto> f) => received = f));

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[0];
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));

        received.Should().NotContain(f => f.Key == "category" && f.Values.Contains("cat-1"));
    }

    // =========================================================================
    // Clear filters (AC-10 — "a way to clear the filters")
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithSelectedFilters_ShowsClearFiltersButton()
    {
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])]));

        cut.Markup.Should().Contain(Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithoutSelectedFilters_DoesNotShowClearFiltersButton()
    {
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, []));

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithPriceBoundApplied_ShowsClearFiltersButton()
    {
        // A narrowed price range is an active filter, so Clear must appear even when no
        // multi-select option is selected (otherwise a price-only filter can't be cleared).
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.SelectedFilters, [])
            .Add(c => c.PriceMin, 25m));

        cut.Markup.Should().Contain(Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithDefaultPriceRangeAndNoSelection_DoesNotShowClearFiltersButton()
    {
        // Price untouched (both bounds null) with nothing selected is the pristine state.
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.SelectedFilters, []));

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ClickClearFilters_WhenActivated_InvokesOnClearFiltersCallback()
    {
        var invoked = false;
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])])
            .Add(c => c.OnClearFilters, () => invoked = true));

        cut.FindAll("button").First(b => b.TextContent.Trim() == Strings.Filter_Clear).Click();

        invoked.Should().BeTrue();
    }

    // =========================================================================
    // Price range commit (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task PriceRangeChange_WhenMinThumbMovesAwayFromBoundary_InvokesPriceMinChangedWithNewValue()
    {
        decimal? received = null;
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.DebounceInterval, 0)
            .Add(c => c.PriceMinChanged, (decimal? min) => received = min));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        await cut.InvokeAsync(() => slider.Instance.ValueChanged.InvokeAsync(25m));

        received.Should().Be(25m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task PriceRangeChange_WhenMinThumbReturnsToLowerBoundary_InvokesPriceMinChangedWithNull()
    {
        decimal? received = 25m;
        var cut = Render<ProductFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.DebounceInterval, 0)
            .Add(c => c.PriceMin, 25m)
            .Add(c => c.PriceMinChanged, (decimal? min) => received = min));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        // Moving the thumb back to the range's own minimum means "no lower bound" (null).
        await cut.InvokeAsync(() => slider.Instance.ValueChanged.InvokeAsync(0m));

        received.Should().BeNull();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: ToggleOption_WhenUserChecksAnOption_RaisesSelectedFiltersChangedWithNewSelection,
//        ToggleOption_WhenUserUnchecksASelectedOption_RemovesItFromSelection,
//        PriceRangeChange_WhenMinThumbMovesAwayFromBoundary_*, PriceRangeChange_WhenMinThumbReturnsToLowerBoundary_*
// AC-10: Render_WithSelectedFilters_ShowsClearFiltersButton, ClickClearFilters_WhenActivated_InvokesOnClearFiltersCallback
