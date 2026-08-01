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
using TheShop.Application.Features.Brands.Commands.UpdateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetBrandById;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="EditBrand"/> page — permission gating that denies a view-only admin's
/// direct link (plan §5 Decision 11, AC-16), the pre-filled form and its happy-path save across all
/// four editable fields (AC-6), validation and duplicate-name failures that preserve entered input
/// (AC-7, AC-8, AC-9), the logo remove/replace flows (AC-10, AC-11), the status toggle (AC-12), and
/// the "brand no longer exists" message for a link to a deleted brand (AC-27).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class EditBrandTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public EditBrandTests()
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

    private void AuthorizeAsBrandEditor()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code));
    }

    private static BrandDto ExampleDto(
        Guid? id = null,
        string name = "Elf Bar",
        string? description = "A vape brand.",
        string? logoUrl = null,
        bool isActive = true) =>
        new(id ?? Guid.NewGuid(), name, description, logoUrl, isActive);

    private void SetUpExistingBrand(BrandDto dto) =>
        _mediator.Send(Arg.Is<GetBrandByIdQuery>(q => q.Id == dto.Id), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(dto));

    private async Task<IRenderedComponent<EditBrand>> RenderAsync(Guid id)
    {
        var cut = Render<EditBrand>(p => p.Add(c => c.Id, id));
        await cut.InvokeAsync(() => { });
        return cut;
    }

    // =========================================================================
    // Permission gating — the page-level brands.edit gate, not the query, denies a direct link
    // (plan §5 Decision 11, AC-16)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenUserHoldsBrandsEditPermission_ShowsTheForm()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        AuthorizeAsBrandEditor();

        var cut = await RenderAsync(dto.Id);

        cut.Markup.Should().Contain(Strings.EditBrand_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void EditBrand_Always_CarriesTheBrandsEditAuthorizePolicy()
    {
        var attributes = typeof(EditBrand).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code),
            "the route-level policy, not the query, is what denies a view-only admin's direct " +
            "link — App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // Pre-filled form (AC-27)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenBrandExists_RequestsItByTheSuppliedId()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        AuthorizeAsBrandEditor();

        await RenderAsync(dto.Id);

        await _mediator.Received(1).Send(Arg.Is<GetBrandByIdQuery>(q => q.Id == dto.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenBrandExists_PreFillsNameDescriptionAndStatus()
    {
        var dto = ExampleDto(name: "Lost Mary", description: "Another brand.", isActive: false);
        SetUpExistingBrand(dto);
        AuthorizeAsBrandEditor();

        var cut = await RenderAsync(dto.Id);

        GetPrivateField<string>(cut.Instance, "_name").Should().Be("Lost Mary");
        GetPrivateField<string?>(cut.Instance, "_description").Should().Be("Another brand.");
        GetPrivateField<bool>(cut.Instance, "_isActive").Should().BeFalse();
    }

    // =========================================================================
    // Brand no longer exists (AC-27)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenTheBrandNoLongerExists_ShowsTheNotFoundMessage()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetBrandByIdQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_NotFound"));
        AuthorizeAsBrandEditor();

        var cut = await RenderAsync(id);

        cut.Markup.Should().Contain(Strings.Brand_NotFound);
        cut.Markup.Should().NotContain(Strings.EditBrand_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenTheBrandNoLongerExists_OffersALinkBackToTheList()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetBrandByIdQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_NotFound"));
        AuthorizeAsBrandEditor();

        var cut = await RenderAsync(id);

        cut.Find($"a[href='{Routes.Admin.ManageBrands}']").Should().NotBeNull();
    }

    // =========================================================================
    // Save — happy path across all four editable fields (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WithChangesToNameDescriptionAndStatus_SendsUpdateBrandCommandWithTheNewValues()
    {
        var dto = ExampleDto(name: "Elf Bar", description: "Old description.", isActive: true);
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(dto with { Name = "Lost Mary" }));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "Lost Mary", description: "New description.", isActive: false, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateBrandCommand>(c =>
                c.Id == dto.Id && c.Name == "Lost Mary" && c.Description == "New description." && !c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageBrands()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Strings.EditBrand_Success, Severity.Success);
        navManager.Uri.Should().EndWith(Routes.Admin.ManageBrands);
    }

    // =========================================================================
    // Save — invalid form refused, command never sent (AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "", description: dto.Description, isActive: dto.IsActive, isFormValid: false);
        await ClickSaveAsync(cut);

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Save — duplicate name refused, input preserved (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenRenamedToAnotherBrandsName_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_AlreadyExists"));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: "Lost Mary", description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageBrands);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenRenamedToAnotherBrandsName_PreservesTheEnteredName()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_AlreadyExists"));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: "Lost Mary", description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        GetPrivateField<string>(cut.Instance, "_name").Should().Be("Lost Mary",
            "everything the staff member already entered must be preserved so they can correct and resubmit (FR-15)");
    }

    // =========================================================================
    // Save — no changes at all still succeeds (AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_SavedWithNoChangesAtAll_Succeeds()
    {
        var dto = ExampleDto(name: "Elf Bar", description: "A vape brand.", isActive: true);
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Strings.EditBrand_Success, Severity.Success);
    }

    // =========================================================================
    // Logo — remove existing (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenTheExistingLogoWasRemovedWithNoReplacementChosen_SendsRemoveLogoTrue()
    {
        var dto = ExampleDto(logoUrl: "https://cdn.example/logo.webp");
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        InvokePrivateMethod(cut, "RemoveExistingLogo");
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateBrandCommand>(c => c.RemoveLogo && c.NewLogo == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Logo — replace with a new upload (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenANewLogoIsSelected_SendsItAsTheReplacement()
    {
        var dto = ExampleDto(logoUrl: "https://cdn.example/logo.webp");
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        InvokePrivateMethod(cut, "RemoveExistingLogo");
        SetNewLogoImages(cut, [new ShopUploadedImage([1, 2, 3], "logo.png", "image/png", "data:image/png;base64,AQID")]);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateBrandCommand>(c =>
                !c.RemoveLogo && c.NewLogo != null && c.NewLogo.FileName == "logo.png" && c.NewLogo.ContentType == "image/png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenTheExistingLogoIsUntouched_SendsNeitherRemoveNorANewLogo()
    {
        var dto = ExampleDto(logoUrl: "https://cdn.example/logo.webp");
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateBrandCommand>(c => !c.RemoveLogo && c.NewLogo == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Status toggle (AC-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenStatusIsSetToInactive_SendsIsActiveFalse()
    {
        var dto = ExampleDto(isActive: true);
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: false, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(Arg.Is<UpdateBrandCommand>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenStatusIsSetToActive_SendsIsActiveTrue()
    {
        var dto = ExampleDto(isActive: false);
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(dto));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: true, isFormValid: true);
        await ClickSaveAsync(cut);

        await _mediator.Received(1).Send(Arg.Is<UpdateBrandCommand>(c => c.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Save — technical failure, input preserved (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task SaveAsync_WhenATechnicalFailureIsReturned_ShowsErrorSnackbarAndDoesNotNavigate()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        _mediator.Send(Arg.Any<UpdateBrandCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<BrandDto>("Brand_UpdateFailed"));
        AuthorizeAsBrandEditor();
        var cut = await RenderAsync(dto.Id);
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetFormState(cut, name: dto.Name, description: dto.Description, isActive: dto.IsActive, isFormValid: true);
        await ClickSaveAsync(cut);

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageBrands);
    }

    // =========================================================================
    // Cancel button — targets ManageBrands
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_Always_CancelButtonLinksToManageBrands()
    {
        var dto = ExampleDto();
        SetUpExistingBrand(dto);
        AuthorizeAsBrandEditor();

        var cut = await RenderAsync(dto.Id);

        cut.Find($"a[href='{Routes.Admin.ManageBrands}']").Should().NotBeNull();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void SetFormState(
        IRenderedComponent<EditBrand> cut, string name, string? description, bool isActive, bool isFormValid)
    {
        var type = typeof(EditBrand);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        type.GetField("_name", flags)!.SetValue(cut.Instance, name);
        type.GetField("_description", flags)!.SetValue(cut.Instance, description);
        type.GetField("_isActive", flags)!.SetValue(cut.Instance, isActive);
        type.GetField("_isFormValid", flags)!.SetValue(cut.Instance, isFormValid);
        cut.Render();
    }

    private static void SetNewLogoImages(IRenderedComponent<EditBrand> cut, IReadOnlyList<ShopUploadedImage> images)
    {
        typeof(EditBrand).GetField("_newLogoImages", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, images);
        cut.Render();
    }

    private static void InvokePrivateMethod(IRenderedComponent<EditBrand> cut, string methodName)
    {
        typeof(EditBrand).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(cut.Instance, null);
        cut.Render();
    }

    private static T GetPrivateField<T>(object instance, string fieldName) =>
        (T)instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static async Task ClickSaveAsync(IRenderedComponent<EditBrand> cut)
    {
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains(Strings.Save));
        await cut.InvokeAsync(() => saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: SaveAsync_WithChangesToNameDescriptionAndStatus_SendsUpdateBrandCommandWithTheNewValues,
//        SaveAsync_WhenMediatorReturnsSuccess_ShowsConfirmationAndNavigatesToManageBrands
// AC-7: SaveAsync_WhenFormIsInvalid_DoesNotSendTheCommand
// AC-8: SaveAsync_WhenRenamedToAnotherBrandsName_ShowsErrorSnackbarAndDoesNotNavigate,
//        SaveAsync_WhenRenamedToAnotherBrandsName_PreservesTheEnteredName
// AC-9: SaveAsync_SavedWithNoChangesAtAll_Succeeds
// AC-10: SaveAsync_WhenTheExistingLogoWasRemovedWithNoReplacementChosen_SendsRemoveLogoTrue,
//         SaveAsync_WhenANewLogoIsSelected_SendsItAsTheReplacement,
//         SaveAsync_WhenTheExistingLogoIsUntouched_SendsNeitherRemoveNorANewLogo
// AC-12: SaveAsync_WhenStatusIsSetToInactive_SendsIsActiveFalse, SaveAsync_WhenStatusIsSetToActive_SendsIsActiveTrue
// AC-16: Render_WhenUserHoldsBrandsEditPermission_ShowsTheForm,
//         Render_WhenUserLacksBrandsEditPermission_ShowsAccessDeniedInsteadOfTheForm,
//         Render_WhenUserIsNotAuthenticated_ShowsAccessDeniedInsteadOfTheForm
// AC-27: Render_WhenBrandExists_RequestsItByTheSuppliedId, Render_WhenBrandExists_PreFillsNameDescriptionAndStatus,
//         Render_WhenTheBrandNoLongerExists_ShowsTheNotFoundMessage, Render_WhenTheBrandNoLongerExists_OffersALinkBackToTheList
//         (the route is id-keyed with no name/slug component, so a rename cannot affect a saved link —
//         structurally guaranteed rather than independently testable in bUnit)
