using System.Globalization;
using Bunit;
using FluentAssertions;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopMoneyFieldTests : TestContext
{
    private sealed class Model { public decimal? Value { get; set; } = 1234.5m; }

    private IRenderedComponent<ShopMoneyField> Field(Model model, Action<ComponentParameterCollectionBuilder<ShopMoneyField>>? configure = null) =>
        Render<ShopMoneyField>(p =>
        {
            p.Add(x => x.Label, Strings.AddProduct_PriceLabel).Add(x => x.Value, model.Value)
                .Add(x => x.ValueExpression, () => model.Value).Add(x => x.ValueChanged, value => model.Value = value);
            configure?.Invoke(p);
        });

    [Theory]
    [InlineData("en-CA")]
    [InlineData("de-DE")]
    public void Editor_AnyAmbientCulture_DisplaysCadAndEditsPlainDecimals(string culture)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var model = new Model();
            var cut = Field(model);
            cut.Find("input").GetAttribute("value").Should().Be(CurrencyFormatter.Format(1234.5m));
            cut.Find("input").Focus();
            cut.Find("input").GetAttribute("value").Should().Be("1234.5");
            cut.Find("input").Input("24.5678");
            cut.Find("input").Blur();
            model.Value.Should().Be(24.5678m);
            cut.Find("input").GetAttribute("value").Should().Be(CurrencyFormatter.Format(24.5678m));
            cut.Find("input").Focus();
            cut.Find("input").GetAttribute("value").Should().Be("24.5678");
            cut.Markup.Should().NotContain("mud-");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-1", true)]
    [InlineData("invalid", true)]
    public void Editor_MoneyBounds_PreservesZeroAndRejectsInvalidAmounts(string draft, bool invalid)
    {
        var model = new Model();
        var cut = Field(model);
        cut.Find("input").Input(draft);
        cut.Find("input").Blur();
        model.Value.Should().Be(invalid ? 1234.5m : 0m);
        cut.Find("input").HasAttribute("aria-invalid").Should().Be(invalid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_EmptyAmount_PreservesOptionalAndRequiredSemantics(bool required)
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Required, required).Add(x => x.RequiredError, Strings.Product_PriceRequired));
        cut.Find("input").Input("");
        await cut.InvokeAsync(async () => (await cut.Instance.ValidateAsync()).Should().Be(!required));
        model.Value.Should().Be(required ? 1234.5m : null);
        if (required) cut.Find(".shop-field-error").TextContent.Should().Be(Strings.Product_PriceRequired);
    }

    [Fact]
    public void Attributes_ConsumerStylesAndDisabled_ReachNativeInput()
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Disabled, true).AddUnmatched("class", "price-custom")
            .AddUnmatched("id", "price").AddUnmatched("name", "originalPrice"));
        cut.Find("input").ClassList.Should().Contain("price-custom");
        cut.Find("input").Id.Should().Be("price");
        cut.Find("input").GetAttribute("name").Should().Be("originalPrice");
        cut.Find("input").HasAttribute("disabled").Should().BeTrue();
        cut.Find("input").Input("25");
        cut.Find("input").Blur();
        model.Value.Should().Be(1234.5m);
    }
}
