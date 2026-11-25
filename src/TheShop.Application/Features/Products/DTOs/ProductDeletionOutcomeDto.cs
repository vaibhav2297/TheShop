namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The outcome of a single or bulk product deletion (RULE-3/RULE-4 partial success). Deletion
/// always applies to every deletable product in the request — <see cref="Blocked"/> lists the rest.
/// </summary>
public sealed record ProductDeletionOutcomeDto(int DeletedCount, IReadOnlyList<ReferencedProductDto> Blocked);
