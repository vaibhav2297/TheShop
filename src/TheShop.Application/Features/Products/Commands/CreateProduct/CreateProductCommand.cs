using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Creates a new product — details, gallery, option types, specification rows, and generated
/// variants — as one aggregate write (plan Section 3). <see cref="Sku"/> is required on every
/// save, complete or not (RULE-8, RULE-15).
/// </summary>
[RequiresPermission("products.create")]
public sealed record CreateProductCommand(
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
