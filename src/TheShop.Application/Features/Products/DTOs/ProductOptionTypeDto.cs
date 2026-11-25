namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One option type a product varies by, as surfaced to the admin edit form.
/// </summary>
public sealed record ProductOptionTypeDto(
    Guid Id, string Name, int Position, IReadOnlyList<ProductOptionValueDto> Values);
