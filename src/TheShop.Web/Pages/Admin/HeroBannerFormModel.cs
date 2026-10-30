using TheShop.Web.Components.Common;

namespace TheShop.Web.Pages.Admin;

/// <summary>Local editing state only; Application validators own all input rules.</summary>
public sealed class HeroBannerFormModel
{
    public string? Heading { get; set; }
    public string? Description { get; set; }
    public string? ButtonLabel { get; set; }
    public string? ButtonLink { get; set; }
    public bool IsVisible { get; set; } = true;
    public IReadOnlyList<ShopUploadedImage> DesktopImage { get; set; } = [];
    public IReadOnlyList<ShopUploadedImage> MobileImage { get; set; } = [];
}

