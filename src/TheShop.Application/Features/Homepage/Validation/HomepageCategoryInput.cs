using FluentValidation;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage;

/// <summary>Shared input rules and selected-category availability check.</summary>
internal static class HomepageCategoryInput
{
    internal static void AddRules<T>(AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> title,
        System.Linq.Expressions.Expression<Func<T, string>> buttonLabel,
        System.Linq.Expressions.Expression<Func<T, string>> buttonLink,
        System.Linq.Expressions.Expression<Func<T, IReadOnlyList<Guid>>> categoryIds)
    {
        validator.RuleFor(title).NotEmpty().WithMessage(HomepageCategoryErrorKeys.TitleRequired);
        validator.RuleFor(buttonLabel).NotEmpty().WithMessage(HomepageCategoryErrorKeys.ButtonLabelRequired);
        validator.RuleFor(buttonLink).NotEmpty().WithMessage(HomepageCategoryErrorKeys.ButtonLinkRequired)
            .Must(HeroBannerInput.IsDestination).WithMessage(HomepageCategoryErrorKeys.ButtonLinkInvalid);
        validator.RuleFor(categoryIds).Must(ids => ids is { Count: 3 } &&
            ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == 3)
            .WithMessage(HomepageCategoryErrorKeys.SelectionInvalid);
    }

    internal static async Task<bool> CategoriesAvailableAsync(ICategoryRepository repository,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        foreach (var id in ids)
        {
            var category = await repository.GetByIdAsync(id, cancellationToken);
            if (category is null || !category.IsActive) return false;
        }
        return true;
    }
}
