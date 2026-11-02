using FluentValidation;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;

/// <summary>Applies the same image checks used before saving.</summary>
public sealed class ValidateFeaturedImageQueryValidator : AbstractValidator<ValidateFeaturedImageQuery>
{
    /// <summary>Uses detected bytes and decoded dimensions.</summary>
    public ValidateFeaturedImageQueryValidator(IHeroBannerImageInspector inspector)
    {
        RuleFor(x => x.Image).NotNull().WithMessage(FeaturedErrorKeys.ImageRequired);
        RuleFor(x => x.Image).CustomAsync(async (image, context, ct) =>
        {
            if (image is not null && await FeaturedImageValidation.ErrorAsync(image, inspector, ct) is { } error)
                context.AddFailure(error);
        });
    }
}
