namespace TheShop.Application.Features.Products.DTOs;

/// <summary>Saved stable product and variant identifiers; URL formatting belongs to Web.</summary>
public sealed record ProductAddressDto(string ProductSlug, IReadOnlyDictionary<Guid, string> VariantSlugs);
