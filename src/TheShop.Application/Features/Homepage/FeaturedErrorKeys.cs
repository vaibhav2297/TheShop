using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage;

/// <summary>Resource keys for expected Featured failures.</summary>
public static class FeaturedErrorKeys
{
    public const string SectionTitleRequired = FeaturedSectionTitleRequiredException.MessageResourceKey;
    public const string TileTitleRequired = FeaturedTileTitleRequiredException.MessageResourceKey;
    public const string ImageRequired = FeaturedImageRequiredException.MessageResourceKey;
    public const string ButtonPairRequired = FeaturedButtonPairRequiredException.MessageResourceKey;
    public const string ButtonLinkInvalid = "Featured_ButtonLinkInvalid";
    public const string ImageInvalidType = "Featured_ImageInvalidType";
    public const string ImageTooLarge = "Featured_ImageTooLarge";
    public const string ImageUnreadable = "Featured_ImageUnreadable";
    public const string ImageDimensionsInvalid = "Featured_ImageDimensionsInvalid";
    public const string AlreadyExists = "Featured_AlreadyExists";
    public const string NotFound = "Featured_NotFound";
    public const string ImageUnavailable = "Featured_ImageUnavailable";
    public const string LoadFailed = "Featured_LoadFailed";
    public const string UploadFailed = "Featured_UploadFailed";
    public const string SaveFailed = "Featured_SaveFailed";
    public const string SaveUnconfirmed = "Featured_SaveUnconfirmed";
}
