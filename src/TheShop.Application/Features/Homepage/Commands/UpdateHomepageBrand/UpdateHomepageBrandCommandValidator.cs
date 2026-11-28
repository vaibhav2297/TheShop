using FluentValidation;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;

/// <summary>Validates the complete replacement brand section.</summary>
public sealed class UpdateHomepageBrandCommandValidator : AbstractValidator<UpdateHomepageBrandCommand>
{
    /// <summary>Creates the validator.</summary>
    public UpdateHomepageBrandCommandValidator() =>
        HomepageBrandInput.AddRules(this, x => x.Title, x => x.ButtonLabel, x => x.ButtonLink,
            x => x.BrandIds, x => x.IsVisible);
}
