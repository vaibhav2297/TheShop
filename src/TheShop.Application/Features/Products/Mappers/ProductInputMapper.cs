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
    /// Resolves every staged image identifier to its saved identity, rejecting unknown identifiers.
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
            v.ImageIds.Select(id => imageIdByClientId.TryGetValue(id, out var imageId)
                ? imageId : throw new TheShop.Domain.Exceptions.ProductImageNotOwnedException()).ToArray()))];

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
