using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopRangeSliderTests : TestContext
{
    private readonly BunitJSModuleInterop _module;

    public ShopRangeSliderTests()
    {
        _module = JSInterop.SetupModule("./js/shopRangeSlider.js");
        _module.SetupVoid("sync", _ => true).SetVoidResult();
        _module.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private IRenderedComponent<ShopRangeSlider> Slider(Action<ComponentParameterCollectionBuilder<ShopRangeSlider>>? configure = null, ShopRangeValue? value = null) =>
        Render<ShopRangeSlider>(p =>
        {
            p.Add(x => x.Label, "Range").Add(x => x.MinimumLabel, Strings.Range_Minimum)
                .Add(x => x.MaximumLabel, Strings.Range_Maximum).Add(x => x.Value, value ?? new ShopRangeValue(20m, 80m));
            configure?.Invoke(p);
        });

    [Fact]
    public void Render_UsesNativeControlsAndCallerFormattingWithNamedThumbs()
    {
        var cut = Slider(p => p.Add(x => x.ValueFormatter, v => "$" + v)
            .Add(x => x.Class, "custom").Add(x => x.Style, "margin:1px").AddUnmatched("data-testid", "range"));
        cut.FindAll("input[type='range']").Should().HaveCount(2);
        cut.FindAll(".shop-field-input").Should().HaveCount(2);
        cut.Find(".shop-range-lower").GetAttribute("aria-valuemax").Should().Be("80");
        cut.Find(".shop-range-upper").GetAttribute("aria-valuemin").Should().Be("20");
        cut.Find(".shop-range-lower").GetAttribute("aria-valuetext").Should().Be("$20");
        cut.Find(".shop-field-input").GetAttribute("value").Should().Be("$20");
        cut.Find("fieldset").ClassList.Should().Contain("custom");
        cut.Find("fieldset").GetAttribute("data-testid").Should().Be("range");
        cut.Find("fieldset").GetAttribute("style").Should().Be("margin:1px");
        cut.Markup.Should().NotContain("mud-");
        cut.FindAll(".shop-range-bubble").Should().BeEmpty();
    }

    [Fact]
    public void Input_MovesBothBounds_EmitsAtomicPairsAndPreventsCrossing()
    {
        var values = new List<ShopRangeValue>();
        var cut = Slider(p => p.Add(x => x.ValueChanged, value => values.Add(value)));
        cut.Find(".shop-range-lower").Input("30");
        cut.Find(".shop-range-upper").Input("60");
        values.Should().Equal(new ShopRangeValue(30m, 80m), new ShopRangeValue(30m, 60m));
        cut.Find(".shop-range-lower").Input("90");
        values.Last().Should().Be(new ShopRangeValue(60m, 60m));
        cut.Find(".shop-range-upper").Input("10");
        values.Should().HaveCount(3);
        cut.Find(".shop-range-upper").GetAttribute("aria-valuemin").Should().Be("60");
    }

    [Theory]
    [InlineData("0.335", "0.34")]
    [InlineData("0.331", "0.33")]
    public void Input_DecimalStep_SnapsWithoutFloatingPointDrift(string input, string expected)
    {
        var cut = Slider(p => p.Add(x => x.Min, 0.1m).Add(x => x.Max, 0.95m)
            .Add(x => x.Step, 0.01m), new ShopRangeValue(0.1m, 0.95m));
        cut.Find(".shop-range-lower").Input(input);
        decimal.Parse(cut.Find(".shop-range-lower").GetAttribute("value")!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Input_UnalignedMaximum_RemainsSelectable()
    {
        var cut = Slider(p => p.Add(x => x.Max, 10m).Add(x => x.Step, 3m), new ShopRangeValue(0m, 9m));
        cut.Find(".shop-range-upper").Input("10");
        cut.Find(".shop-range-upper").GetAttribute("value").Should().Be("10");
    }

    [Theory]
    [InlineData("25.7400000000000", "25.74")]
    [InlineData("25.7461234567890", "25.75")]
    public void Editor_AfterCentStepDrag_ShowsOnlySignificantDecimalPlaces(string candidate, string expected)
    {
        var cut = Slider(p => p.Add(x => x.Step, 0.01m));
        cut.Find(".shop-range-lower").Input(candidate);
        var editor = cut.Find(".shop-field-input");
        editor.Focus();
        editor.GetAttribute("value").Should().Be(expected);
        editor.Input("invalid");
        editor.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        editor.GetAttribute("value").Should().Be(expected);
        cut.Find(".shop-range-upper").Input("60.1200000000000");
        var upperEditor = cut.FindAll(".shop-field-input")[1];
        upperEditor.Focus();
        upperEditor.GetAttribute("value").Should().Be("60.12");
    }

    [Fact]
    public void Editor_FinerNonPriceStep_PreservesMeaningfulPrecision()
    {
        var cut = Slider(p => p.Add(x => x.Step, 0.0001m));
        cut.Find(".shop-range-lower").Input("25.7412000000000");
        cut.Find(".shop-field-input").Focus();
        cut.Find(".shop-field-input").GetAttribute("value").Should().Be("25.7412");
    }

    [Fact]
    public void Editor_FormatsWhenIdleEditsPlainNumbersAndCommitsOnBlur()
    {
        var values = new List<ShopRangeValue>();
        var cut = Slider(p => p.Add(x => x.ValueFormatter, v => "$" + v).Add(x => x.ValueChanged, v => values.Add(v)));
        var field = cut.Find(".shop-field-input");
        field.Focus();
        field.GetAttribute("value").Should().Be("20");
        field.Input("35");
        values.Should().BeEmpty();
        field.Blur();
        values.Should().Equal(new ShopRangeValue(35m, 80m));
        cut.Find(".shop-field-input").GetAttribute("value").Should().Be("$35");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("$30")]
    [InlineData("90")]
    [InlineData("-1")]
    public void Editor_InvalidDraft_DoesNotChangeRangeAndReportsAssociatedError(string input)
    {
        var values = new List<ShopRangeValue>();
        var cut = Slider(p => p.Add(x => x.ValueChanged, v => values.Add(v)));
        var field = cut.Find(".shop-field-input");
        field.Focus();
        field.Input(input);
        field.Blur();
        field.GetAttribute("aria-invalid").Should().Be("true");
        var errorId = field.GetAttribute("aria-describedby")!;
        cut.Find("#" + errorId).TextContent.Should().NotBeEmpty();
        values.Should().BeEmpty();
        cut.Find(".shop-range-lower").GetAttribute("value").Should().Be("20");
        field.Focus();
        field.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        field.GetAttribute("value").Should().Be("20");
        field.HasAttribute("aria-invalid").Should().BeFalse();
    }

    [Fact]
    public void Editor_Enter_AllowsContinuedNumericEditing()
    {
        var cut = Slider(p => p.Add(x => x.ValueFormatter, v => "$" + v));
        var field = cut.Find(".shop-field-input");
        field.Focus();
        field.Input("33");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        field.GetAttribute("value").Should().Be("33");
        field.Input("34");
        field.Blur();
        field.GetAttribute("value").Should().Be("$34");
    }

    [Fact]
    public void Parameters_UnchangedParentRender_PreservesDraftAndExternalResetReplacesIt()
    {
        var cut = Slider();
        var field = cut.Find(".shop-field-input");
        field.Focus();
        field.Input("3.");
        cut.Render(p => p.Add(x => x.Class, "new"));
        field.GetAttribute("value").Should().Be("3.");
        cut.Render(p => p.Add(x => x.Value, new ShopRangeValue(0m, 100m)));
        field.GetAttribute("value").Should().Be("0");
    }

    [Theory]
    [InlineData(true, 0, 100)]
    [InlineData(false, 50, 50)]
    public void DisabledOrEqualBounds_RejectsSyntheticUpdates(bool disabled, int min, int max)
    {
        var values = new List<ShopRangeValue>();
        var cut = Slider(p => p.Add(x => x.Disabled, disabled).Add(x => x.Min, min).Add(x => x.Max, max)
            .Add(x => x.ValueChanged, v => values.Add(v)));
        cut.FindAll("input").Should().OnlyContain(e => e.HasAttribute("disabled"));
        cut.Find(".shop-range-lower").Input("30");
        cut.Find(".shop-field-input").Input("30");
        cut.Find(".shop-field-input").Blur();
        values.Should().BeEmpty();
    }

    [Theory]
    [InlineData(10, 0, 1)]
    [InlineData(0, 10, 0)]
    [InlineData(0, 10, -1)]
    public void Parameters_InvalidConfiguration_IsRejected(int min, int max, int step)
    {
        Action act = () => Slider(p => p.Add(x => x.Min, min).Add(x => x.Max, max).Add(x => x.Step, step));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Parameters_NegativeRangeAndExternalBounds_NormalizeWithoutCallback()
    {
        var values = new List<ShopRangeValue>();
        var cut = Slider(p => p.Add(x => x.Min, -10m).Add(x => x.Max, 10m)
            .Add(x => x.ValueChanged, v => values.Add(v)), new ShopRangeValue(-50m, 90m));
        cut.Find(".shop-range-lower").GetAttribute("value").Should().Be("-10");
        cut.Find(".shop-range-upper").GetAttribute("value").Should().Be("10");
        values.Should().BeEmpty();
        cut.Render(p => p.Add(x => x.Max, 0m));
        cut.Find(".shop-range-upper").GetAttribute("value").Should().Be("0");
    }

    [Fact]
    public async Task Dispose_RepeatedDisposal_CleansModuleOnceAndPreventsChanges()
    {
        var values = new List<ShopRangeValue>();
        var cut = Slider(p => p.Add(x => x.ValueChanged, v => values.Add(v)));
        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();
        cut.Find(".shop-range-lower").Input("40");
        values.Should().BeEmpty();
        _module.Invocations["dispose"].Should().ContainSingle();
    }
}
