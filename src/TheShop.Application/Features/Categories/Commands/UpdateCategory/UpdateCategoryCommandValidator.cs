using FluentValidation;

namespace TheShop.Application.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// Validates <see cref="UpdateCategoryCommand"/>: enforces name/description length limits and,
/// when a replacement image is supplied, restricts it to an allowed content type and 2 MB.
/// </summary>
public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedImageContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(CategoryErrorKeys.NameRequired)
            .Must(name => name.Trim().Length <= MaxNameLength).WithMessage(CategoryErrorKeys.NameTooLong);

        RuleFor(x => x.Description)
            .Must(description => string.IsNullOrWhiteSpace(description) || description.Trim().Length <= MaxDescriptionLength)
            .WithMessage(CategoryErrorKeys.DescriptionTooLong);

        When(x => !x.RemoveImage && x.NewImage is not null, () =>
        {
            RuleFor(x => x.NewImage!.ContentType)
                .Must(contentType => AllowedImageContentTypes.Contains(contentType))
                .WithMessage(CategoryErrorKeys.ImageInvalidType);

            RuleFor(x => x.NewImage!.Content)
                .Must(content => content.Length <= MaxImageBytes)
                .WithMessage(CategoryErrorKeys.ImageTooLarge);
        });
    }
}
