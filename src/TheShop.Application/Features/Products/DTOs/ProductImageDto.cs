namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One image in a product's gallery, as surfaced to the admin edit form. <see cref="Url"/> is
/// the resolved public bucket URL, not the stored object key.
/// </summary>
public sealed record ProductImageDto(Guid Id, string Url, int Position, bool IsPrimary);
