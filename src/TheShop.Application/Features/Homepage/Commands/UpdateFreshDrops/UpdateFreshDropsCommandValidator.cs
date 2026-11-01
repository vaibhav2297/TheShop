using FluentValidation;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops;

/// <summary>Validates Fresh Drops fields before an update handler runs.</summary>
public sealed class UpdateFreshDropsCommandValidator : AbstractValidator<UpdateFreshDropsCommand>
{
    /// <summary>Creates the validator.</summary>
    public UpdateFreshDropsCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(FreshDropsErrorKeys.TitleRequired);
        RuleFor(x => x.ButtonLabel).NotEmpty().When(x => x.ButtonLink is not null)
            .WithMessage(FreshDropsErrorKeys.ButtonPairRequired);
        RuleFor(x => x.ButtonLink).NotEmpty().When(x => x.ButtonLabel is not null)
            .WithMessage(FreshDropsErrorKeys.ButtonPairRequired);
        RuleFor(x => x.ButtonLink).Must(HeroBannerInput.IsDestination)
            .WithMessage(FreshDropsErrorKeys.ButtonLinkInvalid);
        RuleFor(x => x.SelectionMode).IsInEnum().WithMessage(FreshDropsErrorKeys.SelectionInvalid);
        RuleFor(x => x.ManualProductIds).Must(ids => ids is not null && ids.Count <= 3 &&
            ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage(FreshDropsErrorKeys.SelectionInvalid);
    }
}
