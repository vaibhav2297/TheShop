using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;

/// <summary>Searches published products or resolves selected IDs.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode)]
public sealed record GetFreshDropsProductsQuery(IReadOnlyList<Guid>? Ids = null, string? Search = null,
    int Limit = 24, int Offset = 0) : IRequest<Result<IReadOnlyList<FreshDropsProductDto>>>;
