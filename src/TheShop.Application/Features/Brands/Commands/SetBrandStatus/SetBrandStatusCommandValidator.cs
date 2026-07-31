using FluentValidation;

namespace TheShop.Application.Features.Brands.Commands.SetBrandStatus;

/// <summary>
/// Validates <see cref="SetBrandStatusCommand"/>: at least one id, no empty guids, no duplicates.
/// </summary>
public sealed class SetBrandStatusCommandValidator : AbstractValidator<SetBrandStatusCommand>
{
    public SetBrandStatusCommandValidator()
    {
        RuleFor(x => x.BrandIds)
            .NotEmpty().WithMessage(BrandErrorKeys.BrandIdsRequired);

        RuleForEach(x => x.BrandIds)
            .NotEmpty().WithMessage(BrandErrorKeys.BrandIdsRequired);

        RuleFor(x => x.BrandIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage(BrandErrorKeys.BrandIdsRequired);
    }
}
