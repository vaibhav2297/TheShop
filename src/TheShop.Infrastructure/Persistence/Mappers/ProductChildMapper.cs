using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps the product aggregate's child records (<see cref="ProductImageRecord"/>,
/// <see cref="ProductOptionTypeRecord"/>/<see cref="ProductOptionValueRecord"/>,
/// <see cref="ProductSpecificationRecord"/>,
/// <see cref="ProductVariantRecord"/>/<see cref="ProductVariantOptionValueRecord"/>) into their
/// domain entities. Grouping/nesting (values under their type, option-value ids under their
/// variant) is assembled by <see cref="Repositories.SupabaseProductRepository.GetForEditAsync"/>,
/// which is the only caller that loads the full aggregate graph.
/// </summary>
internal static class ProductChildMapper
{
    public static ProductImage ToDomain(this ProductImageRecord record) =>
        ProductImage.Rehydrate(record.Id, record.ObjectKey, record.Position, record.IsPrimary);

    public static ProductOptionType ToDomain(
        this ProductOptionTypeRecord record, IReadOnlyList<ProductOptionValueRecord> values) =>
        ProductOptionType.Create(
            record.Id,
            record.Name,
            record.Position,
            [.. values
                .OrderBy(v => v.Position)
                .Select(v => new ProductOptionValueInput(v.Id, v.Value))]);

    public static ProductSpecification ToDomain(this ProductSpecificationRecord record) =>
        ProductSpecification.Create(record.Id, record.Name, record.Value, record.Position);

    public static ProductVariant ToDomain(this ProductVariantRecord record, IReadOnlySet<Guid> optionValueIds) =>
        ProductVariant.Create(
            record.Id,
            Sku.Create(record.Sku),
            record.OriginalPrice is decimal original
                ? ProductPricing.Create(
                    Money.Create(original), record.SalePrice is decimal sale ? Money.Create(sale) : null)
                : null,
            record.IsAvailable,
            record.ImageId,
            optionValueIds,
            record.Position);
}
