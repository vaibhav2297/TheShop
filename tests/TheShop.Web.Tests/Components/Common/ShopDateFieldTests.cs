using System.Globalization;
using System.Linq.Expressions;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopDateFieldTests : TestContext
{
    private sealed class Model { public DateOnly? Date { get; set; } = new(2000, 1, 1); }

    private IRenderedComponent<ShopDateField> Field(Model model, Action<ComponentParameterCollectionBuilder<ShopDateField>>? configure = null) =>
        Render<ShopDateField>(p =>
        {
            p.Add(x => x.Label, "Date").Add(x => x.Value, model.Date)
                .Add(x => x.ValueExpression, () => model.Date).Add(x => x.ValueChanged, value => model.Date = value);
            configure?.Invoke(p);
        });

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    public void Change_LeapDay_UsesDateOnlyAndInvariantHtml(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var model = new Model();
            var cut = Field(model);
            cut.Find("input").Change("2024-02-29");
            model.Date.Should().Be(new DateOnly(2024, 2, 29));
            cut.Find("input").GetAttribute("value").Should().Be("2024-02-29");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("2023-02-29")]
    public async Task Change_InvalidDate_PreservesValueAndBlocksValidation(string value)
    {
        var model = new Model();
        var cut = Field(model);
        cut.Find("input").Change(value);
        model.Date.Should().Be(new DateOnly(2000, 1, 1));
        await cut.InvokeAsync(() => cut.Instance.Validate().Should().BeFalse());
        cut.Find(".shop-field-error").TextContent.Should().Be(Strings.Date_Invalid);
        cut.Find("input").Change("2024-02-29");
        cut.Find("input").HasAttribute("aria-invalid").Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clear_RespectsRequiredAndNullableValues(bool required)
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Required, required));
        cut.Find("input").Change("");
        model.Date.Should().BeNull();
        await cut.InvokeAsync(() => cut.Instance.Validate().Should().Be(!required));
        cut.Find("input").HasAttribute("aria-invalid").Should().Be(required);
    }

    [Theory]
    [InlineData("1999-12-31", false)]
    [InlineData("2000-01-01", true)]
    [InlineData("2000-12-31", true)]
    [InlineData("2001-01-01", false)]
    public async Task Bounds_AreInclusiveAndValidatedBeyondBrowserHints(string value, bool valid)
    {
        var cut = Field(new(), p => p.Add(x => x.Min, new DateOnly(2000, 1, 1)).Add(x => x.Max, new DateOnly(2000, 12, 31)));
        cut.Find("input").Change(value);
        await cut.InvokeAsync(() => cut.Instance.Validate().Should().Be(valid));
        cut.Find("input").GetAttribute("min").Should().Be("2000-01-01");
        cut.Find("input").GetAttribute("max").Should().Be("2000-12-31");
    }

    [Fact]
    public void Attributes_KeepEnforcedInputContractAndAccessibleAssociations()
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Disabled, true).Add(x => x.Required, true).Add(x => x.HelperText, "Hint")
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object>
            {
                ["id"] = "dob",
                ["name"] = "birth",
                ["class"] = "custom",
                ["style"] = "width:100%",
                ["type"] = "text",
                ["disabled"] = false,
                ["required"] = false,
                ["max"] = "2099-01-01",
                ["value"] = "2099-01-01",
                ["aria-label"] = "Wrong",
                ["aria-describedby"] = "external"
            }));
        var input = cut.Find("input");
        input.GetAttribute("type").Should().Be("date");
        input.HasAttribute("data-shop-date").Should().BeTrue();
        input.GetAttribute("name").Should().Be("birth");
        input.GetAttribute("value").Should().Be("2000-01-01");
        input.HasAttribute("disabled").Should().BeTrue();
        input.HasAttribute("required").Should().BeTrue();
        input.HasAttribute("max").Should().BeFalse();
        input.HasAttribute("aria-label").Should().BeFalse();
        input.GetAttribute("aria-describedby").Should().Be("external dob-hint dob-error");
        input.ClassList.Should().Contain("custom");
        cut.Find("label").GetAttribute("for").Should().Be("dob");
        input.Change("2001-01-01");
        model.Date.Should().Be(new DateOnly(2000, 1, 1));
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void ExternalReset_ClearsParsingErrorAndUpdatesTheInput()
    {
        var cut = Field(new());
        cut.Find("input").Change("bad");
        cut.Render(p => p.Add(x => x.Value, new DateOnly(2020, 3, 4)));
        cut.Find("input").GetAttribute("value").Should().Be("2020-03-04");
        cut.Find("input").HasAttribute("aria-invalid").Should().BeFalse();
    }

    [Fact]
    public async Task EditContext_TracksRequiredParsingBoundsAndFieldChanges_AndCleansUp()
    {
        var model = new Model { Date = null };
        var context = new EditContext(model);
        var field = new FieldIdentifier(model, nameof(Model.Date));
        var show = true;
        var cut = Render<EditForm>(p => p.Add(x => x.EditContext, context)
            .Add(x => x.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                if (!show) return;
                builder.OpenComponent<ShopDateField>(0);
                builder.AddAttribute(1, "Label", "Date");
                builder.AddAttribute(2, "Value", model.Date);
                builder.AddAttribute(3, "ValueExpression", (Expression<Func<DateOnly?>>)(() => model.Date));
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<DateOnly?>(this, value => model.Date = value));
                builder.AddAttribute(5, "Required", true);
                builder.AddAttribute(6, "Max", new DateOnly(2024, 12, 31));
                builder.CloseComponent();
            })));
        await cut.InvokeAsync(() => context.Validate().Should().BeFalse());
        context.GetValidationMessages(field).Should().Contain(Strings.Date_Required);
        cut.Find("input").Change("bad");
        context.GetValidationMessages(field).Should().ContainSingle().Which.Should().Be(Strings.Date_Invalid);
        cut.Find("input").Change("2025-01-01");
        await cut.InvokeAsync(() => context.Validate().Should().BeFalse());
        cut.Find("input").Change("2024-02-29");
        await cut.InvokeAsync(() => context.Validate().Should().BeTrue());
        context.IsModified(field).Should().BeTrue();
        cut.Find("input").Change("");
        show = false;
        cut.Render();
        context.GetValidationMessages(field).Should().BeEmpty();
        context.Validate().Should().BeTrue();
    }
}
