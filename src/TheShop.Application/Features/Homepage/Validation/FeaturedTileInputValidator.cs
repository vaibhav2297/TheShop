using FluentValidation;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;

namespace TheShop.Application.Features.Homepage.Validation;

internal sealed class FeaturedTileInputValidator : AbstractValidator<FeaturedTileInput>
{
    internal FeaturedTileInputValidator(IHeroBannerImageInspector inspector, bool create)
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(FeaturedErrorKeys.TileTitleRequired);
        RuleFor(x => x.ButtonLabel).NotEmpty().When(x => x.ButtonLink is not null)
            .WithMessage(FeaturedErrorKeys.ButtonPairRequired);
        RuleFor(x => x.ButtonLink).NotEmpty().When(x => x.ButtonLabel is not null)
            .WithMessage(FeaturedErrorKeys.ButtonPairRequired);
        RuleFor(x => x.ButtonLink).Must(HeroBannerInput.IsDestination)
            .WithMessage(FeaturedErrorKeys.ButtonLinkInvalid);
        RuleFor(x => x.NewImage).Must((tile, image) => !(create || tile.RemoveImage) || image is not null)
            .WithMessage(FeaturedErrorKeys.ImageRequired);
        RuleFor(x => x.NewImage).CustomAsync(async (image, context, ct) =>
        {
            if (image is not null && await FeaturedImageValidation.ErrorAsync(image, inspector, ct) is { } error)
                context.AddFailure(error);
        });
    }
}
