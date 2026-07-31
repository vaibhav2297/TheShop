namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// The outcome of a single or bulk brand deletion (RULE-13 partial success). Deletion always
/// applies to every deletable brand in the request — <see cref="Blocked"/> lists the rest.
/// </summary>
public sealed record BrandDeletionOutcomeDto(int DeletedCount, IReadOnlyList<BlockedBrandDto> Blocked);
