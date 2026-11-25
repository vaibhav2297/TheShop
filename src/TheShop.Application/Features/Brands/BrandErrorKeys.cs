namespace TheShop.Application.Features.Brands;

/// <summary>
/// String constants matching the resx keys in <c>Strings.resx</c> for brand-related errors.
/// Kept here because the Application layer cannot reference the Web project's typed
/// <c>Strings</c> accessor. Keys are surfaced to the UI verbatim and resolved via
/// <c>Localizer[result.Error]</c>.
/// </summary>
public static class BrandErrorKeys
{
    public const string NameRequired = "Brand_NameRequired";
    public const string NameTooLong = "Brand_NameTooLong";
    public const string DescriptionTooLong = "Brand_DescriptionTooLong";
    public const string AlreadyExists = "Brand_AlreadyExists";
    public const string LogoInvalidType = "Brand_LogoInvalidType";
    public const string LogoTooLarge = "Brand_LogoTooLarge";
    public const string CreateFailed = "Brand_CreateFailed";
    public const string NotFound = "Brand_NotFound";
    public const string UpdateFailed = "Brand_UpdateFailed";
    public const string DeleteFailed = "Brand_DeleteFailed";
    public const string StatusChangeFailed = "Brand_StatusChangeFailed";
    public const string PageInvalid = "Brand_PageInvalid";
    public const string BrandIdsRequired = "Brand_BrandIdsRequired";
}
