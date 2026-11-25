using FluentValidation;

namespace TheShop.Application.Features.Categories.Commands.DeleteCategories;

/// <summary>
/// Validates <see cref="DeleteCategoriesCommand"/>: at least one id, no empty guids, no
/// duplicates.
/// </summary>
public sealed class DeleteCategoriesCommandValidator : AbstractValidator<DeleteCategoriesCommand>
{
    public DeleteCategoriesCommandValidator()
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
