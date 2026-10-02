namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One configured variant of a <see cref="ProductDetailsDto"/>. <see cref="PinnedImageId"/>
/// references an image in the product's shared gallery; <see cref="OptionValueIds"/> are the
/// option values that identify this variant.
/// </summary>
public sealed record ProductDetailsVariantDto(
    Guid Id,
    int Position,
    decimal? OriginalPrice,
    decimal? SalePrice,
    string Currency,
    bool IsAvailable,
    Guid? PinnedImageId,
    IReadOnlyList<Guid> OptionValueIds);
