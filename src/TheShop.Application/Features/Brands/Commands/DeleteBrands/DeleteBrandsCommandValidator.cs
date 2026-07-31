using FluentValidation;

namespace TheShop.Application.Features.Brands.Commands.DeleteBrands;

/// <summary>
/// Validates <see cref="DeleteBrandsCommand"/>: at least one id, no empty guids, no duplicates.
/// </summary>
public sealed class DeleteBrandsCommandValidator : AbstractValidator<DeleteBrandsCommand>
{
    public DeleteBrandsCommandValidator()
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
