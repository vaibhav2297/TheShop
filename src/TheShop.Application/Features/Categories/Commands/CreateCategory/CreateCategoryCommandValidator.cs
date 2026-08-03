using FluentValidation;

namespace TheShop.Application.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Validates <see cref="CreateCategoryCommand"/>: enforces name/description length limits and,
/// when an image is supplied, restricts it to an allowed content type and 2 MB.
/// </summary>
public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedImageContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(CategoryErrorKeys.NameRequired)
            .Must(name => name.Trim().Length <= MaxNameLength).WithMessage(CategoryErrorKeys.NameTooLong);

        RuleFor(x => x.Description)
            .Must(description => string.IsNullOrWhiteSpace(description) || description.Trim().Length <= MaxDescriptionLength)
            .WithMessage(CategoryErrorKeys.DescriptionTooLong);

        When(x => x.Image is not null, () =>
        {
            RuleFor(x => x.Image!.ContentType)
                .Must(contentType => AllowedImageContentTypes.Contains(contentType))
                .WithMessage(CategoryErrorKeys.ImageInvalidType);

            RuleFor(x => x.Image!.Content)
                .Must(content => content.Length <= MaxImageBytes)
                .WithMessage(CategoryErrorKeys.ImageTooLarge);
        });
    }
}
