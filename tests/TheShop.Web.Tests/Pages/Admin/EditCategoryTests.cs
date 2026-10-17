using TheShop.Web.Common.Notifications;
using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public EditCategoryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);

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

        SetFormState(cut, name: "Pod Systems", description: "New description.", isActive: false);
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive);
        await ClickSaveAsync(cut);

        _notifications.Received(1).Show(Strings.EditCategory_Success, ShopNotificationKind.Success);
        navManager.Uri.Should().EndWith(Routes.Admin.ManageCategories);
    }

    // =========================================================================
    // Save — Application validation failures appear on their fields (AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task SaveAsync_WhenCommandValidationFails_ShowsFieldError()
    {
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CategoryDto>(nameof(Strings.Category_NameRequired), [],
                [new FieldValidationError("Name", nameof(Strings.Category_NameRequired))]));
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "", description: dto.Description, isActive: dto.IsActive);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>());
        cut.Find(".shop-field-error").TextContent.Should().Contain(nameof(Strings.Category_NameRequired));
        cut.Find("[data-testid='edit-category-name']").Input("Corrected name");
        cut.Find(".shop-field-error").TextContent.Should().BeEmpty();
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

        SetFormState(cut, name: "Pod Systems", description: dto.Description, isActive: dto.IsActive);
        await ClickSaveAsync(cut);

        _notifications.Received(1).Show(Arg.Any<string>(), ShopNotificationKind.Error);
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

        SetFormState(cut, name: "Pod Systems", description: dto.Description, isActive: dto.IsActive);
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive);
        await ClickSaveAsync(cut);

        _notifications.Received(1).Show(Strings.EditCategory_Success, ShopNotificationKind.Success);
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive);
        await cut.InvokeAsync(() => cut.FindComponent<ShopImageUpload>().Instance.FilesChanged.InvokeAsync([]));
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive);
        await cut.InvokeAsync(() => cut.FindComponent<ShopImageUpload>().Instance.FilesChanged.InvokeAsync([]));
        await cut.InvokeAsync(() => cut.FindComponent<ShopImageUpload>().Instance.FilesChanged.InvokeAsync(
            [new ShopUploadedImage([1, 2, 3], "image.png", "image/png", "data:image/png;base64,AQID")]));
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive);
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: false);
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: true);
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

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive);
        await ClickSaveAsync(cut);

        _notifications.Received(1).Show(Arg.Any<string>(), ShopNotificationKind.Error);
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

    [Fact]
    public async Task SaveAsync_WhilePending_DisablesControlsAndIgnoresDuplicateSubmission()
    {
        var completion = new TaskCompletionSource<Result<CategoryDto>>();
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);
        SetFormState(cut, "  Disposables  ", "  Description  ", true);
        var first = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => cut.Find("button[type=submit]").HasAttribute("disabled").Should().BeTrue());
        cut.FindAll(".shop-field input").Should().OnlyContain(input => input.HasAttribute("disabled"));
        cut.Find(".shop-categories-form-actions a").HasAttribute("href").Should().BeFalse();
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<UpdateCategoryCommand>(c => c.Name == "Disposables" && c.Description == "Description"), Arg.Any<CancellationToken>());
        completion.SetResult(Result.Fail<CategoryDto>(nameof(Strings.Category_CreateFailed)));
        await first;
        cut.WaitForAssertion(() => cut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse());
        cut.Find(".shop-categories-form-actions a").GetAttribute("href").Should().Be(Routes.Admin.ManageCategories);
    }

    [Fact]
    public async Task Render_SingleImageSelection_HasNoMultipleAttribute()
    {
        var dto = ExampleDto();
        SetUpExistingCategory(dto);
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);
        cut.Find("input[type=file]").HasAttribute("multiple").Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_RejectedReplacementRemoved_PreservesStoredImage()
    {
        var dto = ExampleDto(imageUrl: "https://cdn.example/category.webp");
        SetUpExistingCategory(dto);
        _mediator.Send(Arg.Any<UpdateCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsCategoryEditor();
        var cut = await RenderAsync(dto.Id);
        var upload = cut.FindComponent<ShopImageUpload>();
        upload.Instance.Files.Should().ContainSingle(image => image.IsExisting && image.PreviewUrl == dto.ImageUrl);
        await cut.InvokeAsync(() => upload.Instance.FilesChanged.InvokeAsync(
            [new ShopUploadedImage([], "rejected.gif", "image/gif", "") { Error = Strings.Category_ImageInvalidType }]));
        await cut.InvokeAsync(() => upload.Instance.FilesChanged.InvokeAsync([]));
        upload.Instance.Files.Should().ContainSingle(image => image.IsExisting && image.PreviewUrl == dto.ImageUrl);
        await ClickSaveAsync(cut);
        await _mediator.Received(1).Send(Arg.Is<UpdateCategoryCommand>(c => !c.RemoveImage && c.NewImage == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void SetFormState(
        IRenderedComponent<EditCategory> cut, string name, string? description, bool isActive)
    {
        cut.FindAll(".shop-field input")[0].Input(name);
        cut.FindAll(".shop-field input")[1].Input(description ?? string.Empty);
        cut.FindAll(".shop-categories-status button")[isActive ? 0 : 1].Click();
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var model = instance.GetType().GetField("_model", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
        var property = fieldName switch { "_name" => "Name", "_description" => "Description", _ => "IsActive" };
        return (T)model.GetType().GetProperty(property)!.GetValue(model)!;
    }

    private static async Task ClickSaveAsync(IRenderedComponent<EditCategory> cut)
    {
        await cut.Find("form").SubmitAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: SaveAsync_WithChangesToNameDescriptionAndStatus_SendsUpdateCategoryCommandWithTheNewValues,
//        SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageCategories
// AC-9: SaveAsync_WhenCommandValidationFails_ShowsFieldError
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
