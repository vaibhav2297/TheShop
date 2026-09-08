using FluentValidation;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Validates <see cref="CreateProductCommand"/> per plan Section 9: name/description length,
/// SKU presence, category/brand presence, price/sale shape (product and every variant),
/// image type/size and one-primary, variant SKU distinctness, and option type/value
/// presence/uniqueness.
/// </summary>
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    private const int MaxNameLength = 150;
    private const int MaxDescriptionLength = 2000;
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedImageContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ProductErrorKeys.NameRequired)
            .Must(name => name.Trim().Length <= MaxNameLength).WithMessage(ProductErrorKeys.NameTooLong);

        RuleFor(x => x.Description)
            .Must(description => string.IsNullOrWhiteSpace(description) || description.Trim().Length <= MaxDescriptionLength)
            .WithMessage(ProductErrorKeys.DescriptionTooLong);

        RuleFor(x => x.Sku).NotEmpty().WithMessage(ProductErrorKeys.SkuRequired);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage(ProductErrorKeys.CategoryRequired);
        RuleFor(x => x.BrandId).NotEmpty().WithMessage(ProductErrorKeys.BrandRequired);

        When(x => x.OptionTypes.Count == 0, () =>
        {
            RuleFor(x => x.OriginalPrice).NotNull().WithMessage(ProductErrorKeys.PriceRequired);
        });

        When(x => x.OriginalPrice is not null, () =>
        {
            RuleFor(x => x.OriginalPrice!.Value)
                .GreaterThan(0).WithMessage(ProductErrorKeys.PriceInvalid)
                .Must(HasAtMostTwoDecimals)
                .WithMessage(ProductErrorKeys.PriceInvalid);
        });

        When(x => x.SalePrice is not null, () =>
        {
            RuleFor(x => x)
                .Must(x => x.OriginalPrice is not null && x.SalePrice!.Value > 0 && x.SalePrice.Value < x.OriginalPrice.Value)
                .WithMessage(ProductErrorKeys.SalePriceTooHigh);
        });

        RuleForEach(x => x.Gallery).ChildRules(entry =>
        {
            entry.When(e => e.Upload is not null, () =>
            {
                entry.RuleFor(e => e.Upload!.ContentType)
                    .Must(contentType => AllowedImageContentTypes.Contains(contentType))
                    .WithMessage(ProductErrorKeys.ImageInvalidType);

                entry.RuleFor(e => e.Upload!.Content.Length)
                    .Must(length => length <= MaxImageBytes)
                    .WithMessage(ProductErrorKeys.ImageTooLarge);
            });
        });

        RuleFor(x => x.Gallery)
            .Must(gallery => gallery.Count == 0 || gallery.Count(g => g.IsPrimary) == 1)
            .WithMessage(ProductErrorKeys.PrimaryImageRequired);

        RuleFor(x => x)
            .Must(x => HaveNoSkuDuplicates(x.Sku, x.Variants))
            .WithMessage(ProductErrorKeys.SkuDuplicatedInRequest);

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Sku).NotEmpty().WithMessage(ProductErrorKeys.SkuRequired);

            variant.When(v => v.OriginalPrice is not null, () =>
            {
                variant.RuleFor(v => v.OriginalPrice!.Value)
                    .GreaterThan(0).WithMessage(ProductErrorKeys.PriceInvalid)
                    .Must(HasAtMostTwoDecimals).WithMessage(ProductErrorKeys.PriceInvalid);
            });

            variant.When(v => v.SalePrice is not null, () =>
            {
                variant.RuleFor(v => v)
                    .Must(v => v.OriginalPrice is not null && v.SalePrice!.Value > 0 && v.SalePrice.Value < v.OriginalPrice.Value)
                    .WithMessage(ProductErrorKeys.SalePriceTooHigh);
            });
        });

        RuleForEach(x => x.OptionTypes).ChildRules(type =>
        {
            type.RuleFor(t => t.Name).NotEmpty().WithMessage(ProductErrorKeys.OptionNameRequired);
            type.RuleFor(t => t.Values).Must(values => values.Count > 0).WithMessage(ProductErrorKeys.OptionValueRequired);
            type.RuleForEach(t => t.Values)
                .Must(value => !string.IsNullOrWhiteSpace(value.Value))
                .WithMessage(ProductErrorKeys.OptionValueRequired);
            type.RuleFor(t => t.Values)
                .Must(values => values.Select(v => v.Value.Trim().ToLowerInvariant()).Distinct().Count() == values.Count)
                .WithMessage(ProductErrorKeys.OptionValueDuplicated);
        });

        RuleFor(x => x.OptionTypes)
            .Must(types => types.Select(t => t.Name.Trim().ToLowerInvariant()).Distinct().Count() == types.Count)
            .WithMessage(ProductErrorKeys.OptionNameDuplicated);
    }

    private static bool HasAtMostTwoDecimals(decimal value) => decimal.Round(value, 2) == value;

    private static bool HaveNoSkuDuplicates(string productSku, IReadOnlyList<VariantInput> variants)
    {
        var normalized = new[] { productSku }
            .Concat(variants.Select(v => v.Sku))
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToList();

        return normalized.Count == normalized.Distinct().Count();
    }
}
