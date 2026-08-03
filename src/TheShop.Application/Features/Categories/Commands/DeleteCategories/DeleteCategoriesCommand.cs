using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Commands.DeleteCategories;

/// <summary>
/// Deletes every category in <paramref name="CategoryIds"/> that has no referencing product
/// (RULE-6/RULE-15). Covers both the single-row delete and the bulk action (plan §5 Decision 4) —
/// a one-element list is the single-row case.
/// </summary>
[RequiresPermission("categories.delete")]
public sealed record DeleteCategoriesCommand(IReadOnlyList<Guid> CategoryIds) : IRequest<Result<CategoryDeletionOutcomeDto>>;
