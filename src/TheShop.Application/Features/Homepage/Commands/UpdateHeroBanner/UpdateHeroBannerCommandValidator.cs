using FluentValidation;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHeroBanner;

/// <summary>Validates complete banner input and any supplied artwork before uploads.</summary>
public sealed class UpdateHeroBannerCommandValidator : AbstractValidator<UpdateHeroBannerCommand>
{
    /// <summary>Uses the actual-byte inspector for artwork validation.</summary>
    public UpdateHeroBannerCommandValidator(IHeroBannerImageInspector inspector)
    {
        RuleFor(x => x.Heading).NotEmpty().WithMessage(HeroBannerErrorKeys.HeadingRequired);
        RuleFor(x => x.ButtonLabel).NotEmpty().When(x => x.ButtonLink is not null)
            .WithMessage(HeroBannerErrorKeys.ButtonPairRequired);
        RuleFor(x => x.ButtonLink).NotEmpty().When(x => x.ButtonLabel is not null)
            .WithMessage(HeroBannerErrorKeys.ButtonPairRequired);
        RuleFor(x => x.ButtonLink).Must(HeroBannerInput.IsDestination)
            .WithMessage(HeroBannerErrorKeys.ButtonLinkInvalid);
        RuleFor(x => x.NewDesktopImage).Must((x, image) => !x.RemoveDesktopImage || image is not null)
            .WithMessage(HeroBannerErrorKeys.DesktopImageRequired);
        RuleFor(x => x.NewDesktopImage).CustomAsync(async (image, context, ct) =>
        {
            if (image is not null && await HeroBannerImageValidation.ErrorAsync(image, HeroBannerImageRole.Desktop, inspector, ct) is { } error)
                context.AddFailure(error);
        });
        RuleFor(x => x.NewMobileImage).CustomAsync(async (image, context, ct) =>
        {
            if (image is not null && await HeroBannerImageValidation.ErrorAsync(image, HeroBannerImageRole.Mobile, inspector, ct) is { } error)
                context.AddFailure(error);
        });
    }
}
