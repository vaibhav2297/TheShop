using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products;

/// <summary>
/// Deterministic variant, pricing, and gallery-image selection for the customer product-details
/// page (RULE-1 – RULE-4). Every method projects from an already-loaded
/// <see cref="ProductDetailsDto"/>; none reads or writes persisted state. Availability flags only
/// steer the initial preference — every configured variant stays viewable.
/// </summary>
public static class ProductDetailsSelection
{
    /// <summary>
    /// Resolves the initial selection: the first variant flagged available (or the first
    /// configured variant when none is flagged) with its pricing and linked image, or the
    /// product's own pricing and primary/first image when it has no variants.
    /// </summary>
    public static ProductDetailsSelectionDto Resolve(ProductDetailsDto product)
    {
        var variant = product.Variants.FirstOrDefault(v => v.IsAvailable) ?? product.Variants.FirstOrDefault();
        return Project(product, variant);
    }

    /// <summary>
    /// Selects the variant <paramref name="variantId"/>, replacing pricing and the large image
    /// with that variant's pricing and linked image or primary/first fallback (RULE-4). An id
    /// outside the product's variants falls back to <see cref="Resolve(ProductDetailsDto)"/>.
    /// </summary>
    public static ProductDetailsSelectionDto SelectVariant(ProductDetailsDto product, Guid variantId)
    {
        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
        return variant is null ? Resolve(product) : Project(product, variant);
    }

    /// <summary>
    /// Applies a click on option value <paramref name="optionValueId"/>: keeps the current
    /// variant's other option values when a configured variant matches that combination, otherwise
    /// selects the first configured variant carrying the clicked value. A value no configured
    /// variant carries leaves <paramref name="current"/> unchanged.
    /// </summary>
    public static ProductDetailsSelectionDto SelectOptionValue(
        ProductDetailsDto product, ProductDetailsSelectionDto current, Guid optionValueId)
    {
        var optionType = product.OptionTypes.FirstOrDefault(t => t.Values.Any(v => v.Id == optionValueId));
        if (optionType is null || !IsOptionValueSelectable(product, optionValueId))
            return current;

        var currentVariant = FindVariant(product, current.VariantId);
        if (currentVariant is not null && currentVariant.OptionValueIds.Contains(optionValueId))
            return current;

        var siblingValueIds = optionType.Values.Select(v => v.Id).ToHashSet();
        var desired = (currentVariant?.OptionValueIds ?? [])
            .Where(id => !siblingValueIds.Contains(id))
            .Append(optionValueId)
            .ToHashSet();

        var match = product.Variants.FirstOrDefault(v => desired.IsSubsetOf(v.OptionValueIds))
            ?? product.Variants.First(v => v.OptionValueIds.Contains(optionValueId));

        return Project(product, match);
    }

    /// <summary>
    /// Shows gallery image <paramref name="imageId"/> large without changing the selected
    /// variant or pricing. An id outside the current gallery resets to the selection's linked
    /// image or primary/first fallback (RULE-2).
    /// </summary>
    public static ProductDetailsSelectionDto SelectImage(
        ProductDetailsDto product, ProductDetailsSelectionDto current, Guid? imageId)
    {
        var selectedImageId = imageId is { } id && product.Images.Any(i => i.Id == id)
            ? id
            : ResolveImageId(product, FindVariant(product, current.VariantId));

        return current with { SelectedImageId = selectedImageId };
    }

    /// <summary>
    /// <c>true</c> when at least one configured variant carries <paramref name="optionValueId"/>;
    /// availability flags never make a value unselectable.
    /// </summary>
    public static bool IsOptionValueSelectable(ProductDetailsDto product, Guid optionValueId) =>
        product.Variants.Any(v => v.OptionValueIds.Contains(optionValueId));

    /// <summary>
    /// <c>true</c> when <paramref name="optionValueId"/> belongs to the selected variant.
    /// </summary>
    public static bool IsOptionValueSelected(
        ProductDetailsDto product, ProductDetailsSelectionDto selection, Guid optionValueId) =>
        FindVariant(product, selection.VariantId)?.OptionValueIds.Contains(optionValueId) ?? false;

    private static ProductDetailsSelectionDto Project(ProductDetailsDto product, ProductDetailsVariantDto? variant) =>
        variant is null
            ? new ProductDetailsSelectionDto(
                null, product.OriginalPrice, product.SalePrice, product.Currency, ResolveImageId(product, null))
            : new ProductDetailsSelectionDto(
                variant.Id, variant.OriginalPrice, variant.SalePrice, variant.Currency, ResolveImageId(product, variant));

    private static ProductDetailsVariantDto? FindVariant(ProductDetailsDto product, Guid? variantId) =>
        variantId is { } id ? product.Variants.FirstOrDefault(v => v.Id == id) : null;

    /// <summary>
    /// The variant's linked image when it belongs to the gallery, otherwise the primary image,
    /// otherwise the first image, or <c>null</c> for an empty gallery (RULE-1, RULE-4).
    /// </summary>
    private static Guid? ResolveImageId(ProductDetailsDto product, ProductDetailsVariantDto? variant)
    {
        if (variant?.PinnedImageId is { } pinned && product.Images.Any(i => i.Id == pinned))
            return pinned;

        return (product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault())?.Id;
    }
}
