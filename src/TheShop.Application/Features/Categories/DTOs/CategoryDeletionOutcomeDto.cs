namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// The outcome of a single or bulk category deletion (RULE-15 partial success). Deletion always
/// applies to every deletable category in the request — <see cref="Blocked"/> lists the rest.
/// </summary>
public sealed record CategoryDeletionOutcomeDto(int DeletedCount, IReadOnlyList<BlockedCategoryDto> Blocked);
