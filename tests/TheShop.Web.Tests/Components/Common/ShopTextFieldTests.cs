using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopTextFieldTests : TestContext
{
    [Fact]
    public void Render_Defaults_CreatesStableUniqueLabelledInputsWithoutVisiblePlaceholders()
    {
        var model = new FieldModel();
        var first = Render<ShopTextField>(p => p
            .Add(c => c.Label, Strings.Email_Label)
            .Add(c => c.ValueExpression, () => model.Value));
        var second = Render<ShopTextField>(p => p
            .Add(c => c.Label, Strings.Email_Label)
            .Add(c => c.ValueExpression, () => model.Value));
        var id = first.Find("input").Id;
        id.Should().NotBeNullOrWhiteSpace().And.NotBe(second.Find("input").Id);
        first.Find("label").GetAttribute("for").Should().Be(id);
        first.Find("label").TextContent.Should().Be(Strings.Email_Label);
        first.Find("input").GetAttribute("placeholder").Should().Be(" ");
        first.FindAll("svg, .shop-field-hint, .shop-field-error").Should().BeEmpty();
        first.Render(p => p.Add(c => c.Value, "restored@example.invalid"));
        first.Find("input").Id.Should().Be(id);
        first.Find("input").GetAttribute("value").Should().Be("restored@example.invalid");
        typeof(ShopTextField).GetProperty("Placeholder").Should().BeNull();
        typeof(ShopTextField).GetProperty("Variant").Should().BeNull();
    }

    [Fact]
    public void Render_AttributesAndHint_ReachInputAndMergeAccessibleDescriptions()
    {
        var model = new FieldModel();
        var attributes = new Dictionary<string, object>
        {
            ["ID"] = "email",
            ["name"] = "customer-email",
            ["type"] = "email",
            ["autocomplete"] = "email",
            ["required"] = true,
            ["class"] = "custom-input",
            ["style"] = "--shop-test: 1;",
            ["data-testid"] = "email-probe",
            ["aria-describedby"] = "external-hint email-hint external-hint",
            ["aria-label"] = "Wrong name",
            ["aria-labelledby"] = "missing",
            ["placeholder"] = "Must not appear",
            ["disabled"] = false
        };
        var cut = Render<ShopTextField>(p => p
            .Add(c => c.Label, Strings.Email_Label)
            .Add(c => c.HelperText, Strings.Login_Instruction)
            .Add(c => c.StartIcon, ShopIcons.Outlined.Mention)
            .Add(c => c.Disabled, true)
            .Add(c => c.ValueExpression, () => model.Value)
            .Add(c => c.AdditionalAttributes, attributes));

        var input = cut.Find("input");
        input.Id.Should().Be("email");
        input.GetAttribute("name").Should().Be("customer-email");
        input.GetAttribute("type").Should().Be("email");
        input.GetAttribute("autocomplete").Should().Be("email");
        input.HasAttribute("required").Should().BeTrue();
        input.HasAttribute("disabled").Should().BeTrue();
        input.GetAttribute("placeholder").Should().Be(" ");
        input.HasAttribute("aria-label").Should().BeFalse();
        input.HasAttribute("aria-labelledby").Should().BeFalse();
        input.GetAttribute("aria-describedby").Should().Be("external-hint email-hint");
        input.ClassList.Should().Contain("shop-field-input").And.Contain("custom-input");
        input.GetAttribute("style").Should().Be("--shop-test: 1;");
        input.GetAttribute("data-testid").Should().Be("email-probe");
        cut.Find(".shop-field").HasAttribute("style").Should().BeFalse();
        cut.Find("#email-hint").TextContent.Should().Be(Strings.Login_Instruction);
        cut.Find("svg").GetAttribute("aria-hidden").Should().Be("true");
        attributes["placeholder"].Should().Be("Must not appear");
        attributes["aria-label"].Should().Be("Wrong name");
        cut.Render(p => p.Add(c => c.HelperText, (string?)null).Add(c => c.StartIcon, (string?)null));
        cut.FindAll(".shop-field-hint, svg").Should().BeEmpty();
    }

    [Fact]
    public void Input_ImmediateBindingAndDisabledGuard_PreserveValuesAndCallbacks()
    {
        var model = new FieldModel();
        var calls = 0;
        var injectedCalls = 0;
        var cut = Render<ShopTextField>(p => p
            .Add(c => c.Label, Strings.Email_Label)
            .Add(c => c.ValueExpression, () => model.Value)
            .Add(c => c.ValueChanged, value => { model.Value = value; calls++; })
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["value"] = "wrong",
                ["oninput"] = EventCallback.Factory.Create<ChangeEventArgs>(this, _ => injectedCalls++)
            }));

        cut.Find("input").Input("first@example.invalid");
        model.Value.Should().Be("first@example.invalid");
        calls.Should().Be(1);
        injectedCalls.Should().Be(0);
        cut.Render(p => p.Add(c => c.Disabled, true));
        cut.Find("input").TriggerEvent("oninput", new ChangeEventArgs { Value = "blocked" });
        model.Value.Should().Be("first@example.invalid");
        calls.Should().Be(1);
        cut.Render(p => p.Add(c => c.Disabled, false));
        cut.Find("input").Input("");
        model.Value.Should().BeEmpty();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Validation_EditContextMessages_UpdateOwnedErrorsAndInvalidState()
    {
        var model = new FieldModel();
        var context = new EditContext(model);
        var field = new FieldIdentifier(model, nameof(FieldModel.Value));
        var messages = new ValidationMessageStore(context);
        var changes = 0;
        context.OnFieldChanged += (_, args) => { if (args.FieldIdentifier.Equals(field)) changes++; };
        var cut = Render<EditForm>(p => p
            .Add(c => c.EditContext, context)
            .Add(c => c.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<ShopTextField>(0);
                builder.AddAttribute(1, nameof(ShopTextField.Label), Strings.Email_Label);
                builder.AddAttribute(2, nameof(ShopTextField.Value), model.Value);
                builder.AddAttribute(3, nameof(ShopTextField.ValueExpression), (System.Linq.Expressions.Expression<Func<string?>>)(() => model.Value));
                builder.AddAttribute(4, nameof(ShopTextField.ValueChanged), EventCallback.Factory.Create<string?>(this, value => model.Value = value));
                builder.AddAttribute(5, nameof(ShopTextField.HelperText), Strings.Login_Instruction);
                builder.AddAttribute(6, nameof(ShopTextField.AdditionalAttributes), new Dictionary<string, object>
                {
                    ["id"] = "email",
                    ["aria-describedby"] = "external",
                    ["aria-invalid"] = "false"
                });
                builder.CloseComponent();
            })));

        cut.Find("input").GetAttribute("aria-describedby").Should().Be("external email-hint email-error");
        cut.Find("input").Input("invalid");
        changes.Should().Be(1);
        model.Value.Should().Be("invalid");
        context.IsModified(field).Should().BeTrue();
        await cut.InvokeAsync(() => { messages.Add(field, Strings.Email_Invalid); context.NotifyValidationStateChanged(); });
        cut.Find("input").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("input").ClassList.Should().Contain("invalid");
        cut.Find("#email-error").TextContent.Should().Be(Strings.Email_Invalid);
        cut.Find("#email-error").GetAttribute("aria-live").Should().Be("polite");
        await cut.InvokeAsync(() => { messages.Clear(field); context.NotifyValidationStateChanged(); });
        cut.Find("#email-error").TextContent.Should().BeEmpty();
        cut.Find("input").GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("input").ClassList.Should().NotContain("invalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Render_BlankLabel_RejectsAnUnnamedField(string? label)
    {
        var model = new FieldModel();
        Action render = () => Render<ShopTextField>(p => p
            .Add(c => c.Label, label!)
            .Add(c => c.ValueExpression, () => model.Value));
        render.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(nameof(ShopTextField.Label));
    }

    private sealed class FieldModel
    {
        public string? Value { get; set; }
    }
}
