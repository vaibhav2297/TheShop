namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// A brand that <c>delete_brands</c> could not delete because a product still references it.
/// <see cref="Name"/> feeds the single-brand <c>Brand_InUse</c> message; the bulk outcome message
/// is count-only and never enumerates names — the blocked-row in-use indicator in the table names
/// them instead (plan §5 Decision 12).
/// </summary>
public sealed record BlockedBrandDto(Guid Id, string Name, int ProductCount);
