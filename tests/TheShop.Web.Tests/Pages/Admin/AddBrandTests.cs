using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.Commands.CreateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="AddBrand"/> page (Figma node <c>2465:1128</c>) — the add-brand form:
/// permission gating (Behavior 4, AC-8), the Active status default (RULE-5), the
/// optional-fields-omitted happy path (AC-6), the logo/description/inactive-status happy paths
/// (AC-1, AC-4, AC-7), duplicate/validation/technical-failure error surfacing that preserves
/// entered input (Behavior 3, AC-2/AC-3), and success navigation to
/// <see cref="Routes.Admin.ManageBrands"/> (AC-1, FR-6).
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class AddBrandTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public AddBrandTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));

        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
    }

    private void AuthorizeAsBrandCreator()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Brands.Create.Code));
    }

    private static BrandDto ExampleDto(
        string name = "Elf Bar", string? description = null, string? logoUrl = null, bool isActive = true) =>
        new(Guid.NewGuid(), name, "elf-bar", description, logoUrl, isActive);

    // =========================================================================
    // Permission gating (Behavior 4, AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_WhenUserHoldsBrandsCreatePermission_ShowsTheForm()
    {
        AuthorizeAsBrandCreator();

        var cut = Render<AddBrand>();

        cut.Markup.Should().Contain(Strings.AddBrand_Heading);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_WhenUserLacksBrandsCreatePermission_ShowsAccessDeniedInsteadOfTheForm()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("support-user"); // authenticated, but no brands.create policy

        var cut = Render<AddBrand>();

        cut.Markup.Should().NotContain(Strings.AddBrand_Heading,
            "an absent permission must hide the capability entirely, not merely disable it");
        cut.Markup.Should().Contain(Strings.AccessDenied_Title);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_WhenUserIsNotAuthenticated_ShowsAccessDeniedInsteadOfTheForm()
    {
        var authContext = this.AddAuthorization();
        authContext.SetNotAuthorized();

        var cut = Render<AddBrand>();

        cut.Markup.Should().NotContain(Strings.AddBrand_Heading);
        cut.Markup.Should().Contain(Strings.AccessDenied_Title);
    }

    // =========================================================================
    // Status default — Active (RULE-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_Always_DefaultsStatusToActive()
    {
        AuthorizeAsBrandCreator();

        var cut = Render<AddBrand>();

        GetPrivateField<bool>(cut.Instance, "_isActive").Should().BeTrue();
    }

    // =========================================================================
    // Save — happy path, name only (AC-1, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WithOnlyAName_SendsCreateBrandCommandWithNoDescriptionOrLogoAndActiveStatus()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto()));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();

        SetFormState(cut, name: "Elf Bar", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<CreateBrandCommand>(c =>
                c.Name == "Elf Bar" && c.Description == null && c.Logo == null && c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageBrands()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto()));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Elf Bar", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Strings.Brand_Created, Severity.Success);
        navManager.Uri.Should().EndWith(Routes.Admin.ManageBrands);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WithDescriptionAndInactiveStatus_SendsCreateBrandCommandWithBoth()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto(description: "A vape brand.", isActive: false)));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();

        SetFormState(cut, name: "Elf Bar", description: "A vape brand.", isActive: false, isFormValid: true);

        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<CreateBrandCommand>(c => c.Description == "A vape brand." && !c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WithASelectedLogo_SendsCreateBrandCommandCarryingTheLogo()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto(logoUrl: "https://cdn.example/logo.webp")));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();

        SetFormState(cut, name: "Elf Bar", description: null, isActive: true, isFormValid: true);
        SetLogoImages(cut, [new ShopUploadedImage([1, 2, 3], "logo.png", "image/png", "data:image/png;base64,AQID")]);

        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<CreateBrandCommand>(c =>
                c.Logo != null && c.Logo.FileName == "logo.png" && c.Logo.ContentType == "image/png"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Accessibility — remove-logo control is announced via a localized aria-label (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_WithASelectedLogo_RemoveButtonHasALocalizedAriaLabel()
    {
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();

        SetLogoImages(cut, [new ShopUploadedImage([1, 2, 3], "logo.png", "image/png", "data:image/png;base64,AQID")]);

        cut.Find($"[aria-label='{Strings.AddBrand_LogoRemove}']").Should().NotBeNull();
    }

    // =========================================================================
    // Save — invalid form refused, command never sent (Behavior 3, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand()
    {
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();

        SetFormState(cut, name: "", description: null, isActive: true, isFormValid: false);

        await ClickSaveAsync(cut);

        await _mediator.DidNotReceive().Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Save — duplicate name refused, input preserved (Behavior 3, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WhenBrandAlreadyExists_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_AlreadyExists"));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Elf Bar", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageBrands);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WhenBrandAlreadyExists_PreservesTheEnteredName()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_AlreadyExists"));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();

        SetFormState(cut, name: "Elf Bar", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        GetPrivateField<string>(cut.Instance, "_name").Should().Be("Elf Bar",
            "everything the staff member already entered must be preserved so they can correct and resubmit (FR-8)");
    }

    // =========================================================================
    // Save — technical failure, input preserved (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task SaveAsync_WhenATechnicalFailureIsReturned_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        _mediator.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_CreateFailed"));
        AuthorizeAsBrandCreator();
        var cut = Render<AddBrand>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Elf Bar", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageBrands);
    }

    // =========================================================================
    // Cancel button — targets ManageBrands
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_Always_CancelButtonLinksToManageBrands()
    {
        AuthorizeAsBrandCreator();

        var cut = Render<AddBrand>();

        cut.Find($"a[href='{Routes.Admin.ManageBrands}']").Should().NotBeNull();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void SetFormState(
        IRenderedComponent<AddBrand> cut, string name, string? description, bool isActive, bool isFormValid)
    {
        var type = typeof(AddBrand);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        type.GetField("_name", flags)!.SetValue(cut.Instance, name);
        type.GetField("_description", flags)!.SetValue(cut.Instance, description);
        type.GetField("_isActive", flags)!.SetValue(cut.Instance, isActive);
        type.GetField("_isFormValid", flags)!.SetValue(cut.Instance, isFormValid);
        cut.Render();
    }

    private static void SetLogoImages(IRenderedComponent<AddBrand> cut, IReadOnlyList<ShopUploadedImage> images)
    {
        typeof(AddBrand).GetField("_logoImages", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, images);
        cut.Render();
    }

    private static T GetPrivateField<T>(object instance, string fieldName) =>
        (T)instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static async Task ClickSaveAsync(IRenderedComponent<AddBrand> cut)
    {
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains(Strings.AddBrand_SaveButton));
        await saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: SaveAsync_WithOnlyAName_SendsCreateBrandCommandWithNoDescriptionOrLogoAndActiveStatus,
//        SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageBrands
// AC-2: SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand
// AC-3: SaveAsync_WhenBrandAlreadyExists_ShowsErrorSnackbarAndDoesNotNavigate,
//        SaveAsync_WhenBrandAlreadyExists_PreservesTheEnteredName
// AC-4: SaveAsync_WithASelectedLogo_SendsCreateBrandCommandCarryingTheLogo
// AC-6: SaveAsync_WithOnlyAName_SendsCreateBrandCommandWithNoDescriptionOrLogoAndActiveStatus
// AC-7: SaveAsync_WithDescriptionAndInactiveStatus_SendsCreateBrandCommandWithBoth,
//        Render_Always_DefaultsStatusToActive
// AC-8: Render_WhenUserHoldsBrandsCreatePermission_ShowsTheForm,
//        Render_WhenUserLacksBrandsCreatePermission_ShowsAccessDeniedInsteadOfTheForm,
//        Render_WhenUserIsNotAuthenticated_ShowsAccessDeniedInsteadOfTheForm
// AC-10: Render_WithASelectedLogo_RemoveButtonHasALocalizedAriaLabel
//        (full keyboard/focus/screen-reader coverage requires a real browser and is out of
//        bUnit's reach; this asserts the one DOM-level accessibility contract bUnit can verify —
//        see the summary's coverage note)
