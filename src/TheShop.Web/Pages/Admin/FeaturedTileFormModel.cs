using TheShop.Web.Components.Common;

namespace TheShop.Web.Pages.Admin;

/// <summary>Local text and accepted preview state for a fixed Featured tile.</summary>
public sealed class FeaturedTileFormModel
{
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? ButtonLabel { get; set; }
    public string? ButtonLink { get; set; }
    public IReadOnlyList<ShopUploadedImage> Images { get; set; } = [];
}
