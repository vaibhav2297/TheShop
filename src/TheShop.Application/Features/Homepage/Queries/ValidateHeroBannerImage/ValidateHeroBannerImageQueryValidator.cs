using FluentValidation;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Queries.ValidateHeroBannerImage;

/// <summary>Applies the same image rules as save commands.</summary>
public sealed class ValidateHeroBannerImageQueryValidator : AbstractValidator<ValidateHeroBannerImageQuery>
{
    /// <summary>Validates bytes and geometry using the registered inspector.</summary>
    public ValidateHeroBannerImageQueryValidator(IHeroBannerImageInspector inspector)
    {
        RuleFor(x => x.ImageRole).IsInEnum().WithMessage(HeroBannerErrorKeys.ImageUnreadable);
        RuleFor(x => x.Image).NotNull().WithMessage(HeroBannerErrorKeys.ImageUnreadable);
        RuleFor(x => x).CustomAsync(async (request, context, ct) =>
        {
            if (request.Image is not null && await HeroBannerImageValidation.ErrorAsync(request.Image, request.ImageRole, inspector, ct) is { } error)
                context.AddFailure(nameof(request.Image), error);
        });
    }
}

