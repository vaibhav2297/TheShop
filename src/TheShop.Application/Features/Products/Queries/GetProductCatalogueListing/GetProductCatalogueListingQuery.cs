using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;

namespace TheShop.Application.Features.Products.Queries.GetProductCatalogueListing;

/// <summary>Loads a public page together with eligible variant choices and selection cleanup.</summary>
public sealed record GetProductCatalogueListingQuery(GetProductCataloguePageQuery PageQuery)
    : IRequest<Result<ProductListingPageDto<ProductSummaryDto>>>;
