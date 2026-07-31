using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Commands.DeleteBrands;

/// <summary>
/// Deletes every brand in <paramref name="BrandIds"/> that has no referencing product
/// (RULE-6/RULE-13). Covers both the single-row delete and the bulk action (plan §5 Decision 1) —
/// a one-element list is the single-row case.
/// </summary>
[RequiresPermission("brands.delete")]
public sealed record DeleteBrandsCommand(IReadOnlyList<Guid> BrandIds) : IRequest<Result<BrandDeletionOutcomeDto>>;
