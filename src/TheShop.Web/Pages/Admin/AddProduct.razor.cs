using MediatR;
using Microsoft.AspNetCore.Components;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetActiveBrands;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetActiveCategories;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Creates a new product (Figma node <c>2687:14713</c>). A thin wrapper over
/// <c>ProductForm</c> — loads the Active category/brand pickers and hands off the rest.
/// Requires a signed-in user via <c>Pages/Admin/_Imports.razor</c>, and is gated on
/// <c>products.create</c> within the page itself.
/// </summary>
[Route(Routes.Admin.AddProduct)]
[AuthorizePermission("products.create")]
public partial class AddProduct : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private IReadOnlyList<CategoryLookupDto> _categories = [];
    private IReadOnlyList<BrandLookupDto> _brands = [];

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageProducts_Heading, Routes.Admin.ManageProducts)
            .Current(Strings.AddProduct_Heading));

        var categoriesResult = await Mediator.Send(new GetActiveCategoriesQuery());
        if (categoriesResult.IsSuccess)
            _categories = categoriesResult.Value;

        var brandsResult = await Mediator.Send(new GetActiveBrandsQuery());
        if (brandsResult.IsSuccess)
            _brands = brandsResult.Value;
    }
}
