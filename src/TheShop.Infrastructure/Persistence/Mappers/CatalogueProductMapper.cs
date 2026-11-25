using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

internal static class CatalogueProductMapper
{
    public static Product ToDomain(this CatalogueProductRowRecord row, Func<string, string> resolvePublicUrl) =>
        new ProductRecord
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            Sku = row.Sku,
            OriginalPrice = row.OriginalPrice,
            SalePrice = row.SalePrice,
            Currency = row.Currency,
            IsPublished = row.IsPublished,
            CategoryId = row.CategoryId,
            Category = new CategoryRecord { Id = row.CategoryId, Name = row.CategoryName },
            BrandId = row.BrandId,
            Brand = new BrandRecord { Id = row.BrandId, Name = row.BrandName },
            ImagePath = row.PrimaryImageKey ?? row.ImagePath,
            CreatedAt = row.CreatedAt,
            MinVariantPrice = row.MinVariantPrice,
            HasSellableVariant = row.HasSellableVariant,
        }.ToDomain(resolvePublicUrl);
}
