using FluentValidation;

namespace TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;

/// <summary>Validates the complete new section.</summary>
public sealed class CreateHomepageCategoryCommandValidator : AbstractValidator<CreateHomepageCategoryCommand>
{
    /// <summary>Creates the validator.</summary>
    public CreateHomepageCategoryCommandValidator() =>
        HomepageCategoryInput.AddRules(this, x => x.Title, x => x.ButtonLabel, x => x.ButtonLink, x => x.CategoryIds);
}
