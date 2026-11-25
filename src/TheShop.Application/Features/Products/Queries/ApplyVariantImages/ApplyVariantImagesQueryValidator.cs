using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.ApplyVariantImages;

/// <summary>Validates preview target identity and assignment uniqueness.</summary>
public sealed class ApplyVariantImagesQueryValidator : AbstractValidator<ApplyVariantImagesQuery>
{
    public ApplyVariantImagesQueryValidator()
    {
        RuleFor(q => q.TargetVariantIds).Must(ids => ids.Count > 0 &&
            ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage(ProductErrorKeys.VariantImageSelectionInvalid);
        RuleFor(q => q).Must(q => q.TargetVariantIds.All(id =>
            q.Variants.Any(v => v.Id == id)))
            .WithMessage(ProductErrorKeys.VariantImageSelectionInvalid);
        RuleFor(q => q.ImageIds).Must(ids => ids.All(id => id != Guid.Empty) &&
            ids.Distinct().Count() == ids.Count)
            .WithMessage(ProductErrorKeys.VariantImageDuplicate);
        RuleFor(q => q.RemovedImageIds).Must(ids => ids.All(id => id != Guid.Empty) &&
            ids.Distinct().Count() == ids.Count)
            .WithMessage(ProductErrorKeys.VariantImageSelectionInvalid);
    }
}
