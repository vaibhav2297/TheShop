using System.Linq.Expressions;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopNumericFieldTests : TestContext
{
    private sealed class Model { public decimal? Value { get; set; } = 20m; }

    private IRenderedComponent<ShopNumericField> Field(Model model, Action<ComponentParameterCollectionBuilder<ShopNumericField>>? configure = null) =>
        Render<ShopNumericField>(p =>
        {
            p.Add(x => x.Label, "Amount").Add(x => x.Value, model.Value)
                .Add(x => x.ValueExpression, () => model.Value).Add(x => x.ValueChanged, value => model.Value = value);
            configure?.Invoke(p);
        });

    [Fact]
    public void Editor_FocusEnterBlurEscape_PreservesDraftUntilCommit()
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.ValueFormatter, value => "$" + value));
        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be("$20");
        input.Focus();
        input.GetAttribute("value").Should().Be("20");
        input.Input("3.");
        model.Value.Should().Be(20m);
        cut.Render();
        input.GetAttribute("value").Should().Be("3.");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        input.GetAttribute("value").Should().Be("20");
        input.Input("33.125");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        model.Value.Should().Be(33.125m);
        input.GetAttribute("value").Should().Be("33.125");
        input.Input("34");
        input.Blur();
        model.Value.Should().Be(34m);
        input.GetAttribute("value").Should().Be("$34");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("$30")]
    [InlineData("1,234.50")]
    [InlineData("1e2")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("-1")]
    [InlineData("101")]
    public void Editor_InvalidDraft_RetainsTextAndCommittedValue(string draft)
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Min, 0m).Add(x => x.Max, 100m));
        var input = cut.Find("input");
        input.Focus();
        input.Input(draft);
        input.Blur();
        model.Value.Should().Be(20m);
        input.GetAttribute("value").Should().Be(draft);
        input.GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("#" + input.GetAttribute("aria-describedby")).TextContent.Should().NotBeEmpty();
        input.Focus();
        input.GetAttribute("value").Should().Be(draft);
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });
        input.GetAttribute("value").Should().Be("20");
        input.HasAttribute("aria-invalid").Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editor_EmptyDraft_RespectsNullableAndRequiredContracts(bool required)
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Required, required));
        var input = cut.Find("input");
        input.Input("");
        input.Blur();
        model.Value.Should().Be(required ? 20m : null);
        input.HasAttribute("aria-invalid").Should().Be(required);
    }

    [Fact]
    public void Editor_NoBounds_PreservesNegativeAndHighPrecisionNumbers()
    {
        var model = new Model();
        var cut = Field(model);
        cut.Find("input").Input("-123.456789");
        cut.Find("input").Blur();
        model.Value.Should().Be(-123.456789m);
    }

    [Fact]
    public void Parameters_ExternalReset_ReplacesInvalidDraft()
    {
        var model = new Model();
        var cut = Field(model);
        cut.Find("input").Input("bad");
        cut.Find("input").Blur();
        cut.Render(p => p.Add(x => x.Value, 40m));
        cut.Find("input").GetAttribute("value").Should().Be("40");
        cut.Find("input").HasAttribute("aria-invalid").Should().BeFalse();
    }

    [Fact]
    public void Attributes_DisabledAndOwnedBehavior_CannotBeOverridden()
    {
        var model = new Model();
        var cut = Field(model, p => p.Add(x => x.Disabled, true).Add(x => x.HelperText, "Hint")
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object>
            {
                ["id"] = "number",
                ["name"] = "amount",
                ["class"] = "custom",
                ["style"] = "width:100%",
                ["disabled"] = false,
                ["type"] = "number",
                ["value"] = "wrong",
                ["aria-label"] = "wrong",
                ["aria-describedby"] = "external number-hint",
                ["oninput"] = EventCallback.Factory.Create<ChangeEventArgs>(this, _ => model.Value = 99m)
            }));
        var input = cut.Find("input");
        input.Id.Should().Be("number");
        input.GetAttribute("name").Should().Be("amount");
        input.GetAttribute("type").Should().Be("text");
        input.GetAttribute("inputmode").Should().Be("decimal");
        input.GetAttribute("value").Should().Be("20");
        input.HasAttribute("disabled").Should().BeTrue();
        input.ClassList.Should().Contain("custom");
        input.GetAttribute("style").Should().Be("width:100%");
        input.HasAttribute("aria-label").Should().BeFalse();
        input.GetAttribute("aria-describedby").Should().Be("external number-hint number-error");
        cut.Find("label").GetAttribute("for").Should().Be("number");
        input.Input("30");
        input.Blur();
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        model.Value.Should().Be(20m);
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public async Task ValidateAsync_PendingEdit_AwaitsConsumerBeforeReturning()
    {
        var model = new Model();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<ShopNumericField>(p => p.Add(x => x.Label, "Amount").Add(x => x.Value, model.Value)
            .Add(x => x.ValueExpression, () => model.Value).Add(x => x.ValueChanged, async value =>
        {
            entered.SetResult();
            await completion.Task;
            model.Value = value;
        }));
        cut.Find("input").Input("45");
        var validation = cut.InvokeAsync(() => cut.Instance.ValidateAsync());
        await entered.Task;
        validation.IsCompleted.Should().BeFalse();
        completion.SetResult();
        await validation;
        model.Value.Should().Be(45m);
    }

    [Fact]
    public async Task Validation_EditContext_TracksParsingErrorsAndCommittedChanges()
    {
        var model = new Model();
        var context = new EditContext(model);
        var identifier = new FieldIdentifier(model, nameof(Model.Value));
        var cut = Render<EditForm>(p => p.Add(x => x.EditContext, context)
            .Add(x => x.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<ShopNumericField>(0);
                builder.AddAttribute(1, "Label", "Amount");
                builder.AddAttribute(2, "Value", model.Value);
                builder.AddAttribute(3, "ValueExpression", (Expression<Func<decimal?>>)(() => model.Value));
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, value => model.Value = value));
                builder.CloseComponent();
            })));
        cut.Find("input").Input("bad");
        await cut.InvokeAsync(() => context.Validate().Should().BeFalse());
        context.GetValidationMessages(identifier).Should().Contain(Strings.Numeric_Invalid);
        model.Value.Should().Be(20m);
        cut.Find("input").Input("35.25");
        await cut.InvokeAsync(() => cut.FindComponent<ShopNumericField>().Instance.ValidateAsync());
        model.Value.Should().Be(35.25m);
        context.IsModified(identifier).Should().BeTrue();
        context.GetValidationMessages(identifier).Should().BeEmpty();
    }

    [Fact]
    public async Task Virtualization_RowOwnedDraft_PreservesInvalidTextAcrossRemount()
    {
        var model = new Model();
        var draft = new ShopNumericDraft();
        var first = Field(model, p => p.Add(x => x.DraftState, draft));
        first.Find("input").Input("bad");
        first.Find("input").Blur();
        first.Dispose();
        var second = Field(model, p => p.Add(x => x.DraftState, draft));
        second.Find("input").GetAttribute("value").Should().Be("bad");
        second.Find("input").GetAttribute("aria-invalid").Should().Be("true");
        await second.InvokeAsync(async () => (await second.Instance.ValidateAsync()).Should().BeFalse());
    }

    [Theory]
    [InlineData("35.125", true)]
    [InlineData("bad", false)]
    public async Task Virtualization_UnmountedPendingDraft_StillParticipatesInSave(string draft, bool valid)
    {
        var model = new Model();
        var state = new ShopNumericDraft();
        var cut = Field(model, p => p.Add(x => x.DraftState, state));
        var editor = cut.Instance;
        cut.Find("input").Input(draft);
        cut.Dispose();
        await cut.InvokeAsync(async () => (await editor.ValidateAsync()).Should().Be(valid));
        model.Value.Should().Be(valid ? 35.125m : 20m);
        var remounted = Field(model, p => p.Add(x => x.DraftState, state));
        remounted.Find("input").GetAttribute("value").Should().Be(draft);
        remounted.Find("input").HasAttribute("aria-invalid").Should().Be(!valid);
    }

    [Fact]
    public void Parameters_UnnamedFieldOrReversedBounds_RejectsConfiguration()
    {
        var model = new Model();
        Action unnamed = () => Render<ShopNumericField>(p => p.Add(x => x.ValueExpression, () => model.Value));
        Action reversed = () => Field(model, p => p.Add(x => x.Min, 2m).Add(x => x.Max, 1m));
        unnamed.Should().Throw<ArgumentException>();
        reversed.Should().Throw<ArgumentOutOfRangeException>();
    }
}
