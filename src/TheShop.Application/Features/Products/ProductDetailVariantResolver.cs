using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products;

/// <summary>Resolves only explicit, complete storefront option selections.</summary>
public static class ProductDetailVariantResolver
{
    /// <summary>Returns the matching variant, or null until exactly one value in every group forms a configured combination.</summary>
    public static ProductDetailVariantDto? Resolve(ProductDetailDto product, IReadOnlyCollection<Guid> selectedValueIds)
    {
        if (!product.HasVariants || product.OptionTypes.Count == 0)
            return null;
        var selected = selectedValueIds.ToHashSet();
        if (selected.Count != selectedValueIds.Count || selected.Count != product.OptionTypes.Count ||
            product.OptionTypes.Any(t => t.Values.Count(v => selected.Contains(v.Id)) != 1))
            return null;
        return product.Variants.FirstOrDefault(v => selected.SetEquals(v.OptionValueIds));
    }
}
