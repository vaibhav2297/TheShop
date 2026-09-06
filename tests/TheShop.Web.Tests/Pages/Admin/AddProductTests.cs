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
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="AddProduct"/> page: permission gating (AC-3, AC-5), that it opens the
/// product form empty with Active category/brand pickers loaded (AC-4, AC-5, AC-23), and that
/// only Active lookups are offered.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class AddProductTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public AddProductTests()
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

    private void AuthorizeAsProductCreator()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.Create.Code));
    }

    private async Task<IRenderedComponent<AddProduct>> RenderAsync()
    {
        AuthorizeAsProductCreator();
        var cut = Render<AddProduct>();
        await cut.InvokeAsync(() => { });
        return cut;
    }

    // =========================================================================
    // Permission gating (AC-3, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenUserHoldsProductsCreatePermission_ShowsTheForm()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().Contain(Strings.AddProduct_Heading);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void AddProduct_Always_CarriesTheProductsCreateAuthorizePolicy()
    {
        var attributes = typeof(AddProduct).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Products.Create.Code),
            "the route-level policy is the only gate between a non-creator and the form — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // Opens empty, in Create mode (AC-4, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_Always_PassesCreateModeAndNoInitialDataToTheForm()
    {
        var cut = await RenderAsync();

        var form = cut.FindComponent<ProductForm>();
        form.Instance.Mode.Should().Be(ProductFormMode.Create);
        form.Instance.InitialData.Should().BeNull();
    }

    // =========================================================================
    // Active category/brand pickers loaded (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_LoadsActiveCategoriesAndBrandsIntoTheForm()
    {
        var cut = await RenderAsync();

        var form = cut.FindComponent<ProductForm>();
        form.Instance.Categories.Should().ContainSingle(c => c.Name == "Disposables");
        form.Instance.Brands.Should().ContainSingle(b => b.Name == "Elf Bar");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_Always_SetsBreadcrumbTrailToAddProductHeading()
    {
        var cut = await RenderAsync();

        var breadcrumbs = Services.GetRequiredService<BreadcrumbState>();
        breadcrumbs.Trail.Should().Contain(item => item.Text == Strings.AddProduct_Heading);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-3 (add control shown only with products.create): Render_WhenUserHoldsProductsCreatePermission_ShowsTheForm,
//        AddProduct_Always_CarriesTheProductsCreateAuthorizePolicy
// AC-4 / AC-5 (add form has its own address and opens empty, Unpublished): Render_Always_PassesCreateModeAndNoInitialDataToTheForm
// AC-23 (only Active categories/brands offered): Render_LoadsActiveCategoriesAndBrandsIntoTheForm
