using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductListing;

/// <summary>Loads a management page with all authorized variant choices and selection cleanup.</summary>
[RequiresPermission("products.view")]
public sealed record GetAdminProductListingQuery(GetAdminProductsPageQuery PageQuery)
    : IRequest<Result<ProductListingPageDto<ProductListItemDto>>>;
