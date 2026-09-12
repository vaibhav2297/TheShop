using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Mappers;

/// <summary>
/// Hand-written command DTO → Domain input mapping, shared by <c>CreateProductHandler</c> and
/// <c>UpdateProductHandler</c> (the second real call site — constitution Rule 25).
/// </summary>
internal static class ProductInputMapper
{
    public static ProductPricing? ToPricing(decimal? originalPrice, decimal? salePrice) =>
        originalPrice is decimal original
            ? ProductPricing.Create(Money.Create(original), salePrice is decimal sale ? Money.Create(sale) : null)
            : null;

    public static IReadOnlyList<ProductOptionTypeInput> ToOptionTypeInputs(IReadOnlyList<OptionTypeInput> types) =>
        [.. types.Select(t => new ProductOptionTypeInput(
            t.Id, t.Name, [.. t.Values.Select(v => new ProductOptionValueInput(v.Id, v.Value))]))];

    /// <summary>
    /// Maps the command's specification rows onto the aggregate's input shape. The command's own
    /// <see cref="SpecificationInput.Position"/> is display-only — <c>Product.SetSpecifications</c>
    /// assigns position by list order, which the command already carries the rows in.
    /// </summary>
    public static IReadOnlyList<ProductSpecificationInput> ToSpecificationInputs(
        IReadOnlyList<SpecificationInput> specifications) =>
        [.. specifications.Select(s => new ProductSpecificationInput(s.Id, s.Name, s.Value))];

    /// <summary>
    /// Maps the command's variant rows onto the aggregate's input shape, resolving each pin through
    /// <paramref name="imageIdByClientId"/> — the map from the caller's own gallery identifiers to
    /// the identifiers the domain minted for this save. A pin that resolves to nothing is dropped
    /// rather than carried through: it names an image that is not in the product's gallery, which
    /// RULE-13 forbids, and the same rule already leaves a variant unpinned when its image goes.
    /// </summary>
    public static IReadOnlyList<ProductVariantInput> ToVariantInputs(
        IReadOnlyList<VariantInput> variants,
        IReadOnlyDictionary<Guid, Guid> imageIdByClientId) =>
        [.. variants.Select(v => new ProductVariantInput(
            v.Id,
            new HashSet<Guid>(v.OptionValueIds),
            Sku.Create(v.Sku),
            ToPricing(v.OriginalPrice, v.SalePrice),
            v.IsAvailable,
            ResolvePin(v.PinnedImageId, imageIdByClientId)))];

    private static Guid? ResolvePin(Guid? clientImageId, IReadOnlyDictionary<Guid, Guid> imageIdByClientId) =>
        clientImageId is Guid clientId && imageIdByClientId.TryGetValue(clientId, out var imageId)
            ? imageId
            : null;

    /// <summary>
    /// Normalizes the product's own SKU and every variant SKU (RULE-8) into the comparison set
    /// <c>IProductRepository.FindConflictsAsync</c> checks against the catalogue.
    /// </summary>
    public static IReadOnlyCollection<string> CollectNormalizedSkus(string productSku, IReadOnlyList<VariantInput> variants) =>
        [.. new[] { productSku }.Concat(variants.Select(v => v.Sku))
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .Distinct()];
}
