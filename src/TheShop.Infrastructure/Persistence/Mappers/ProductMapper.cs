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
    /// using the product name.
    /// </summary>
    public static Product ToDomain(this ProductRecord record, Func<string, string> resolvePublicUrl)
    {
        if (record.Category is null)
            throw new InvalidOperationException($"Product '{record.Id}' is missing its embedded category join.");

        if (record.Brand is null)
            throw new InvalidOperationException($"Product '{record.Id}' is missing its embedded brand join.");

        var originalPrice = Money.Create(record.OriginalPrice, record.Currency);
        var salePrice = record.SalePrice is decimal sale ? Money.Create(sale, record.Currency) : null;
        var pricing = ProductPricing.Create(originalPrice, salePrice);

        var category = Category.Create(record.Category.Id, record.Category.Name, record.Category.Slug);
        var brand = Brand.Rehydrate(record.Brand.Id, record.Brand.Name, record.Brand.Slug);

        var imageUrl = !string.IsNullOrWhiteSpace(record.ImagePath)
            ? resolvePublicUrl(record.ImagePath)
            : $"https://placehold.co/400x400/E8E8E8/7A7A7A?text={Uri.EscapeDataString(record.Name)}&font=raleway";

        return Product.Rehydrate(
            record.Id,
            record.Name,
            record.Description,
            imageUrl,
            pricing,
            record.StockQuantity,
            record.IsPublished,
            category,
            brand,
            record.Flavour,
            record.NicotineStrengthMg,
            new DateTimeOffset(DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc)));
    }
}
