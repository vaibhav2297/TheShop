namespace TheShop.Application.Features.Categories;

/// <summary>
/// String constants matching the resx keys in <c>Strings.resx</c> for category-related errors.
/// Kept here because the Application layer cannot reference the Web project's typed
/// <c>Strings</c> accessor. Keys are surfaced to the UI verbatim and resolved via
/// <c>Localizer[result.Error]</c>.
/// </summary>
public static class CategoryErrorKeys
{
    public const string NameRequired = "Category_NameRequired";
    public const string NameTooLong = "Category_NameTooLong";
    public const string DescriptionTooLong = "Category_DescriptionTooLong";
    public const string AlreadyExists = "Category_AlreadyExists";
    public const string ImageInvalidType = "Category_ImageInvalidType";
    public const string ImageTooLarge = "Category_ImageTooLarge";
    public const string CreateFailed = "Category_CreateFailed";
    public const string NotFound = "Category_NotFound";
    public const string UpdateFailed = "Category_UpdateFailed";
    public const string DeleteFailed = "Category_DeleteFailed";
    public const string StatusChangeFailed = "Category_StatusChangeFailed";
    public const string PageInvalid = "Category_PageInvalid";
    public const string CategoryIdsRequired = "Category_CategoryIdsRequired";
}
