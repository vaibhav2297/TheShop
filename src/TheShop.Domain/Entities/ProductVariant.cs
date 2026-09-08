using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// One variant of a product — a specific combination of its option values, with its own SKU,
/// pricing, availability, and at most one pinned gallery image. Owned by
/// <see cref="Product"/> — never persisted or mutated on its own; assembled exclusively through
/// <see cref="Product.ApplyVariantConfiguration"/>.
/// </summary>
public sealed class ProductVariant
{
    public Guid Id { get; }
    public Sku Sku { get; private set; }
    public ProductPricing? Pricing { get; private set; }
    public bool IsAvailable { get; private set; }
    public Guid? PinnedImageId { get; private set; }
    public IReadOnlySet<Guid> OptionValueIds { get; }
    public int Position { get; private set; }

    private ProductVariant(
        Guid id,
        Sku sku,
        ProductPricing? pricing,
        bool isAvailable,
        Guid? pinnedImageId,
        IReadOnlySet<Guid> optionValueIds,
        int position)
    {
        Id = id;
        Sku = sku;
        Pricing = pricing;
        IsAvailable = isAvailable;
        PinnedImageId = pinnedImageId;
        OptionValueIds = optionValueIds;
        Position = position;
    }

    /// <summary>
    /// Creates a <see cref="ProductVariant"/>, reusing <paramref name="id"/> when supplied
    /// (an existing variant kept across an edit) or assigning a new one otherwise.
    /// </summary>
    public static ProductVariant Create(
        Guid? id,
        Sku sku,
        ProductPricing? pricing,
        bool isAvailable,
        Guid? pinnedImageId,
        IReadOnlySet<Guid> optionValueIds,
        int position) =>
        new(id ?? Guid.NewGuid(), sku, pricing, isAvailable, pinnedImageId, optionValueIds, position);

    /// <summary>
    /// Pins this variant to a gallery image already verified to belong to the owning product.
    /// </summary>
    internal void Pin(Guid imageId) => PinnedImageId = imageId;

    /// <summary>
    /// Clears this variant's pin — e.g. when its pinned image is removed from the gallery
    /// (RULE-13).
    /// </summary>
    internal void Unpin() => PinnedImageId = null;
}
