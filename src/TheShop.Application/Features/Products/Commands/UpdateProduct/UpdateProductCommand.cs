using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Updates an existing product — details, gallery, option types, specification rows, and
/// generated variants — as one aggregate write. <see cref="RowVersion"/> is the opaque
/// concurrency token the edit form loaded with (Decision 11); a save since then fails with
/// <see cref="Features.Products.ProductErrorKeys.ModifiedElsewhere"/> rather than overwriting it.
/// </summary>
[RequiresPermission("products.edit")]
public sealed record UpdateProductCommand(
    Guid Id,
    string RowVersion,
    string Name,
    string? Description,
    string Sku,
    Guid CategoryId,
    Guid BrandId,
    decimal? OriginalPrice,
    decimal? SalePrice,
    bool IsPublished,
    IReadOnlyList<ProductGalleryEntry> Gallery,
    IReadOnlyList<OptionTypeInput> OptionTypes,
    IReadOnlyList<SpecificationInput> Specifications,
    IReadOnlyList<VariantInput> Variants) : IRequest<Result<AdminProductDto>>;
