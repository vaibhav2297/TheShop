using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Domain.Enums;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

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

    [Fact]
    public async Task RangeDraft_UnrelatedParentRender_PreservesPendingPair()
    {
        var received = new TaskCompletionSource<RangeSelection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.DebounceInterval, 100).Add(c => c.RangeChanged, r => received.TrySetResult(r)));
        cut.Find(".shop-range-lower").Input("25");
        cut.Render(p => p.Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])]));
        cut.Find(".shop-range-lower").GetAttribute("value").Should().Be("25");
        (await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken))
            .Should().Be(new RangeSelection("price", 25m, null));
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("remove")]
    [InlineData("dispose")]
    [InlineData("external")]
    public async Task RangeDraft_ResetOrDisposal_CancelsPendingCommit(string operation)
    {
        var received = new List<RangeSelection>();
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [PriceGroup()])
            .Add(c => c.SelectedRanges, [new RangeSelection("price", 10m, null)])
            .Add(c => c.DebounceInterval, 500).Add(c => c.RangeChanged, r => received.Add(r)));
        cut.Find(".shop-range-lower").Input("25");
        switch (operation)
        {
            case "clear":
                cut.FindAll("button").First(b => b.TextContent.Trim() == Strings.Filter_Clear).Click();
                cut.Render(p => p.Add(c => c.SelectedRanges, []));
                cut.Find(".shop-range-lower").GetAttribute("value").Should().Be("0");
                break;
            case "remove": cut.Render(p => p.Add(c => c.Groups, [])); break;
            case "dispose": await cut.InvokeAsync(cut.Instance.Dispose); break;
            case "external": cut.Render(p => p.Add(c => c.SelectedRanges, [new RangeSelection("price", 40m, null)])); break;
        }
        await Task.Delay(750, Xunit.TestContext.Current.CancellationToken);
        received.Should().BeEmpty();
    }

    [Fact]
    public void Render_UpdatedSelectionsAndOptions_PreservesNativeLabelsAndStableIdentity()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [CategoryGroup(), StatusGroup()]));
        var inputs = cut.FindAll("input[type='checkbox']");
        var ids = inputs.Select(input => input.Id).ToArray();
        ids.Should().OnlyHaveUniqueItems();
        foreach (var input in inputs)
            cut.Find($"label[for='{input.Id}']").TextContent.Should().NotBeNullOrWhiteSpace();

        cut.Render(p => p.Add(c => c.SelectedFilters,
            [new AppliedFilterDto("category", ["cat-2"]), new AppliedFilterDto("status", ["inactive"])]));
        cut.FindAll("input[type='checkbox']").Select(input => input.Id).Should().Equal(ids);
        cut.FindAll("input[type='checkbox']").Select(input => input.HasAttribute("checked"))
            .Should().Equal(false, true, false, true);

        cut.Render(p => p.Add(c => c.SelectedFilters, []));
        cut.FindAll("input[type='checkbox']").Should().OnlyContain(input => !input.HasAttribute("checked"));
        var reversed = CategoryGroup() with { Options = CategoryGroup().Options.Reverse().ToArray() };
        cut.Render(p => p.Add(c => c.Groups, [reversed, StatusGroup()]));
        cut.FindAll("input[type='checkbox']").Select(input => input.Id).Should().Equal(ids[1], ids[0], ids[2], ids[3]);
    }

    private static FilterGroupDto RatingGroup() =>
        new("rating", "Filter_Rating", FilterKind.Range, [], new RangeFilterDto(1m, 5m));

    [Fact]
    public void NativeExpanders_KeepIndependentOpenStateAcrossSelectionAndGroupReordering()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [CategoryGroup(), StatusGroup()]));
        cut.FindAll(".shop-expander-trigger")[0].Click();
        var categoryId = cut.FindAll(".shop-expander-trigger")[0].Id;
        cut.Render(p => p.Add(c => c.SelectedFilters, [new AppliedFilterDto("category", ["cat-1"])])
            .Add(c => c.Groups, [StatusGroup(), CategoryGroup()]));
        var triggers = cut.FindAll(".shop-expander-trigger");
        triggers[0].GetAttribute("aria-expanded").Should().Be("false");
        triggers[1].Id.Should().Be(categoryId);
        triggers[1].GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("span.shop-chip").TextContent.Trim().Should().Be("1");
        cut.FindAll(".shop-expander-trigger button").Should().BeEmpty();
        cut.FindAll(".mud-expand-panel, .mud-chip").Should().BeEmpty();
    }

    // =========================================================================
    // Renders one control per backend group (plan §5 decision 4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithMultiSelectGroup_RendersOneCheckboxPerOption()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [CategoryGroup()]));

        cut.FindAll("input[type='checkbox']").Should().HaveCount(2);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithRangeGroup_RendersRangeSliderWithBackendBounds()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [PriceGroup()]));

        var slider = cut.FindComponent<ShopRangeSlider>();
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

        var checkbox = cut.FindAll("input[type='checkbox']")[0];
        await checkbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

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

        var checkbox = cut.FindAll("input[type='checkbox']")[0];
        await checkbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });

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

        var slider = cut.FindComponent<ShopRangeSlider>();
        await cut.InvokeAsync(() => slider.Find(".shop-range-lower").Input("25"));

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

        var slider = cut.FindComponent<ShopRangeSlider>();
        // Moving the thumb back to the range's own minimum means "no lower bound" (null).
        await cut.InvokeAsync(() => slider.Find(".shop-range-lower").Input("0"));

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

        var slider = cut.FindComponent<ShopRangeSlider>();
        await cut.InvokeAsync(() => slider.Find(".shop-range-lower").Input("25"));
        received.Clear();

        await cut.InvokeAsync(() => slider.Find(".shop-range-upper").Input("60"));

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

        var sliders = cut.FindComponents<ShopRangeSlider>();
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

        var sliders = cut.FindComponents<ShopRangeSlider>();
        // Price is untouched, so both its thumbs sit at the backend bounds; only rating is narrowed.
        (sliders[0].Instance.Value.Lower, sliders[0].Instance.Value.Upper).Should().Be((0m, 100m));
        (sliders[1].Instance.Value.Lower, sliders[1].Instance.Value.Upper).Should().Be((3m, 5m));
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

        var ratingSlider = cut.FindComponents<ShopRangeSlider>()[1];
        await cut.InvokeAsync(() => ratingSlider.Find(".shop-range-lower").Input("3"));

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

        cut.FindAll("input[type='checkbox']").Should().HaveCount(2);
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

        cut.FindAll("input[type='checkbox']").Should().OnlyContain(c => !c.HasAttribute("checked"));
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithSingleSelectGroupAndASelectedOption_RendersOnlyThatOptionChecked()
    {
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SelectedFilters, [new AppliedFilterDto("status", ["inactive"])]));

        var checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[0].HasAttribute("checked").Should().BeFalse("active");
        checkboxes[1].HasAttribute("checked").Should().BeTrue("inactive");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChooseSingleSelectOption_WhenUserChecksAnOption_RaisesSingleSelectChangedWithTheGroupKeyAndValue()
    {
        (string GroupKey, string? Value)? received = null;
        var cut = Render<ShopFilterPanel>(p => p
            .Add(c => c.Groups, [StatusGroup()])
            .Add(c => c.SingleSelectChanged, (ValueTuple<string, string?> t) => received = t));

        var checkbox = cut.FindAll("input[type='checkbox']")[1]; // "inactive"
        await checkbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

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

        var checkbox = cut.FindAll("input[type='checkbox']")[1]; // "inactive"
        await checkbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });

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
