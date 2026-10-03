using System.Linq.Expressions;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Domain.Enums;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopSelectTests : TestContext
{
    private static readonly IReadOnlyList<ShopSelectOption<string?>> Options =
    [new("one", "Selection 1"), new("two", "Selection 2"), new("disabled", "Unavailable", true)];

    public ShopSelectTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Render_EmptyAndSelectedValues_UsesLabelledReadOnlyComboboxWithStableIds()
    {
        var model = new Model();
        var cut = RenderSelect(model);
        var other = RenderSelect(model);
        var trigger = cut.Find("[role='combobox']");
        trigger.TagName.Should().Be("BUTTON");
        trigger.GetAttribute("type").Should().Be("button");
        trigger.Id.Should().NotBeNullOrWhiteSpace().And.NotBe(other.Find("button").Id);
        cut.Find("label").GetAttribute("for").Should().Be(trigger.Id);
        trigger.GetAttribute("aria-labelledby").Should().Be(cut.Find("label").Id);
        trigger.GetAttribute("aria-controls").Should().Be(cut.Find("[role='listbox']").Id);
        trigger.GetAttribute("aria-expanded").Should().Be("false");
        cut.FindAll("input, textarea, [contenteditable], [aria-selected='true']").Should().BeEmpty();
        cut.FindAll("[role='option']").Should().HaveCount(3);
        cut.Find(".shop-select-value").TextContent.Should().BeEmpty();
        var id = trigger.Id;
        cut.Render(p => p.Add(c => c.Value, "two"));
        cut.Find("button").Id.Should().Be(id);
        cut.Find(".shop-select-value").TextContent.Should().Be("Selection 2");
        cut.Find("[aria-selected='true']").TextContent.Should().Contain("Selection 2");
        cut.Find(".shop-select-control").ClassList.Should().Contain("shop-select-populated");
        cut.FindAll("svg").Should().OnlyContain(icon => icon.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public async Task Select_AvailableOption_CommitsOnceAndRejectsDisabledRemovedOrUnknownOptions()
    {
        var model = new Model();
        var calls = 0;
        var cut = RenderSelect(model, value => { model.Value = value; calls++; });
        var ids = cut.FindAll("[role='option']").Select(option => option.Id).ToArray();
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(ids[1]));
        model.Value.Should().Be("two");
        calls.Should().Be(1);
        cut.Find(".shop-select-value").TextContent.Should().Be("Selection 2");
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(ids[1]));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(ids[2]));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync("unknown"));
        calls.Should().Be(1);
        cut.Render(p => p.Add(c => c.Disabled, true).Add(c => c.Value, model.Value));
        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(ids[0]));
        calls.Should().Be(1);
        cut.Render(p => p.Add(c => c.Disabled, false).Add(c => c.Options, [Options[1]]));
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(ids[0]));
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Options_ReorderAndRemove_PreserveIdentityWithoutSilentlyChangingValue()
    {
        var model = new Model { Value = "one" };
        var cut = RenderSelect(model, value => model.Value = value);
        var firstId = cut.FindAll("[role='option']")[0].Id;
        cut.Render(p => p.Add(c => c.Options, [Options[1], Options[0]]));
        cut.FindAll("[role='option']")[1].Id.Should().Be(firstId);
        cut.Render(p => p.Add(c => c.Options, [Options[1]]));
        cut.Find(".shop-select-value").TextContent.Should().BeEmpty();
        model.Value.Should().Be("one");
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(firstId));
        model.Value.Should().Be("one");
    }

    [Fact]
    public void Attributes_ConflictingOwnedValues_CannotEnableEditingOrReplaceSemantics()
    {
        var model = new Model();
        var attributes = new Dictionary<string, object>
        {
            ["ID"] = "choice",
            ["name"] = "choice-name",
            ["class"] = "custom-choice",
            ["style"] = "--probe: 1;",
            ["data-testid"] = "choice-test",
            ["type"] = "submit",
            ["aria-label"] = "wrong",
            ["aria-labelledby"] = "wrong",
            ["role"] = "textbox",
            ["contenteditable"] = true,
            ["disabled"] = false,
            ["aria-expanded"] = "true",
            ["aria-controls"] = "wrong",
            ["aria-activedescendant"] = "wrong",
            ["tabindex"] = "-1",
            ["aria-describedby"] = "external choice-hint external",
            ["value"] = "wrong"
        };
        var cut = Render<ShopSelect<string?>>(p => p
            .Add(c => c.Label, Strings.Sort_Label).Add(c => c.Options, Options)
            .Add(c => c.ValueExpression, () => model.Value).Add(c => c.Disabled, true)
            .Add(c => c.HelperText, Strings.Login_Instruction).Add(c => c.AdditionalAttributes, attributes));
        var trigger = cut.Find("button");
        trigger.Id.Should().Be("choice");
        trigger.GetAttribute("name").Should().Be("choice-name");
        trigger.GetAttribute("type").Should().Be("button");
        trigger.GetAttribute("role").Should().Be("combobox");
        trigger.GetAttribute("aria-expanded").Should().Be("false");
        trigger.GetAttribute("aria-controls").Should().Be("choice-options");
        trigger.GetAttribute("aria-labelledby").Should().Be("choice-label");
        trigger.GetAttribute("aria-describedby").Should().Be("external choice-hint");
        trigger.GetAttribute("style").Should().Be("--probe: 1;");
        trigger.ClassList.Should().Contain("shop-select-trigger").And.Contain("custom-choice");
        trigger.HasAttribute("disabled").Should().BeTrue();
        string[] excluded = ["contenteditable", "aria-label", "aria-activedescendant", "tabindex", "value"];
        excluded.Should().OnlyContain(name => !trigger.HasAttribute(name));
        attributes["aria-labelledby"].Should().Be("wrong");
        cut.Find("#choice-hint").TextContent.Should().Be(Strings.Login_Instruction);
    }

    [Fact]
    public async Task Validation_Selection_NotifiesEditContextAndAssociatesOwnedErrors()
    {
        var model = new Model();
        var context = new EditContext(model);
        var field = new FieldIdentifier(model, nameof(Model.Value));
        var messages = new ValidationMessageStore(context);
        var notifications = 0;
        context.OnFieldChanged += (_, _) => notifications++;
        var form = Render<EditForm>(p => p.Add(c => c.EditContext, context)
            .Add(c => c.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<ShopSelect<string?>>(0);
                builder.AddAttribute(1, "Label", Strings.Sort_Label);
                builder.AddAttribute(2, "Options", Options);
                builder.AddAttribute(3, "Value", model.Value);
                builder.AddAttribute(4, "ValueExpression", (Expression<Func<string?>>)(() => model.Value));
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, value => model.Value = value));
                builder.AddAttribute(6, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "validated", ["aria-invalid"] = "false" });
                builder.CloseComponent();
            })));
        var cut = form.FindComponent<ShopSelect<string?>>();
        await cut.InvokeAsync(() => { messages.Add(field, Strings.Email_Required); context.NotifyValidationStateChanged(); });
        cut.Find("button").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("button").ClassList.Should().Contain("invalid");
        cut.Find("button").GetAttribute("aria-describedby").Should().Contain("validated-error");
        cut.Find("#validated-error").TextContent.Should().Be(Strings.Email_Required);
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(cut.FindAll("[role='option']")[0].Id));
        notifications.Should().Be(1);
        context.IsModified(field).Should().BeTrue();
        model.Value.Should().Be("one");
        await cut.InvokeAsync(() => { messages.Clear(); context.NotifyValidationStateChanged(); });
        cut.Find("button").GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("#validated-error").TextContent.Should().BeEmpty();
    }

    [Fact]
    public async Task Select_NullableGuidAndEnumValues_PreservesTypesWithoutStringParsing()
    {
        Guid? value = null;
        var id = Guid.NewGuid();
        var guid = Render<ShopSelect<Guid?>>(p => p.Add(c => c.Label, Strings.Sort_Label)
            .Add(c => c.ValueExpression, () => value).Add(c => c.ValueChanged, next => value = next)
            .Add(c => c.Options, [new(id, "Guid value"), new(null, "None")]));
        await guid.InvokeAsync(() => guid.Instance.SelectAsync(guid.FindAll("[role='option']")[0].Id));
        value.Should().Be(id);
        await guid.InvokeAsync(() => guid.Instance.SelectAsync(guid.FindAll("[role='option']")[1].Id));
        value.Should().BeNull();
        var sort = ProductSortOption.NewestFirst;
        var enumSelect = Render<ShopSelect<ProductSortOption>>(p => p.Add(c => c.Label, Strings.Sort_Label)
            .Add(c => c.ValueExpression, () => sort).Add(c => c.ValueChanged, next => sort = next)
            .Add(c => c.Options, [new(ProductSortOption.NameAToZ, Strings.Sort_NameAZ)]));
        await enumSelect.InvokeAsync(() => enumSelect.Instance.SelectAsync(enumSelect.Find("[role='option']").Id));
        sort.Should().Be(ProductSortOption.NameAToZ);
    }

    [Fact]
    public async Task Dispose_AfterMount_DisposesBrowserModuleAndIgnoresLateSelection()
    {
        var model = new Model();
        var cut = RenderSelect(model, value => model.Value = value);
        var id = cut.Find("[role='option']").Id;
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.SelectAsync(id));
        model.Value.Should().BeNull();
        JSInterop.Invocations.Should().Contain(invocation => invocation.Identifier == "dispose");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Render_BlankLabel_RejectsUnnamedControl(string label)
    {
        var model = new Model();
        Action render = () => Render<ShopSelect<string?>>(p => p.Add(c => c.Label, label).Add(c => c.ValueExpression, () => model.Value));
        render.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Render_DuplicateValues_RejectsAmbiguousSelection()
    {
        var model = new Model();
        Action render = () => Render<ShopSelect<string?>>(p => p.Add(c => c.Label, Strings.Sort_Label)
            .Add(c => c.ValueExpression, () => model.Value).Add(c => c.Options, [Options[0], Options[0]]));
        render.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("Options");
    }

    private IRenderedComponent<ShopSelect<string?>> RenderSelect(Model model, Action<string?>? changed = null) =>
        Render<ShopSelect<string?>>(p => p.Add(c => c.Label, Strings.Sort_Label).Add(c => c.Options, Options)
            .Add(c => c.Value, model.Value).Add(c => c.ValueExpression, () => model.Value)
            .Add(c => c.ValueChanged, value => changed?.Invoke(value)));

    private sealed class Model
    {
        public string? Value { get; set; }
    }
}
