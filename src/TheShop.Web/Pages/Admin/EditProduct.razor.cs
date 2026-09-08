using MediatR;
using Microsoft.AspNetCore.Components;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetActiveBrands;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetActiveCategories;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductForEdit;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Edits an existing product, id-keyed. A thin wrapper over <c>ProductForm</c> — loads the
/// product, the Active category/brand pickers, and hands off the rest. A stale id renders a
/// not-found panel with a way back (AC-34). Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>products.edit</c> within the page
/// itself — <see cref="GetProductForEditQuery"/> only requires <c>products.view</c>, so this
/// page-level gate is what denies a view-only admin's direct link.
/// </summary>
[Route(Routes.Admin.EditProductPattern)]
[AuthorizePermission("products.edit")]
public partial class EditProduct : ComponentBase
{
    [Parameter]
    public Guid Id { get; set; }

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private AdminProductDto? _product;
    private IReadOnlyList<CategoryLookupDto> _categories = [];
    private IReadOnlyList<BrandLookupDto> _brands = [];
    private bool _loaded;
    private bool _notFound;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageProducts_Heading, Routes.Admin.ManageProducts)
            .Current(Strings.EditProduct_Heading));

        var productResult = await Mediator.Send(new GetProductForEditQuery(Id));
        if (!productResult.IsSuccess)
        {
            _notFound = true;
            _loaded = true;
            return;
        }

        _product = productResult.Value;

        var categoriesResult = await Mediator.Send(new GetActiveCategoriesQuery());
        if (categoriesResult.IsSuccess)
            _categories = categoriesResult.Value;

        var brandsResult = await Mediator.Send(new GetActiveBrandsQuery());
        if (brandsResult.IsSuccess)
            _brands = brandsResult.Value;

        _loaded = true;
    }
}
