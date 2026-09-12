using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Renders an image with a named storefront treatment, configurable display width, and predictable scaling.
/// </summary>
public partial class ShopImage : MudComponentBase
{
    /// <summary>
    /// The source URL rendered by the image.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Src { get; set; }

    /// <summary>
    /// Localized alternate text describing the image.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Alt { get; set; }

    /// <summary>
    /// The source URL used when <see cref="Src"/> cannot be loaded.
    /// </summary>
    [Parameter]
    public string? FallbackSrc { get; set; }

    /// <summary>
    /// The preset that controls the image aspect ratio and fit behavior.
    /// </summary>
    [Parameter, EditorRequired]
    public ShopImageTreatment Treatment { get; set; }

    /// <summary>
    /// A valid CSS width applied to the rendered image. Defaults to the full available width.
    /// </summary>
    [Parameter]
    public string DisplayWidth { get; set; } = "100%";

    private string Classname => new CssBuilder("shop-image")
        .AddClass(TreatmentClass)
        .AddClass(Class)
        .Build();

    private string Stylename => new StyleBuilder()
        .AddStyle("width", DisplayWidth)
        .AddStyle(Style)
        .Build();

    private ObjectFit ImageFit => Treatment switch
    {
        ShopImageTreatment.CategoryTile or
        ShopImageTreatment.CategoryBanner or
        ShopImageTreatment.HeroDesktop or
        ShopImageTreatment.HeroMobile or
        ShopImageTreatment.Editorial => ObjectFit.Cover,
        _ => ObjectFit.Contain
    };

    private string TreatmentClass => Treatment switch
    {
        ShopImageTreatment.Product => "shop-image-product",
        ShopImageTreatment.Thumbnail => "shop-image-thumbnail",
        ShopImageTreatment.CategoryTile => "shop-image-category-tile",
        ShopImageTreatment.CategoryBanner => "shop-image-category-banner",
        ShopImageTreatment.HeroDesktop => "shop-image-hero-desktop",
        ShopImageTreatment.HeroMobile => "shop-image-hero-mobile",
        ShopImageTreatment.Editorial => "shop-image-editorial",
        ShopImageTreatment.BrandLogo => "shop-image-brand-logo",
        ShopImageTreatment.SocialSharing => "shop-image-social-sharing",
        _ => throw new ArgumentOutOfRangeException(nameof(Treatment), Treatment, "Unknown image treatment.")
    };
}
