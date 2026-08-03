using FluentValidation;

namespace TheShop.Application.Features.Categories.Commands.SetCategoryStatus;

/// <summary>
/// Validates <see cref="SetCategoryStatusCommand"/>: at least one id, no empty guids, no
/// duplicates.
/// </summary>
public sealed class SetCategoryStatusCommandValidator : AbstractValidator<SetCategoryStatusCommand>
{
    public SetCategoryStatusCommandValidator()
    {
        RuleFor(x => x.CategoryIds)
            .NotEmpty().WithMessage(CategoryErrorKeys.CategoryIdsRequired);

        RuleForEach(x => x.CategoryIds)
            .NotEmpty().WithMessage(CategoryErrorKeys.CategoryIdsRequired);

        RuleFor(x => x.CategoryIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage(CategoryErrorKeys.CategoryIdsRequired);
    }
}
