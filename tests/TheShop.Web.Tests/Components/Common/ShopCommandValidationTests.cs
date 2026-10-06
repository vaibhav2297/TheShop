using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopCommandValidationTests : TestContext
{
    public ShopCommandValidationTests()
    {
        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), $"Localized {call.Arg<string>()}"));
        Services.AddSingleton(localizer);
    }

    [Fact]
    public async Task ShowErrors_MatchingProperties_AssignsAllFieldsWithoutMappingOrPriorEdits()
    {
        var model = new FormModel();
        var context = new EditContext(model);
        var form = RenderForm(context);
        var validation = form.FindComponent<ShopCommandValidation>().Instance;

        await form.InvokeAsync(() => validation.ShowErrors(
        [
            new(nameof(FormModel.Email), "Email_Required"),
            new(nameof(FormModel.FirstName), "FirstName_Required"),
            new(nameof(FormModel.LastName), "LastName_Required")
        ]));

        context.GetValidationMessages(context.Field(nameof(FormModel.Email))).Should().Equal("Localized Email_Required");
        context.GetValidationMessages(context.Field(nameof(FormModel.FirstName))).Should().Equal("Localized FirstName_Required");
        context.GetValidationMessages(context.Field(nameof(FormModel.LastName))).Should().Equal("Localized LastName_Required");
        form.Find(".shop-command-validation").TextContent.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ShowErrors_RenamedProperty_UsesExplicitMapping()
    {
        var model = new FormModel();
        var context = new EditContext(model);
        var form = RenderForm(context, new Dictionary<string, string>
        {
            [nameof(FormModel.Email)] = nameof(FormModel.EmailAddress)
        });

        await form.InvokeAsync(() => form.FindComponent<ShopCommandValidation>().Instance.ShowErrors(
            [new(nameof(FormModel.Email), "Email_Invalid"), new(nameof(FormModel.FirstName), "FirstName_Required")]));

        context.GetValidationMessages(context.Field(nameof(FormModel.EmailAddress))).Should().Equal("Localized Email_Invalid");
        context.GetValidationMessages(context.Field(nameof(FormModel.Email))).Should().BeEmpty();
        context.GetValidationMessages(context.Field(nameof(FormModel.FirstName))).Should().Equal("Localized FirstName_Required");
    }

    [Theory]
    [InlineData("Address.City")]
    [InlineData("Items[0].Price")]
    [InlineData("Addresses[0].City")]
    [InlineData("Tags[0]")]
    public async Task ShowErrors_NestedOrIndexedPath_MatchesInputFieldIdentifier(string path)
    {
        var model = new FormModel();
        var context = new EditContext(model);
        var form = RenderForm(context);
        var fields = new Dictionary<string, FieldIdentifier>
        {
            ["Address.City"] = FieldIdentifier.Create(() => model.Address!.City),
            ["Items[0].Price"] = FieldIdentifier.Create(() => model.Items[0].Price),
            ["Addresses[0].City"] = FieldIdentifier.Create(() => model.Addresses[0].City),
            ["Tags[0]"] = FieldIdentifier.Create(() => model.Tags[0])
        };

        await form.InvokeAsync(() => form.FindComponent<ShopCommandValidation>().Instance.ShowErrors([new(path, "Required")]));

        context.GetValidationMessages(fields[path]).Should().Equal("Localized Required");
        form.Find(".shop-command-validation").TextContent.Should().BeNullOrWhiteSpace();
        await form.InvokeAsync(() => context.NotifyFieldChanged(fields[path]));
        context.GetValidationMessages().Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("MissingProperty")]
    [InlineData("Address.City")]
    [InlineData("Items[5].Price")]
    [InlineData("Items[-1].Price")]
    [InlineData("Items[bad].Price")]
    [InlineData("Items[0")]
    [InlineData("Items[0]Price")]
    [InlineData("Items[0].")]
    [InlineData("Items[0].MissingProperty")]
    [InlineData("Email.Length")]
    public async Task ShowErrors_UnresolvableOrModelLevelPath_DisplaysFormSummary(string path)
    {
        var model = new FormModel { Address = null };
        var context = new EditContext(model);
        var form = RenderForm(context);

        await form.InvokeAsync(() => form.FindComponent<ShopCommandValidation>().Instance.ShowErrors([new(path, "Required")]));

        context.GetValidationMessages(context.Field(string.Empty)).Should().Equal("Localized Required");
        form.Find(".shop-command-validation").TextContent.Should().Contain("Localized Required");
        form.Find(".shop-command-validation").GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public async Task ShowErrors_SubsequentResponse_ReplacesOnlyOwnedMessages()
    {
        var context = new EditContext(new FormModel());
        var form = RenderForm(context);
        var validation = form.FindComponent<ShopCommandValidation>().Instance;
        var foreign = new ValidationMessageStore(context);
        foreign.Add(context.Field(nameof(FormModel.Email)), "Parsing error");

        await form.InvokeAsync(() => validation.ShowErrors([new(nameof(FormModel.Email), "Required")]));
        await form.InvokeAsync(() => validation.ShowErrors([new(nameof(FormModel.FirstName), "Required")]));

        context.GetValidationMessages(context.Field(nameof(FormModel.Email))).Should().Equal("Parsing error");
        context.GetValidationMessages(context.Field(nameof(FormModel.FirstName))).Should().Equal("Localized Required");
        await form.InvokeAsync(validation.Clear);
        context.GetValidationMessages().Should().Equal("Parsing error");
    }

    [Fact]
    public async Task FieldChanged_ClearsOnlyChangedFieldAndKeepsOtherErrors()
    {
        var context = new EditContext(new FormModel());
        var form = RenderForm(context);
        var validation = form.FindComponent<ShopCommandValidation>().Instance;
        await form.InvokeAsync(() => validation.ShowErrors(
        [
            new(nameof(FormModel.Email), "Email_Required"),
            new(nameof(FormModel.FirstName), "FirstName_Required"),
            new("", "FormError")
        ]));

        await form.InvokeAsync(() => context.NotifyFieldChanged(context.Field(nameof(FormModel.Email))));

        context.GetValidationMessages(context.Field(nameof(FormModel.Email))).Should().BeEmpty();
        context.GetValidationMessages(context.Field(nameof(FormModel.FirstName))).Should().Equal("Localized FirstName_Required");
        form.Find(".shop-command-validation").TextContent.Should().Contain("Localized FormError");
    }

    [Fact]
    public async Task ContextChanged_RemovesOldMessagesAndSubscription()
    {
        var oldContext = new EditContext(new FormModel());
        var newContext = new EditContext(new FormModel());
        var form = RenderForm(oldContext);
        var validation = form.FindComponent<ShopCommandValidation>().Instance;
        await form.InvokeAsync(() => validation.ShowErrors([new(nameof(FormModel.Email), "Required")]));

        form.Render(parameters => parameters
            .Add(component => component.Value, newContext)
            .Add(component => component.ChildContent, ValidationContent()));
        oldContext.GetValidationMessages().Should().BeEmpty();
        await form.InvokeAsync(() => validation.ShowErrors([new(nameof(FormModel.Email), "Required")]));
        var oldNotifications = 0;
        oldContext.OnValidationStateChanged += (_, _) => oldNotifications++;
        await form.InvokeAsync(() => oldContext.NotifyFieldChanged(oldContext.Field(nameof(FormModel.Email))));

        oldNotifications.Should().Be(0);
        newContext.GetValidationMessages(newContext.Field(nameof(FormModel.Email))).Should().Equal("Localized Required");
    }

    [Fact]
    public async Task Dispose_RemovesOwnedMessagesAndSubscription()
    {
        var context = new EditContext(new FormModel());
        var form = RenderForm(context);
        await form.InvokeAsync(() => form.FindComponent<ShopCommandValidation>().Instance.ShowErrors([new(nameof(FormModel.Email), "Required")]));

        await DisposeComponentsAsync();

        context.GetValidationMessages().Should().BeEmpty();
        var notifications = 0;
        context.OnValidationStateChanged += (_, _) => notifications++;
        context.NotifyFieldChanged(context.Field(nameof(FormModel.Email)));
        notifications.Should().Be(0);
    }

    [Fact]
    public void Render_WithoutEditContext_RejectsMisconfiguredComponent()
    {
        Action render = () => Render<ShopCommandValidation>();

        render.Should().Throw<InvalidOperationException>().WithMessage("*EditForm*");
    }

    private IRenderedComponent<CascadingValue<EditContext>> RenderForm(EditContext context,
        IReadOnlyDictionary<string, string>? fieldMap = null) =>
        Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .Add(component => component.ChildContent, ValidationContent(fieldMap)));

    private static RenderFragment ValidationContent(IReadOnlyDictionary<string, string>? fieldMap = null) => builder =>
    {
        builder.OpenComponent<ShopCommandValidation>(0);
        builder.AddAttribute(1, nameof(ShopCommandValidation.FieldMap), fieldMap);
        builder.CloseComponent();
    };

    private sealed class FormModel
    {
        public string Email { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public AddressModel? Address { get; set; } = new();
        public List<ItemModel> Items { get; set; } = [new()];
        public AddressModel[] Addresses { get; set; } = [new()];
        public List<string> Tags { get; set; } = [string.Empty];
    }

    private sealed class AddressModel
    {
        public string City { get; set; } = string.Empty;
    }

    private sealed class ItemModel
    {
        public decimal Price { get; set; }
    }
}
