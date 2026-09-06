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
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="ManageProducts"/> admin harness shell — the verification surface for
/// RBAC's admin gating (Figma node <c>2470:2200</c>). Gated on <c>products.view</c> by its
/// route-level <c>[AuthorizePermission]</c> attribute — the denied/redirect experience is
/// App.razor's router scaffolding, so this class asserts the structural seam. Permission
/// policies resolve against the claims minted into the access token; bUnit's auth context
/// stands in for that principal here.
///
/// Also carries the create-product feature's coverage of the real paginated list: row content,
/// pagination controls, the permission-gated add/edit affordances (AC-1, AC-2, AC-3, AC-35).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class ManageProductsTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public ManageProductsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BreadcrumbState>();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddMudServices();
        var popoverService = Substitute.For<IPopoverService>();
        popoverService.PopoverOptions.Returns(new PopoverOptions());
        Services.Replace(ServiceDescriptor.Singleton(popoverService));

        _localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));

        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => BuildPage(call.Arg<GetAdminProductsPageQuery>().Pagination, totalCount: 1, itemCount: 1));
    }

    private void AuthorizeAsProductManager()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Products.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Products.Create.Code),
            PolicyNames.Permission(PermissionCatalogue.Products.Edit.Code));
    }

    private void AuthorizeAsProductViewerOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("viewer-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));
    }

    private static ProductListItemDto Item(bool isPublished = true, bool hasVariants = false, decimal? minVariantPrice = null) =>
        new(Guid.NewGuid(), "Elf Bar BC5000", "ELF-BC5000", "https://example.com/photo.webp",
            "Elf Bar", "Disposables", hasVariants ? null : 24.99m, minVariantPrice, hasVariants, "CAD", isPublished);

    private static Result<PagedResult<ProductListItemDto>> BuildPage(PaginationRequest pagination, int totalCount, int itemCount)
    {
        var items = Enumerable.Range(1, itemCount).Select(_ => Item()).ToList();
        return Result.Ok(new PagedResult<ProductListItemDto>(items, pagination.Page, pagination.PageSize, totalCount));
    }

    private void SetUpKnownProductsList(IReadOnlyList<ProductListItemDto> items) =>
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new PagedResult<ProductListItemDto>(
                items, call.Arg<GetAdminProductsPageQuery>().Pagination.Page, 10, items.Count)));

    private async Task<IRenderedComponent<ManageProducts>> RenderListAsync()
    {
        AuthorizeAsProductManager();
        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });
        return cut;
    }

    // =========================================================================
    // create-product — row content and pagination (AC-1, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenProductsExist_ShowsEachProductsNameSkuBrandCategoryAndStatus()
    {
        var item = Item();
        SetUpKnownProductsList([item]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(item.Name);
        cut.Markup.Should().Contain(item.Sku);
        cut.Markup.Should().Contain(item.BrandName);
        cut.Markup.Should().Contain(item.CategoryName);
        cut.Markup.Should().Contain(Strings.ManageProducts_StatusActive);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenAProductIsUnpublished_ShowsInactiveStatus()
    {
        SetUpKnownProductsList([Item(isPublished: false)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageProducts_StatusInactive);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenAProductHasVariants_ShowsAFromPrice()
    {
        SetUpKnownProductsList([Item(hasVariants: true, minVariantPrice: 12.50m)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(string.Format(Strings.ManageProducts_PriceFrom, "$12.50"));
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_OnInitialize_RequestsTheFirstPageOfTen()
    {
        AuthorizeAsProductManager();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        await _mediator.Received(1).Send(
            Arg.Is<GetAdminProductsPageQuery>(q => q.Pagination.Page == 1 && q.Pagination.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenProductsSpanMultiplePages_ShowsPaginationControls()
    {
        var items = new[] { Item() };
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new PagedResult<ProductListItemDto>(
                items, call.Arg<GetAdminProductsPageQuery>().Pagination.Page, 10, 24)));

        var cut = await RenderListAsync();

        cut.FindComponent<ShopPagination>().Instance.TotalPages.Should().Be(3);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task ChangePage_RequestsTheNextPageOfTenInTheSameOrder()
    {
        var items = new[] { Item() };
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new PagedResult<ProductListItemDto>(
                items, call.Arg<GetAdminProductsPageQuery>().Pagination.Page, 10, 24)));
        var cut = await RenderListAsync();

        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));

        await _mediator.Received(1).Send(
            Arg.Is<GetAdminProductsPageQuery>(q => q.Pagination.Page == 2), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // create-product — empty state (AC-35)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenNoProductsExistYet_ShowsTheEmptyStateMessage()
    {
        SetUpKnownProductsList([]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageProducts_EmptyTitle);
        cut.Markup.Should().Contain(Strings.ManageProducts_EmptyDescription);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenNoProductsExistAndUserHoldsCreatePermission_ShowsTheAddProductControl()
    {
        SetUpKnownProductsList([]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.AddProduct_Heading);
    }

    // =========================================================================
    // create-product — permission-gated add/edit affordances (AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenUserHoldsOnlyProductsView_DoesNotShowTheAddProductButton()
    {
        AuthorizeAsProductViewerOnly();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        cut.Markup.Should().NotContain(Strings.AddProduct_Heading);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_WhenUserHoldsOnlyProductsView_DoesNotShowTheEditRowControl()
    {
        var item = Item();
        SetUpKnownProductsList([item]);
        AuthorizeAsProductViewerOnly();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        cut.FindAll($"[aria-label='{string.Format(Strings.ManageProducts_EditAria, item.Name)}']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Render_EditControl_LinksToTheProductsIdKeyedEditRoute()
    {
        var item = Item();
        SetUpKnownProductsList([item]);

        var cut = await RenderListAsync();

        cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_EditAria, item.Name)}']")
           .GetAttribute("href").Should().Be(Routes.Admin.EditProduct(item.Id));
    }

    // =========================================================================
    // Held products.view permission — content renders
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_WhenUserHoldsProductsViewPermission_ShowsManageProductsContent()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        var cut = Render<ManageProducts>();

        cut.Markup.Should().Contain(Strings.ManageProducts_Heading);
    }

    // =========================================================================
    // Structural — the page carries its own products.view route-level policy; the
    // denied/redirect experience it triggers is App.razor's router scaffolding
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void ManageProducts_Always_CarriesTheProductsViewAuthorizePolicy()
    {
        var attributes = typeof(ManageProducts).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Products.View.Code),
            "the route-level policy is the only gate between a non-viewer and the shell — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // Breadcrumb — set unconditionally in OnInitialized, independent of the permission gate
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_SetsBreadcrumbTrailToManageProductsHeading()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        Render<ManageProducts>();

        var breadcrumbs = Services.GetRequiredService<BreadcrumbState>();
        breadcrumbs.Trail.Should().Contain(item => item.Text == Strings.ManageProducts_Heading);
    }
}

// =============================================================================
// AC → Test mapping (create-product)
// =============================================================================
// AC-1 (row content: image/name/brand/category/price/status): Render_WhenProductsExist_ShowsEachProductsNameSkuBrandCategoryAndStatus,
//        Render_WhenAProductIsUnpublished_ShowsInactiveStatus, Render_WhenAProductHasVariants_ShowsAFromPrice,
//        Render_OnInitialize_RequestsTheFirstPageOfTen
// AC-2 (pagination, same newest-first order): Render_WhenProductsSpanMultiplePages_ShowsPaginationControls,
//        ChangePage_RequestsTheNextPageOfTenInTheSameOrder
// AC-3 (add/edit affordances shown only with the permission): Render_WhenUserHoldsOnlyProductsView_DoesNotShowTheAddProductButton,
//        Render_WhenUserHoldsOnlyProductsView_DoesNotShowTheEditRowControl
// AC-35 (empty state, add option only with the permission): Render_WhenNoProductsExistYet_ShowsTheEmptyStateMessage,
//        Render_WhenNoProductsExistAndUserHoldsCreatePermission_ShowsTheAddProductControl
// (edit link id-keyed, matches the AddCategory/AddBrand convention): Render_EditControl_LinksToTheProductsIdKeyedEditRoute
