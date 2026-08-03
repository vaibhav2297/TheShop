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
using TheShop.Application.Features.Categories.Commands.UpdateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetCategoryById;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="EditCategory"/> page — permission gating that denies a view-only
/// admin's direct link (plan §5 Decision 9, AC-20), the pre-filled form and its happy-path save
/// across all four editable fields (AC-8), validation and duplicate-name failures that preserve
/// entered input (AC-9, AC-10, AC-11), the image remove/replace flows (AC-13), the status toggle
/// (AC-16), and the "category no longer exists" message for a link to a deleted category (AC-23).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class EditCategoryTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public EditCategoryTests()
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

    private void AuthorizeAsCategoryEditor()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Categories.Edit.Code));
    }

    private static CategoryDto ExampleDto(
        Guid? id = null,
        string name = "Disposables",
        string? description = "Single-use vape devices.",
        string? imageUrl = null,
        bool isActive = true) =>
        new(id ?? Guid.NewGuid(), name, description, imageUrl, isActive);

    private void SetUpExistingCategory(CategoryDto dto) =>
        _mediator.Send(Arg.Is<GetCategoryByIdQuery>(q => q.Id == dto.Id), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(dto));

    private async Task<IRenderedComponent<EditCategory>> RenderAsync(Guid id)
    {
        var cut = Render<EditCategory>(p => p.Add(c => c.Id, id));
        await cut.InvokeAsync(() => { });
        return cut;
    }

    // =========================================================================
    // Permission gating — the page-level categories.edit gate, not the query, denies a direct
    // link (plan §5 Decision 9, AC-20)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenUserHoldsCategoriesEditPermission_ShowsTheForm()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();

        var cut = await RenderAsync(dto.Id);

        cut.Markup.Should().Contain(Strings.EditCategory_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void EditCategory_Always_CarriesTheCategoriesEditAuthorizePolicy()
    {
        var attributes = typeof(EditCategory).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Categories.Edit.Code),
            "the route-level policy, not the query, is what denies a view-only admin's direct " +
            "link — App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // Pre-filled form (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenCategoryExists_RequestsItByTheSuppliedId()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();

        await RenderAsync(dto.Id);

        await _mediator.Received(1).Send(Arg.Is<GetCategoryByIdQuery>(q => q.Id == dto.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenCategoryExists_PreFillsNameDescriptionAndStatus()
    {
        var dto = ExampleDto(name: "Pod Systems", description: "Another category.", isActive: false);
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();

        var cut = await RenderAsync(dto.Id);

        GetPrivateField<string>(cut.Instance, "_name").Should().Be("Pod Systems");
        GetPrivateField<string?>(cut.Instance, "_description").Should().Be("Another category.");
        GetPrivateField<bool>(cut.Instance, "_isActive").Should().BeFalse();
    }

    // =========================================================================
    // Category no longer exists (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenTheCategoryNoLongerExists_ShowsTheNotFoundMessage()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetCategoryByIdQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_NotFound"));
        AuthorizeAsCategoryEditor();

        var cut = await RenderAsync(id);

        cut.Markup.Should().Contain(Strings.Category_NotFound);
        cut.Markup.Should().NotContain(Strings.EditCategory_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenTheCategoryNoLongerExists_OffersALinkBackToTheList()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetCategoryByIdQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_NotFound"));
        AuthorizeAsCategoryEditor();

        var cut = await RenderAsync(id);

        cut.Find($"a[href='{Routes.Admin.ManageCategories}']").Should().NotBeNull();
    }

    // =========================================================================
    // Save — happy path across all four editable fields (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WithChangesToNameDescriptionAndStatus_SendsUpdateCategoryCommandWithTheNewValues()
    {
        var dto = ExampleDto(name: "Disposables", description: "Old description.", isActive: true);
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(dto with { Name = "Pod Systems" }));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "Pod Systems", description: "New description.", isActive: false, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateCategoryCommand>(c =>
                c.Id == dto.Id && c.Name == "Pod Systems" && c.Description == "New description." && !c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageCategories()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Strings.EditCategory_Success, Severity.Success);
        navManager.Uri.Should().EndWith(Routes.Admin.ManageCategories);
    }

    // =========================================================================
    // Save — invalid form refused, command never sent (AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "", description: dto.Description, isActive: dto.IsActive, isFormValid: false);
        await ClickSaveAsync(cut);

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Save — duplicate name refused, input preserved (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenRenamedToAnotherCategorysName_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_AlreadyExists"));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Pod Systems", description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageCategories);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenRenamedToAnotherCategorysName_PreservesTheEnteredName()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_AlreadyExists"));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "Pod Systems", description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        GetPrivateField<string>(cut.Instance, "_name").Should().Be("Pod Systems",
            "everything the staff member already entered must be preserved so they can correct and resubmit");
    }

    // =========================================================================
    // Save — no changes at all still succeeds (AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_SavedWithNoChangesAtAll_Succeeds()
    {
        var dto = ExampleDto(name: "Disposables", description: "Single-use vape devices.", isActive: true);
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Strings.EditCategory_Success, Severity.Success);
    }

    // =========================================================================
    // Image — remove existing (AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenTheExistingImageWasRemovedWithNoReplacementChosen_SendsRemoveImageTrue()
    {
        var dto = ExampleDto(imageUrl: "https://cdn.example/image.webp");
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        InvokePrivateMethod(cut, "RemoveExistingImage");
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateCategoryCommand>(c => c.RemoveImage && c.NewImage == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Image — replace with a new upload (AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenANewImageIsSelected_SendsItAsTheReplacement()
    {
        var dto = ExampleDto(imageUrl: "https://cdn.example/image.webp");
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        InvokePrivateMethod(cut, "RemoveExistingImage");
        SetNewCategoryImages(cut, [new ShopUploadedImage([1, 2, 3], "image.png", "image/png", "data:image/png;base64,AQID")]);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateCategoryCommand>(c =>
                !c.RemoveImage && c.NewImage != null && c.NewImage.FileName == "image.png" && c.NewImage.ContentType == "image/png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenTheExistingImageIsUntouched_SendsNeitherRemoveNorANewImage()
    {
        var dto = ExampleDto(imageUrl: "https://cdn.example/image.webp");
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateCategoryCommand>(c => !c.RemoveImage && c.NewImage == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Status toggle (AC-16)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenStatusIsSetToInactive_SendsIsActiveFalse()
    {
        var dto = ExampleDto(isActive: true);
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: false, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(Arg.Is<UpdateCategoryCommand>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenStatusIsSetToActive_SendsIsActiveTrue()
    {
        var dto = ExampleDto(isActive: false);
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: true, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(Arg.Is<UpdateCategoryCommand>(c => c.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Save — technical failure, input preserved (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenATechnicalFailureIsReturned_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<CategoryDto>("Category_UpdateFailed"));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageCategories);
    }

    // =========================================================================
    // Cancel button — targets ManageCategories
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_Always_CancelButtonLinksToManageCategories()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();

        var cut = await RenderAsync(dto.Id);

        cut.Find($"a[href='{Routes.Admin.ManageCategories}']").Should().NotBeNull();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void SetFormState(
        IRenderedComponent<EditCategory> cut, string name, string? description, bool isActive, bool isFormValid)
    {
        var type = typeof(EditCategory);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        type.GetField("_name", flags)!.SetValue(cut.Instance, name);
        type.GetField("_description", flags)!.SetValue(cut.Instance, description);
        type.GetField("_isActive", flags)!.SetValue(cut.Instance, isActive);
        type.GetField("_isFormValid", flags)!.SetValue(cut.Instance, isFormValid);
        cut.Render();
    }

    private static void SetNewCategoryImages(IRenderedComponent<EditCategory> cut, IReadOnlyList<ShopUploadedImage> images)
    {
        typeof(EditCategory).GetField("_newCategoryImages", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, images);
        cut.Render();
    }

    private static void InvokePrivateMethod(IRenderedComponent<EditCategory> cut, string methodName)
    {
        typeof(EditCategory).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(cut.Instance, null);
        cut.Render();
    }

    private static T GetPrivateField<T>(object instance, string fieldName) =>
        (T)instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static async Task ClickSaveAsync(IRenderedComponent<EditCategory> cut)
    {
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains(Strings.Save));
        await cut.InvokeAsync(() => saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: SaveAsync_WithChangesToNameDescriptionAndStatus_SendsUpdateCategoryCommandWithTheNewValues,
//        SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageCategories
// AC-9: SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand
// AC-10: SaveAsync_WhenRenamedToAnotherCategorysName_ShowsErrorSnackbarAndDoesNotNavigate,
//         SaveAsync_WhenRenamedToAnotherCategorysName_PreservesTheEnteredName
// AC-11: SaveAsync_SavedWithNoChangesAtAll_Succeeds
// AC-13: SaveAsync_WhenTheExistingImageWasRemovedWithNoReplacementChosen_SendsRemoveImageTrue,
//         SaveAsync_WhenANewImageIsSelected_SendsItAsTheReplacement,
//         SaveAsync_WhenTheExistingImageIsUntouched_SendsNeitherRemoveNorANewImage
// AC-16: SaveAsync_WhenStatusIsSetToInactive_SendsIsActiveFalse, SaveAsync_WhenStatusIsSetToActive_SendsIsActiveTrue
// AC-20: Render_WhenUserHoldsCategoriesEditPermission_ShowsTheForm, EditCategory_Always_CarriesTheCategoriesEditAuthorizePolicy
// AC-23: Render_WhenCategoryExists_RequestsItByTheSuppliedId, Render_WhenCategoryExists_PreFillsNameDescriptionAndStatus,
//         Render_WhenTheCategoryNoLongerExists_ShowsTheNotFoundMessage, Render_WhenTheCategoryNoLongerExists_OffersALinkBackToTheList
