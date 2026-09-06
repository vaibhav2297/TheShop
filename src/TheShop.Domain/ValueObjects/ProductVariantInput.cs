namespace TheShop.Domain.ValueObjects;

/// <summary>
/// One variant row passed to <c>Product.ApplyVariantConfiguration</c>, keyed by the set of
/// option-value ids the variant is a combination of. A combination absent from this list keeps
/// its previously configured data verbatim (RULE-12); a combination absent from the product's
/// current option types is dropped regardless of whether it appears here (RULE-10).
/// </summary>
public sealed record ProductVariantInput(
    Guid? Id,
    IReadOnlySet<Guid> OptionValueIds,
    Sku Sku,
    ProductPricing? Pricing,
    bool IsAvailable,
    Guid? PinnedImageId);
