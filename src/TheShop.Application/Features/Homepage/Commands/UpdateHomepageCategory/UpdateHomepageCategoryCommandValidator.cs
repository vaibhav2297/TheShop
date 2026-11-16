using FluentValidation;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;

/// <summary>Validates the complete replacement section.</summary>
public sealed class UpdateHomepageCategoryCommandValidator : AbstractValidator<UpdateHomepageCategoryCommand>
{
    /// <summary>Creates the validator.</summary>
    public UpdateHomepageCategoryCommandValidator() =>
        HomepageCategoryInput.AddRules(this, x => x.Title, x => x.ButtonLabel, x => x.ButtonLink, x => x.CategoryIds);
}
