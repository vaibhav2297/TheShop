namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One value of a <see cref="ProductOptionTypeDto"/>, as surfaced to the admin edit form.
/// </summary>
public sealed record ProductOptionValueDto(Guid Id, string Value, int Position);
