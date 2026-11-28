using FluentValidation;
using TheShop.Application.Common.Interfaces;

namespace TheShop.Application.Features.Homepage.Validation;

/// <summary>Shared command input rules and selected-brand availability check.</summary>
internal static class HomepageBrandInput
{
    internal static void AddRules<T>(AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> title,
        System.Linq.Expressions.Expression<Func<T, string>> buttonLabel,
        System.Linq.Expressions.Expression<Func<T, string>> buttonLink,
        System.Linq.Expressions.Expression<Func<T, IReadOnlyList<Guid>>> brandIds,
        System.Linq.Expressions.Expression<Func<T, bool>> isVisible)
    {
        var visible = isVisible.Compile();
        validator.RuleFor(title).NotEmpty().WithMessage(HomepageBrandErrorKeys.TitleRequired);
        validator.RuleFor(buttonLabel).NotEmpty().WithMessage(HomepageBrandErrorKeys.ButtonLabelRequired);
        validator.RuleFor(buttonLink).NotEmpty().WithMessage(HomepageBrandErrorKeys.ButtonLinkRequired)
            .Must(HeroBannerInput.IsDestination).WithMessage(HomepageBrandErrorKeys.ButtonLinkInvalid);
        validator.RuleFor(brandIds).Must((request, ids) => ids is not null && ids.Count <= 6 &&
            (!visible(request) || ids.Count >= 1) &&
            ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage(HomepageBrandErrorKeys.SelectionInvalid);
    }

    internal static async Task<bool> BrandsAvailableAsync(IBrandRepository repository,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        foreach (var id in ids)
        {
            var brand = await repository.GetByIdAsync(id, cancellationToken);
            if (brand is null || !brand.IsActive) return false;
        }
        return true;
    }
}
