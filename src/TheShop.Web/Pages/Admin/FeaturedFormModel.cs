using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Admin;

/// <summary>Local Featured draft; Application owns validation.</summary>
public sealed class FeaturedFormModel
{
    public string? Title { get; set; } = Strings.Homepage_Featured;
    public FeaturedTileFormModel Tile1 { get; set; } = new();
    public FeaturedTileFormModel Tile2 { get; set; } = new();
    public bool IsVisible { get; set; } = true;
}
