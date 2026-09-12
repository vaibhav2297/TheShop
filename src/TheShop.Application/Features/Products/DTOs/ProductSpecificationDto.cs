namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One name/value specification row, as surfaced to the admin edit form.
/// </summary>
public sealed record ProductSpecificationDto(Guid Id, string Name, string Value, int Position);
