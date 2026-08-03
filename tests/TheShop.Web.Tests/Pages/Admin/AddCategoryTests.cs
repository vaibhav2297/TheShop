using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.Commands.CreateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="AddCategory"/> page (Figma node <c>2629:1874</c>) — the add-category
/// form: permission gating, the Active status default (plan §5 Decision 11, RULE-5), the
/// optional-fields-omitted happy path (AC-7), the image/description/inactive-status happy paths
/// (AC-6, AC-7), duplicate/validation/technical-failure error surfacing that preserves entered
/// input, and success navigation to <see cref="Routes.Admin.ManageCategories"/>.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class AddCategoryTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public AddCategoryTests()
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

    private void AuthorizeAsCategoryCreator()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Categories.Create.Code));
    }

    private static CategoryDto ExampleDto(
        string name = "Disposables", string? description = null, string? imageUrl = null, bool isActive = true) =>
        new(Guid.NewGuid(), name, description, imageUrl, isActive);

    // =========================================================================
    // Permission gating
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Render_WhenUserHoldsCategoriesCreatePermission_ShowsTheForm()
    {
        AuthorizeAsCategoryCreator();

        var cut = Render<AddCategory>();

        cut.Markup.Should().Contain(Strings.AddCategory_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void AddCategory_Always_CarriesTheCategoriesCreateAuthorizePolicy()
    {
        var attributes = typeof(AddCategory).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Categories.Create.Code),
            "the route-level policy is the only gate between a non-creator and the form — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // Status default — Active (plan §5 Decision 11, RULE-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Render_Always_DefaultsStatusToActive()
    {
        AuthorizeAsCategoryCreator();

        var cut = Render<AddCategory>();

        GetPrivateField<bool>(cut.Instance, "_isActive").Should().BeTrue();
    }

    // =========================================================================
    // Save — happy path, name only (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WithOnlyAName_SendsCreateCategoryCommandWithNoDescriptionOrImageAndActiveStatus()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto()));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();

        SetFormState(cut, name: "Disposables", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<CreateCategoryCommand>(c =>
                c.Name == "Disposables" && c.Description == null && c.Image == null && c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageCategories()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto()));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Disposables", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Strings.Category_Created, Severity.Success);
        navManager.Uri.Should().EndWith(Routes.Admin.ManageCategories);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WithDescriptionAndInactiveStatus_SendsCreateCategoryCommandWithBoth()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto(description: "Single-use vape devices.", isActive: false)));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();

        SetFormState(cut, name: "Disposables", description: "Single-use vape devices.", isActive: false, isFormValid: true);

        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<CreateCategoryCommand>(c => c.Description == "Single-use vape devices." && !c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WithASelectedImage_SendsCreateCategoryCommandCarryingTheImage()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(ExampleDto(imageUrl: "https://cdn.example/image.webp")));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();

        SetFormState(cut, name: "Disposables", description: null, isActive: true, isFormValid: true);
        SetCategoryImages(cut, [new ShopUploadedImage([1, 2, 3], "image.png", "image/png", "data:image/png;base64,AQID")]);

        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<CreateCategoryCommand>(c =>
                c.Image != null && c.Image.FileName == "image.png" && c.Image.ContentType == "image/png"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Accessibility — remove-image control is announced via a localized aria-label
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Render_WithASelectedImage_RemoveButtonHasALocalizedAriaLabel()
    {
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();

        SetCategoryImages(cut, [new ShopUploadedImage([1, 2, 3], "image.png", "image/png", "data:image/png;base64,AQID")]);

        cut.Find($"[aria-label='{Strings.AddCategory_ImageRemove}']").Should().NotBeNull();
    }

    // =========================================================================
    // Save — invalid form refused, command never sent (AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand()
    {
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();

        SetFormState(cut, name: "", description: null, isActive: true, isFormValid: false);

        await ClickSaveAsync(cut);

        await _mediator.DidNotReceive().Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Save — duplicate name refused, input preserved (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenCategoryAlreadyExists_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_AlreadyExists"));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Disposables", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageCategories);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenCategoryAlreadyExists_PreservesTheEnteredName()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_AlreadyExists"));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();

        SetFormState(cut, name: "Disposables", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        GetPrivateField<string>(cut.Instance, "_name").Should().Be("Disposables",
            "everything the staff member already entered must be preserved so they can correct and resubmit");
    }

    // =========================================================================
    // Save — technical failure, input preserved (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenATechnicalFailureIsReturned_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        _mediator.Send(Arg.Any<CreateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_CreateFailed"));
        AuthorizeAsCategoryCreator();
        var cut = Render<AddCategory>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Disposables", description: null, isActive: true, isFormValid: true);

        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageCategories);
    }

    // =========================================================================
    // Cancel button — targets ManageCategories
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Render_Always_CancelButtonLinksToManageCategories()
    {
        AuthorizeAsCategoryCreator();

        var cut = Render<AddCategory>();

        cut.Find($"a[href='{Routes.Admin.ManageCategories}']").Should().NotBeNull();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void SetFormState(
        IRenderedComponent<AddCategory> cut, string name, string? description, bool isActive, bool isFormValid)
    {
        var type = typeof(AddCategory);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        type.GetField("_name", flags)!.SetValue(cut.Instance, name);
        type.GetField("_description", flags)!.SetValue(cut.Instance, description);
        type.GetField("_isActive", flags)!.SetValue(cut.Instance, isActive);
        type.GetField("_isFormValid", flags)!.SetValue(cut.Instance, isFormValid);
        cut.Render();
    }

    private static void SetCategoryImages(IRenderedComponent<AddCategory> cut, IReadOnlyList<ShopUploadedImage> images)
    {
        typeof(AddCategory).GetField("_categoryImages", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, images);
        cut.Render();
    }

    private static T GetPrivateField<T>(object instance, string fieldName) =>
        (T)instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static async Task ClickSaveAsync(IRenderedComponent<AddCategory> cut)
    {
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains(Strings.AddCategory_SaveButton));
        await saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: SaveAsync_WithOnlyAName_SendsCreateCategoryCommandWithNoDescriptionOrImageAndActiveStatus,
//        SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageCategories,
//        SaveAsync_WithASelectedImage_SendsCreateCategoryCommandCarryingTheImage
// AC-7: SaveAsync_WithDescriptionAndInactiveStatus_SendsCreateCategoryCommandWithBoth,
//        Render_Always_DefaultsStatusToActive
// AC-9: SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand
// AC-10: SaveAsync_WhenCategoryAlreadyExists_ShowsErrorSnackbarAndDoesNotNavigate,
//         SaveAsync_WhenCategoryAlreadyExists_PreservesTheEnteredName
// AC-20: Render_WhenUserHoldsCategoriesCreatePermission_ShowsTheForm,
//         AddCategory_Always_CarriesTheCategoriesCreateAuthorizePolicy
// AC-26: Render_WithASelectedImage_RemoveButtonHasALocalizedAriaLabel
//        (full keyboard/focus/screen-reader coverage requires a real browser and is out of
//        bUnit's reach; this asserts the one DOM-level accessibility contract bUnit can verify,
//        mirroring manage-brands' AC-10 coverage note)
