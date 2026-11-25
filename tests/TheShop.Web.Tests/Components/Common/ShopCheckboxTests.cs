using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopCheckboxTests : TestContext
{
    [Fact]
    public void Render_MixedState_SynchronizesNativePropertyAndRetainsBooleanBinding()
    {
        var module = JSInterop.SetupModule("./js/shopCheckbox.js");
        module.SetupVoid("setIndeterminate", _ => true).SetVoidResult();
        var model = new CheckboxModel();
        var cut = Render<ShopCheckbox>(p => p.Add(c => c.Label, Strings.Table_SelectPage)
            .Add(c => c.HideLabel, true).Add(c => c.Indeterminate, true)
            .Add(c => c.ValueExpression, () => model.Accepted)
            .Add(c => c.ValueChanged, value => model.Accepted = value));
        cut.Find("input").GetAttribute("aria-checked").Should().Be("mixed");
        cut.Find(".shop-checkbox-text").ClassList.Should().Contain("shop-visually-hidden");
        cut.Find(".shop-checkbox-mixed").GetAttribute("aria-hidden").Should().Be("true");
        module.Invocations["setIndeterminate"].Last().Arguments[1].Should().Be(true);
        cut.Find("input").Change(true);
        model.Accepted.Should().BeTrue();
        cut.Render(p => p.Add(c => c.Indeterminate, false));
        cut.Find("input").HasAttribute("aria-checked").Should().BeFalse();
        module.Invocations["setIndeterminate"].Last().Arguments[1].Should().Be(false);
    }

    [Fact]
    public void Render_Defaults_AssociatesStableUniqueLabelsAndUsesBuiltInCheckboxBinding()
    {
        var model = new CheckboxModel();
        var first = Render<ShopCheckbox>(p => p.Add(c => c.Label, Strings.SignUp_AgeConfirm)
            .Add(c => c.ValueExpression, () => model.Accepted));
        var second = Render<ShopCheckbox>(p => p.Add(c => c.Label, Strings.SignUp_AgeConfirm)
            .Add(c => c.ValueExpression, () => model.Accepted));
        var id = first.Find("input").Id;
        id.Should().NotBeNullOrWhiteSpace().And.NotBe(second.Find("input").Id);
        first.Find("label").GetAttribute("for").Should().Be(id);
        first.Find("label").TextContent.Should().Contain(Strings.SignUp_AgeConfirm);
        first.Find("input").GetAttribute("type").Should().Be("checkbox");
        first.Find("input").GetAttribute("value").Should().Be(bool.TrueString);
        first.Instance.Element.Should().NotBeNull();
        first.Find("input").HasAttribute("checked").Should().BeFalse();
        first.Find(".shop-checkbox").ClassList.Should().Contain("shop-checkbox-medium");
        first.FindAll("svg").Should().HaveCount(2).And.OnlyContain(s => s.GetAttribute("aria-hidden") == "true");
        first.FindAll(".shop-checkbox-error").Should().BeEmpty();
        first.Render(p => p.Add(c => c.Value, true));
        first.Find("input").Id.Should().Be(id);
        first.Find("input").HasAttribute("checked").Should().BeTrue();
        typeof(ShopCheckbox).Should().BeDerivedFrom<InputCheckbox>();
    }

    [Theory]
    [InlineData(ShopSize.Small, "small")]
    [InlineData(ShopSize.Medium, "medium")]
    [InlineData(ShopSize.Large, "large")]
    public void Render_Size_UsesSharedEnumModifier(ShopSize size, string suffix)
    {
        var model = new CheckboxModel();
        var cut = Render<ShopCheckbox>(p => p.Add(c => c.Label, Strings.SignUp_AgeConfirm)
            .Add(c => c.Size, size).Add(c => c.ValueExpression, () => model.Accepted));
        cut.Find(".shop-checkbox").ClassList.Should().Contain($"shop-checkbox-{suffix}");
    }

    [Fact]
    public void Render_ConflictingAttributes_PreservesNativeSemanticsAndInputForwarding()
    {
        var model = new CheckboxModel();
        var otherCalls = 0;
        var attributes = new Dictionary<string, object>
        {
            ["ID"] = "consent",
            ["name"] = "consent",
            ["required"] = true,
            ["class"] = "custom-checkbox",
            ["style"] = "--shop-test: 1;",
            ["data-testid"] = "consent",
            ["type"] = "text",
            ["checked"] = true,
            ["disabled"] = false,
            ["aria-label"] = "Wrong",
            ["aria-labelledby"] = "missing",
            ["aria-checked"] = "mixed",
            ["role"] = "switch",
            ["aria-describedby"] = "hint",
            ["onchange"] = EventCallback.Factory.Create<ChangeEventArgs>(this, _ => otherCalls++)
        };
        var cut = Render<ShopCheckbox>(p => p.Add(c => c.Label, Strings.SignUp_AgeConfirm)
            .Add(c => c.Disabled, true).Add(c => c.ValueExpression, () => model.Accepted)
            .Add(c => c.AdditionalAttributes, attributes));
        var input = cut.Find("input");
        input.Id.Should().Be("consent");
        input.GetAttribute("type").Should().Be("checkbox");
        input.GetAttribute("name").Should().Be("consent");
        input.GetAttribute("aria-describedby").Should().Be("hint");
        input.GetAttribute("data-testid").Should().Be("consent");
        input.GetAttribute("style").Should().Be("--shop-test: 1;");
        input.ClassList.Should().Contain("custom-checkbox");
        input.HasAttribute("disabled").Should().BeTrue();
        input.HasAttribute("required").Should().BeTrue();
        input.HasAttribute("checked").Should().BeFalse();
        string[] enforcedNames = ["role", "aria-checked", "aria-label", "aria-labelledby"];
        foreach (var name in enforcedNames)
            input.HasAttribute(name).Should().BeFalse();
        input.Change(true);
        otherCalls.Should().Be(0);
        input.HasAttribute("checked").Should().BeFalse();
        attributes["aria-label"].Should().Be("Wrong");
        cut.Find(".shop-checkbox").HasAttribute("style").Should().BeFalse();
    }

    [Fact]
    public void Change_CheckedUncheckedAndDisabled_UpdatesBindingExactlyOncePerChange()
    {
        var model = new CheckboxModel();
        var calls = 0;
        var cut = Render<ShopCheckbox>(p => p.Add(c => c.Label, Strings.SignUp_AgeConfirm)
            .Add(c => c.ValueExpression, () => model.Accepted)
            .Add(c => c.ValueChanged, value => { model.Accepted = value; calls++; }));
        cut.Find("input").Change(true);
        model.Accepted.Should().BeTrue();
        calls.Should().Be(1);
        cut.Render(p => p.Add(c => c.Disabled, true));
        cut.Find("input").Change(false);
        model.Accepted.Should().BeTrue();
        calls.Should().Be(1);
        cut.Render(p => p.Add(c => c.Disabled, false));
        cut.Find("input").Change(false);
        model.Accepted.Should().BeFalse();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Validation_EditContext_TracksFieldChangesAndAssociatesErrors()
    {
        var model = new CheckboxModel();
        var context = new EditContext(model);
        var field = new FieldIdentifier(model, nameof(CheckboxModel.Accepted));
        var messages = new ValidationMessageStore(context);
        var changes = 0;
        context.OnFieldChanged += (_, args) => { if (args.FieldIdentifier.Equals(field)) changes++; };
        var cut = Render<EditForm>(p => p.Add(c => c.EditContext, context)
            .Add(c => c.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<ShopCheckbox>(0);
                builder.AddAttribute(1, nameof(ShopCheckbox.Label), Strings.SignUp_AgeConfirm);
                builder.AddAttribute(2, nameof(ShopCheckbox.ValueExpression), (System.Linq.Expressions.Expression<Func<bool>>)(() => model.Accepted));
                builder.AddAttribute(3, nameof(ShopCheckbox.ValueChanged), EventCallback.Factory.Create<bool>(this, value => model.Accepted = value));
                builder.AddAttribute(4, nameof(ShopCheckbox.AdditionalAttributes), new Dictionary<string, object>
                {
                    ["id"] = "consent",
                    ["aria-describedby"] = "hint consent-error",
                    ["aria-invalid"] = "false"
                });
                builder.CloseComponent();
            })));
        cut.Find("input").GetAttribute("aria-describedby").Should().Be("hint consent-error");
        await cut.InvokeAsync(() => { messages.Add(field, Strings.Auth_Underage); context.NotifyValidationStateChanged(); });
        cut.Find("input").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("#consent-error").TextContent.Should().Be(Strings.Auth_Underage);
        cut.Find("input").Change(true);
        model.Accepted.Should().BeTrue();
        context.IsModified(field).Should().BeTrue();
        changes.Should().Be(1);
        await cut.InvokeAsync(() => { messages.Clear(field); context.NotifyValidationStateChanged(); });
        cut.Find("#consent-error").TextContent.Should().BeEmpty();
        cut.Find("input").GetAttribute("aria-invalid").Should().NotBe("true");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Render_BlankLabel_RejectsUnnamedCheckbox(string? label)
    {
        var model = new CheckboxModel();
        Action render = () => Render<ShopCheckbox>(p => p.Add(c => c.Label, label!)
            .Add(c => c.ValueExpression, () => model.Accepted));
        render.Should().Throw<ArgumentException>();
    }

    private sealed class CheckboxModel
    {
        public bool Accepted { get; set; }
    }
}
