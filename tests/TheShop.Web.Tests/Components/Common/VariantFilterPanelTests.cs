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

public class VariantFilterPanelTests : TestContext
{
    public VariantFilterPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(c => new LocalizedString(c.Arg<string>(), c.Arg<string>()));
        Services.AddSingleton(localizer);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Render_MerchantLabels_AreEncodedAndAssociatedWithNativeCheckboxes()
    {
        var group = new FilterGroupDto("variant:colour", "Filter_Variant", FilterKind.MultiSelect,
            [new("red", "<script>Red</script>", null)], null, "Colour <img src=x onerror=alert(1)>");
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [group])
            .Add(c => c.SelectedFilters, [new("variant:colour", ["red"])])
            .Add(c => c.InitiallyExpanded, true));

        cut.FindAll("script, img").Should().BeEmpty();
        var options = cut.Find("[role=group]");
        cut.Find("[id='" + options.GetAttribute("aria-labelledby") + "']").TextContent.Should().Be(group.Label);
        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.HasAttribute("checked").Should().BeTrue();
        cut.Find("label[for='" + checkbox.Id + "']").TextContent.Should().Contain("<script>Red</script>");
        cut.Find("h3").TextContent.Should().Contain("Colour");
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Toggle_MultiSelect_ReportsOnlyChangedOptionAndKeepsOtherSelections()
    {
        var toggles = new List<FilterToggle>();
        var group = new FilterGroupDto("variant:colour", "Filter_Variant", FilterKind.MultiSelect,
            [new("red", "Red", null), new("blue", "Blue", null)], null, "Colour");
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups, [group])
            .Add(c => c.SelectedFilters, [new("variant:colour", ["red", "blue"])])
            .Add(c => c.FilterToggled, (FilterToggle t) => toggles.Add(t)));

        cut.FindAll("input[type=checkbox]")[0].Change(false);

        toggles.Should().ContainSingle().Which.Should().BeEquivalentTo(new FilterToggle("variant:colour", "red", false));
        cut.Render(p => p.Add(c => c.SelectedFilters, [new("variant:colour", ["blue"])]));
        cut.FindAll("input[type=checkbox]").Select(i => i.HasAttribute("checked")).Should().Equal(false, true);
        await cut.InvokeAsync(() => { });
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Render_NoVariantGroups_PreservesFixedGroupLabels()
    {
        var cut = Render<ShopFilterPanel>(p => p.Add(c => c.Groups,
            [new("brand", "Filter_Brand", FilterKind.MultiSelect, [new("id", "Brand", null)], null)]));

        cut.Find("h3").TextContent.Should().Be("Filter_Brand");
        cut.FindAll("[data-filter-key^='variant:']").Should().BeEmpty();
        cut.FindAll("input[type=checkbox]").Should().HaveCount(1);
    }
}
