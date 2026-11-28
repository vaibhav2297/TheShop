using FluentValidation;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Commands.CreateHomepageBrand;

/// <summary>Validates the complete new brand section.</summary>
public sealed class CreateHomepageBrandCommandValidator : AbstractValidator<CreateHomepageBrandCommand>
{
    /// <summary>Creates the validator.</summary>
    public CreateHomepageBrandCommandValidator() =>
        HomepageBrandInput.AddRules(this, x => x.Title, x => x.ButtonLabel, x => x.ButtonLink,
            x => x.BrandIds, x => x.IsVisible);
}
