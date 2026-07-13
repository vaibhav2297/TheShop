using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetCatalogueFilters;

/// <summary>
/// Requests the backend-driven filter groups for the catalogue's filter sidebar. Stable across
/// pagination — dispatched once on page load, not on every page turn.
/// </summary>
public sealed record GetCatalogueFiltersQuery : IRequest<Result<CatalogueFiltersDto>>;
