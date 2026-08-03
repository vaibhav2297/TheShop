namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// A category that <c>delete_categories</c> could not delete because a product still references
/// it. <see cref="Name"/> feeds the single-category <c>Category_InUse</c> message; the bulk
/// outcome message is count-only and never enumerates names — the blocked-row in-use indicator in
/// the table names them instead (plan §5 Decision 12).
/// </summary>
public sealed record BlockedCategoryDto(Guid Id, string Name, int ProductCount);
