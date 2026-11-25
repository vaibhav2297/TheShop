using FluentValidation;

namespace TheShop.Application.Features.Brands.Commands.CreateBrand;

/// <summary>
/// Validates <see cref="CreateBrandCommand"/>: enforces name/description length limits and,
/// when a logo is supplied, restricts it to an allowed content type and 2 MB.
/// </summary>
public sealed class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedLogoContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public CreateBrandCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(BrandErrorKeys.NameRequired)
            .Must(name => name.Trim().Length <= MaxNameLength).WithMessage(BrandErrorKeys.NameTooLong);

        RuleFor(x => x.Description)
            .Must(description => string.IsNullOrWhiteSpace(description) || description.Trim().Length <= MaxDescriptionLength)
            .WithMessage(BrandErrorKeys.DescriptionTooLong);

        When(x => x.Logo is not null, () =>
        {
            RuleFor(x => x.Logo!.ContentType)
                .Must(contentType => AllowedLogoContentTypes.Contains(contentType))
                .WithMessage(BrandErrorKeys.LogoInvalidType);

            RuleFor(x => x.Logo!.Content)
                .Must(content => content.Length <= MaxLogoBytes)
                .WithMessage(BrandErrorKeys.LogoTooLarge);
        });
    }
}
