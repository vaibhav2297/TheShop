using FluentValidation;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Commands.UpdateFeatured;

/// <summary>Validates complete text and both tile candidates before any upload.</summary>
public sealed class UpdateFeaturedCommandValidator : AbstractValidator<UpdateFeaturedCommand>
{
    /// <summary>Uses the shared byte inspector with Featured image rules.</summary>
    public UpdateFeaturedCommandValidator(IHeroBannerImageInspector inspector)
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(FeaturedErrorKeys.SectionTitleRequired);
        RuleFor(x => x.Tile1).NotNull().WithMessage(FeaturedErrorKeys.TileTitleRequired)
            .SetValidator(new FeaturedTileInputValidator(inspector, false));
        RuleFor(x => x.Tile2).NotNull().WithMessage(FeaturedErrorKeys.TileTitleRequired)
            .SetValidator(new FeaturedTileInputValidator(inspector, false));
    }
}
