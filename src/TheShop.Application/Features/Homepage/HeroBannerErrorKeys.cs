using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage;

/// <summary>Resource keys for banner administration failures.</summary>
public static class HeroBannerErrorKeys
{
    public const string HeadingRequired = HeroBannerHeadingRequiredException.MessageResourceKey;
    public const string DesktopImageRequired = HeroBannerDesktopImageRequiredException.MessageResourceKey;
    public const string ButtonPairRequired = HeroBannerButtonPairRequiredException.MessageResourceKey;
    public const string ButtonLinkInvalid = "HeroBanner_ButtonLinkInvalid";
    public const string ImageInvalidType = "HeroBanner_ImageInvalidType";
    public const string ImageTooLarge = "HeroBanner_ImageTooLarge";
    public const string ImageUnreadable = "HeroBanner_ImageUnreadable";
    public const string DesktopDimensionsInvalid = "HeroBanner_DesktopDimensionsInvalid";
    public const string MobileDimensionsInvalid = "HeroBanner_MobileDimensionsInvalid";
    public const string AlreadyExists = "HeroBanner_AlreadyExists";
    public const string NotFound = "HeroBanner_NotFound";
    public const string ImageUnavailable = "HeroBanner_ImageUnavailable";
    public const string LoadFailed = "HeroBanner_LoadFailed";
    public const string UploadFailed = "HeroBanner_UploadFailed";
    public const string SaveFailed = "HeroBanner_SaveFailed";
    public const string SaveUnconfirmed = "HeroBanner_SaveUnconfirmed";
}

