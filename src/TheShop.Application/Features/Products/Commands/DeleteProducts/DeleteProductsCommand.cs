using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Commands.DeleteProducts;

/// <summary>
/// Deletes every product in <paramref name="ProductIds"/> that has no referencing record
/// (FR-12, RULE-3). Covers both the single-row delete and the bulk action (plan §5 Decision 2) —
/// a one-element list is the single-row case.
/// </summary>
[RequiresPermission("products.delete")]
public sealed record DeleteProductsCommand(IReadOnlyList<Guid> ProductIds) : IRequest<Result<ProductDeletionOutcomeDto>>;
