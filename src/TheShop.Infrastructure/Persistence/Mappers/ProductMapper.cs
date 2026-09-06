using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps <see cref="ProductRecord"/> rows (joined with their <see cref="CategoryRecord"/> and
/// <see cref="BrandRecord"/> via Postgrest resource embedding) into the domain
/// <see cref="Product"/> aggregate.
/// </summary>
internal static class ProductMapper
{
    /// <summary>
    /// Maps a <see cref="ProductRecord"/> to a domain <see cref="Product"/>, resolving the
    /// product image. When <see cref="ProductRecord.ImagePath"/> is set (an admin-uploaded
    /// Supabase Storage object key) it is turned into a public URL via
    /// <paramref name="resolvePublicUrl"/>; when it is absent a placeholder image URL is generated
    /// using the product name. When <paramref name="images"/>/<paramref name="optionTypes"/>/
    /// <paramref name="variants"/> are supplied (the admin edit-load path) the aggregate carries
    /// its full gallery/option-type/variant children; otherwise (the catalogue/admin-list read
    /// paths) it carries the DB's precomputed <c>min_variant_price</c>/<c>has_sellable_variant</c>
    /// read model instead, per <see cref="Product.Rehydrate"/>'s read-optimized overload.
    /// </summary>
    public static Product ToDomain(
        this ProductRecord record,
        Func<string, string> resolvePublicUrl,
        IReadOnlyList<ProductImage>? images = null,
        IReadOnlyList<ProductOptionType>? optionTypes = null,
        IReadOnlyList<ProductVariant>? variants = null)
    {
        if (record.Category is null)
            throw new InvalidOperationException($"Product '{record.Id}' is missing its embedded category join.");

        if (record.Brand is null)
            throw new InvalidOperationException($"Product '{record.Id}' is missing its embedded brand join.");

        var pricing = record.OriginalPrice is decimal original
            ? ProductPricing.Create(
                Money.Create(original, record.Currency),
                record.SalePrice is decimal sale ? Money.Create(sale, record.Currency) : null)
            : null;

        var category = Category.Rehydrate(record.Category.Id, record.Category.Name);
        var brand = Brand.Rehydrate(record.Brand.Id, record.Brand.Name);

        var imageUrl = !string.IsNullOrWhiteSpace(record.ImagePath)
            ? resolvePublicUrl(record.ImagePath)
            : PlaceholderImage.For(record.Name);

        return Product.Rehydrate(
            record.Id,
            record.Name,
            record.Description,
            imageUrl,
            Sku.Create(record.Sku),
            pricing,
            record.IsPublished,
            category,
            brand,
            record.CreatedAt,
            images,
            optionTypes,
            variants,
            hasVariants: record.MinVariantPrice is not null || variants is { Count: > 0 },
            minVariantPrice: record.MinVariantPrice is decimal min ? Money.Create(min, record.Currency) : null,
            hasSellableVariant: record.HasSellableVariant);
    }
}
