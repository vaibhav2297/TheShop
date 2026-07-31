using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Domain.Enums;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using MudBlazor.Extensions;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopFilterPanel"/> — renders one control per backend-driven
/// <see cref="FilterGroupDto"/> and raises the selection/range/clear callbacks that back
/// the catalogue's filtering (FR-6, AC-6, AC-10; plan §5 decision 4: filters are dynamic and
/// backend-driven — the UI hard-codes no filter set). Originally <c>ProductFilterPanelTests</c>;
/// moved here when the panel was extracted into a feature-agnostic shared component
/// (manage-brands plan §5 Decision 4, TASK-020) — assertions are unchanged by that move.
/// manage-brands also added the <see cref="FilterKind.SingleSelect"/> group kind for its status
/// filter (plan §5 Decision 5, spec AC-4) — those tests are appended below, separately traited.
/// <see href=".specs/product-catalogue/spec.md"/>
/// <see href=".specs/manage-brands/plan.md"/>
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class ShopFilterPanelTests : TestContext
{
    public ShopFilterPanelTests()
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
        new("price", "Filter_Price", FilterKind.Range, [], new RangeFilterDto(0m, 100m));

    private static FilterGroupDto RatingGroup() =>
        new("rating", "Filter_Rating", FilterKind.Range, [], new RangeFilterDto(1m, 5m));

    // =========================================================================
    // Renders one control per backend group (plan §5 decision 4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithMultiSelectGroup_RendersOneCheckboxPerOption()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [CategoryGroup()]));

        cut.FindComponents<MudCheckBox<bool>>().Should().HaveCount(2);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithRangeGroup_RendersRangeSliderWithBackendBounds()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [PriceGroup()]));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        slider.Instance.Min.Should().Be(0m);
        slider.Instance.Max.Should().Be(100m);
    }

    // =========================================================================
    // Toggling a multi-select option (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ToggleOption_WhenUserChecksAnOption_RaisesFilterToggledForThatOptionAsSelected()
    {
        FilterToggle? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.FilterToggled, (FilterToggle t) => received = t));

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[0];
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));

        received.Should().NotBeNull();
        received!.GroupKey.Should().Be("category");
        received.Value.Should().Be("cat-1");
        received.IsSelected.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ToggleOption_WhenUserUnchecksASelectedOption_RaisesFilterToggledAsDeselected()
    {
        FilterToggle? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])])
            .Add(c => c.FilterToggled, (FilterToggle t) => received = t));

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[0];
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));

        received.Should().NotBeNull();
        received!.GroupKey.Should().Be("category");
        received.Value.Should().Be("cat-1");
        received.IsSelected.Should().BeFalse();
    }

    // =========================================================================
    // Clear filters (AC-10 — "a way to clear the filters")
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithSelectedFilters_ShowsClearFiltersButton()
    {
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])]));

        cut.Markup.Should().Contain(Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithoutSelectedFilters_DoesNotShowClearFiltersButton()
    {
        var cut = Render<ShopFilterPanel>(p => p
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
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.SelectedFilters, [])
            .Add(c => c.SelectedRanges, [new RangeSelection("price", 25m, null)]));

        cut.Markup.Should().Contain(Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithDefaultPriceRangeAndNoSelection_DoesNotShowClearFiltersButton()
    {
        // Price untouched (both bounds null) with nothing selected is the pristine state.
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.SelectedFilters, []));

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == Strings.Filter_Clear);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ClickClearFilters_WhenActivated_InvokesOnClearFiltersCallback()
    {
        var invoked = false;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [CategoryGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])])
            .Add(c => c.OnClearFilters, () => invoked = true));

        cut.FindAll("button").First(b => b.TextContent.Trim() == Strings.Filter_Clear).Click();

        invoked.Should().BeTrue();
    }

    // =========================================================================
    // Range commit (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task RangeChange_WhenMinThumbMovesAwayFromBoundary_RaisesRangeChangedWithNewValue()
    {
        RangeSelection? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.DebounceInterval, 0)
            .Add(c => c.RangeChanged, (RangeSelection r) => received = r));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        await cut.InvokeAsync(() => slider.Instance.ValueChanged.InvokeAsync(25m));

        received.Should().NotBeNull();
        received!.GroupKey.Should().Be("price");
        received.Min.Should().Be(25m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task RangeChange_WhenMinThumbReturnsToLowerBoundary_RaisesRangeChangedWithNullMin()
    {
        RangeSelection? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.DebounceInterval, 0)
            .Add(c => c.SelectedRanges, [new RangeSelection("price", 25m, null)])
            .Add(c => c.RangeChanged, (RangeSelection r) => received = r));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        // Moving the thumb back to the range's own minimum means "no lower bound" (null).
        await cut.InvokeAsync(() => slider.Instance.ValueChanged.InvokeAsync(0m));

        received.Should().NotBeNull();
        received!.Min.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task RangeChange_WhenBothThumbsMove_RaisesOneRangeChangedCarryingBothBounds()
    {
        // Both bounds travel in a single callback so the owning page performs one state update
        // per settled drag rather than one per thumb.
        var received = new List<RangeSelection>();
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.DebounceInterval, 0)
            .Add(c => c.RangeChanged, (RangeSelection r) => received.Add(r)));

        var slider = cut.FindComponent<MudRangeSlider<decimal>>();
        await cut.InvokeAsync(() => slider.Instance.ValueChanged.InvokeAsync(25m));
        received.Clear();

        await cut.InvokeAsync(() => slider.Instance.UpperValueChanged.InvokeAsync(60m));

        received.Should().ContainSingle();
        received[0].Min.Should().Be(25m);
        received[0].Max.Should().Be(60m);
    }

    // =========================================================================
    // Range groups are independent — the panel hard-codes no range filter
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithTwoRangeGroups_RendersEachSliderWithItsOwnBackendBounds()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [PriceGroup(), RatingGroup()]));

        var sliders = cut.FindComponents<MudRangeSlider<decimal>>();
        sliders.Should().HaveCount(2);
        (sliders[0].Instance.Min, sliders[0].Instance.Max).Should().Be((0m, 100m));
        (sliders[1].Instance.Min, sliders[1].Instance.Max).Should().Be((1m, 5m));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithTwoRangeGroups_PositionsEachSliderFromItsOwnAppliedBounds()
    {
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup(), RatingGroup()])
            .Add(c => c.SelectedRanges, [new RangeSelection("rating", 3m, null)]));

        var sliders = cut.FindComponents<MudRangeSlider<decimal>>();
        // Price is untouched, so both its thumbs sit at the backend bounds; only rating is narrowed.
        (sliders[0].Instance.GetState(x => x.Value), sliders[0].Instance.GetState(x => x.UpperValue)).Should().Be((0m, 100m));
        (sliders[1].Instance.GetState(x => x.Value), sliders[1].Instance.GetState(x => x.UpperValue)).Should().Be((3m, 5m));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task RangeChange_WithTwoRangeGroups_RaisesRangeChangedForTheGroupThatMoved()
    {
        RangeSelection? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [PriceGroup(), RatingGroup()])
            .Add(c => c.DebounceInterval, 0)
            .Add(c => c.RangeChanged, (RangeSelection r) => received = r));

        var ratingSlider = cut.FindComponents<MudRangeSlider<decimal>>()[1];
        await cut.InvokeAsync(() => ratingSlider.Instance.ValueChanged.InvokeAsync(3m));

        received.Should().NotBeNull();
        received!.GroupKey.Should().Be("rating");
        received.Min.Should().Be(3m);
        received.Max.Should().BeNull();
    }

    // =========================================================================
    // manage-brands: SingleSelect group — the status filter (plan §5 Decision 5, AC-4)
    // =========================================================================

    private static FilterGroupDto StatusGroup() =>
        new(
            "status", "Filter_Status", FilterKind.SingleSelect,
            [new FilterOptionDto("active", "Filter_StatusActive", null), new FilterOptionDto("inactive", "Filter_StatusInactive", null)],
            null);

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithSingleSelectGroup_RendersOneCheckboxPerOption()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [StatusGroup()]));

        cut.FindComponents<MudCheckBox<bool>>().Should().HaveCount(2);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithSingleSelectGroupAndNoSelection_RendersEveryOptionUnchecked()
    {
        // A single-select group offers no "All" option of its own — no option checked is what
        // "no narrowing" looks like (plan §5 Decision 5).
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SelectedFilters, []));

        cut.FindComponents<MudCheckBox<bool>>().Should().OnlyContain(c => !c.Instance.Value);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithSingleSelectGroupAndASelectedOption_RendersOnlyThatOptionChecked()
    {
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("status", ["inactive"])]));

        var checkboxes = cut.FindComponents<MudCheckBox<bool>>();
        checkboxes[0].Instance.GetState(x => x.Value).Should().BeFalse("active"); // rendered first per StatusGroup()'s option order
        checkboxes[1].Instance.GetState(x => x.Value).Should().BeTrue("inactive");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChooseSingleSelectOption_WhenUserChecksAnOption_RaisesSingleSelectChangedWithTheGroupKeyAndValue()
    {
        (string GroupKey, string? Value)? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SingleSelectChanged, (ValueTuple<string, string?> t) => received = t));

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[1]; // "inactive"
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));

        received.Should().NotBeNull();
        received!.Value.GroupKey.Should().Be("status");
        received.Value.Value.Should().Be("inactive");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChooseSingleSelectOption_WhenUserUnchecksTheSelectedOption_RaisesSingleSelectChangedWithANullValue()
    {
        // Unchecking the selected option is the only way to clear a single-select group — there is
        // no separate "All" option (plan §5 Decision 5).
        (string GroupKey, string? Value)? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("status", ["inactive"])])
            .Add(c => c.SingleSelectChanged, (ValueTuple<string, string?> t) => received = t));

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[1]; // "inactive"
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));

        received.Should().NotBeNull();
        received!.Value.GroupKey.Should().Be("status");
        received.Value.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithASingleSelectGroupSelected_ShowsClearFiltersButton()
    {
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("status", ["inactive"])]));

        cut.Markup.Should().Contain(Strings.Filter_Clear);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: ToggleOption_WhenUserChecksAnOption_RaisesFilterToggledForThatOptionAsSelected,
//        ToggleOption_WhenUserUnchecksASelectedOption_RaisesFilterToggledAsDeselected,
//        RangeChange_WhenMinThumbMovesAwayFromBoundary_*, RangeChange_WhenMinThumbReturnsToLowerBoundary_*,
//        RangeChange_WhenBothThumbsMove_*, RangeChange_WithTwoRangeGroups_*
// AC-10: Render_WithSelectedFilters_ShowsClearFiltersButton, ClickClearFilters_WhenActivated_InvokesOnClearFiltersCallback

// =============================================================================
// AC → Test mapping (manage-brands)
// =============================================================================
// AC-4: Render_WithSingleSelectGroup_RendersOneCheckboxPerOption,
//        Render_WithSingleSelectGroupAndNoSelection_RendersEveryOptionUnchecked,
//        Render_WithSingleSelectGroupAndASelectedOption_RendersOnlyThatOptionChecked,
//        ChooseSingleSelectOption_WhenUserChecksAnOption_RaisesSingleSelectChangedWithTheGroupKeyAndValue,
//        ChooseSingleSelectOption_WhenUserUnchecksTheSelectedOption_RaisesSingleSelectChangedWithANullValue,
//        Render_WithASingleSelectGroupSelected_ShowsClearFiltersButton
