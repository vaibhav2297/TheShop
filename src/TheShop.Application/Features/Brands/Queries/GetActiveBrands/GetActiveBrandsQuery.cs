using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Queries.GetActiveBrands;

/// <summary>
/// Requests every Active brand as a picker lookup (AC-23). Undecorated — brand names are public
/// reference data (<c>brands_public_read</c>), so no permission gate applies.
/// </summary>
public sealed record GetActiveBrandsQuery : IRequest<Result<IReadOnlyList<BrandLookupDto>>>;
