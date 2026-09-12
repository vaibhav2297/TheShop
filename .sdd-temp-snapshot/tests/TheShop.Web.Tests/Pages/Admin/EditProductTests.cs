using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetActiveBrands;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetActiveCategories;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductForEdit;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="EditProduct"/> page: permission gating that denies a view-only
/// admin's direct link (AC-3), the pre-filled form (AC-6), the deactivated-category-kept edge
/// case (AC-24), and the not-found panel for a stale edit link (AC-34).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class EditProductTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public EditProductTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddMudServices();
        var popoverService = Substitute.For<IPopoverService>();
        popoverService.PopoverOptions.Returns(new PopoverOptions());
        Services.Replace(ServiceDescriptor.Singleton(popoverService));

        _localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));

        _mediator.Send(Arg.Any<GetActiveCategoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CategoryLookupDto>>([new CategoryLookupDto(Guid.NewGuid(), "Disposables")]));
        _mediator.Send(Arg.Any<GetActiveBrandsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<BrandLookupDto>>([new BrandLookupDto(Guid.NewGuid(), "Elf Bar")]));
    }

    private void AuthorizeAsProductEditor()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.Edit.Code));
    }

    private static AdminProductDto ExampleDto(
        Guid? id = null, string categoryName = "Disposables", string brandName = "Elf Bar") =>
        new(id ?? Guid.NewGuid(), "Elf Bar BC5000", "A long-lasting disposable vape.", "ELF-BC5000",
            Guid.NewGuid(), categoryName, Guid.NewGuid(), brandName, 24.99m, null, true, [], [], [], [], "row-version-1");

    private void SetUpExistingProduct(AdminProductDto dto) =>
        _mediator.Send(Arg.Is<GetProductForEditQuery>(q => q.Id == dto.Id), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(dto));

    private async Task<IRenderedComponent<EditProduct>> RenderAsync(Guid id)
    {
        var cut = Render<EditProduct>(p => p.Add(c => c.Id, id));
        await cut.InvokeAsync(() => { });
        return cut;
    }

    // =========================================================================
    // Permission gating — the page-level products.edit gate denies a view-only admin (AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenUserHoldsProductsEditPermission_ShowsTheForm()
    {
        var dto = ExampleDto();
        SetUpExistingProduct(dto);
        AuthorizeAsProductEditor();

        var cut = await RenderAsync(dto.Id);

        cut.Markup.Should().Contain(Strings.EditProduct_Heading);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void EditProduct_Always_CarriesTheProductsEditAuthorizePolicy()
    {
        var attributes = typeof(EditProduct).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Products.Edit.Code),
            "the route-level policy, not the query, is what denies a view-only admin's direct " +
            "link — App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // Pre-filled form (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenTheProductExists_RequestsItByTheSuppliedId()
    {
        var dto = ExampleDto();
        SetUpExistingProduct(dto);
        AuthorizeAsProductEditor();

        await RenderAsync(dto.Id);

        await _mediator.Received(1).Send(Arg.Is<GetProductForEditQuery>(q => q.Id == dto.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenTheProductExists_PassesEditModeAndTheLoadedDataToTheForm()
    {
        var dto = ExampleDto();
        SetUpExistingProduct(dto);
        AuthorizeAsProductEditor();

        var cut = await RenderAsync(dto.Id);

        var form = cut.FindComponent<ProductForm>();
        form.Instance.Mode.Should().Be(ProductFormMode.Edit);
        form.Instance.InitialData.Should().Be(dto);
    }

    // =========================================================================
    // Deactivated assigned category/brand kept, shown as inactive (AC-24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenTheAssignedCategoryHasSinceBeenDeactivated_StillOffersItInTheForm()
    {
        // The Active-only lookup from GetActiveCategoriesQuery does not include the product's own
        // (now-inactive) category — ProductForm.DisplayCategories is what adds it back so the
        // current assignment still shows.
        var dto = ExampleDto(categoryName: "Retired Category");
        SetUpExistingProduct(dto);
        AuthorizeAsProductEditor();

        var cut = await RenderAsync(dto.Id);

        var form = cut.FindComponent<ProductForm>();
        form.Instance.Categories.Should().NotContain(c => c.Name == "Retired Category",
            "only Active categories come from the lookup query");
        form.Instance.InitialData!.CategoryName.Should().Be("Retired Category",
            "the current, possibly-inactive assignment is still passed through for the form to display");
    }

    // =========================================================================
    // Stale edit link (AC-34)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenTheProductNoLongerExists_ShowsTheNotFoundMessage()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetProductForEditQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<AdminProductDto>("Product_NotFound"));
        AuthorizeAsProductEditor();

        var cut = await RenderAsync(id);

        cut.Markup.Should().Contain(Strings.EditProduct_NotFoundTitle);
        cut.Markup.Should().NotContain(Strings.EditProduct_Heading);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenTheProductNoLongerExists_OffersALinkBackToTheList()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetProductForEditQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<AdminProductDto>("Product_NotFound"));
        AuthorizeAsProductEditor();

        var cut = await RenderAsync(id);

        cut.Find($"a[href='{Routes.Admin.ManageProducts}']").Should().NotBeNull();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-3 (edit control shown only with products.edit; direct link denied): Render_WhenUserHoldsProductsEditPermission_ShowsTheForm,
//        EditProduct_Always_CarriesTheProductsEditAuthorizePolicy
// AC-6 (edit form opens pre-filled with everything saved): Render_WhenTheProductExists_RequestsItByTheSuppliedId,
//        Render_WhenTheProductExists_PassesEditModeAndTheLoadedDataToTheForm
// AC-24 (deactivated assigned category kept, shown as inactive): Render_WhenTheAssignedCategoryHasSinceBeenDeactivated_StillOffersItInTheForm
// AC-34 (stale edit link → not-found with a way back): Render_WhenTheProductNoLongerExists_ShowsTheNotFoundMessage,
//        Render_WhenTheProductNoLongerExists_OffersALinkBackToTheList
