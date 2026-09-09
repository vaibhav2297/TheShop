using FluentValidation;

namespace TheShop.Application.Features.Products.Commands.SetProductStatus;

/// <summary>
/// Validates <see cref="SetProductStatusCommand"/>: at least one id, no empty guids, no duplicates.
/// </summary>
public sealed class SetProductStatusCommandValidator : AbstractValidator<SetProductStatusCommand>
{
    public SetProductStatusCommandValidator()
    {
        RuleFor(x => x.ProductIds)
            .NotEmpty().WithMessage(ProductErrorKeys.ProductIdsRequired);

        RuleForEach(x => x.ProductIds)
            .NotEmpty().WithMessage(ProductErrorKeys.ProductIdsRequired);

        RuleFor(x => x.ProductIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage(ProductErrorKeys.ProductIdsRequired);
    }
}
