namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One variant row in a create/update command's desired configuration, keyed by
/// <see cref="OptionValueIds"/>. A combination the caller omits keeps its previously configured
/// data (RULE-12); one no longer valid against the submitted option types is dropped.
/// </summary>
public sealed record VariantInput(
    Guid? Id,
    IReadOnlyList<Guid> OptionValueIds,
    string Sku,
    decimal? OriginalPrice,
    decimal? SalePrice,
    bool IsAvailable,
    Guid? PinnedImageId);
