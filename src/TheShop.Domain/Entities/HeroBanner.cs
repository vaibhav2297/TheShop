using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>The singleton homepage banner configuration managed by staff.</summary>
public sealed class HeroBanner
{
    /// <summary>The only valid banner identity.</summary>
    public short Id => 1;
    public string Heading { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ButtonLabel { get; private set; }
    public string? ButtonLink { get; private set; }
    public string DesktopImageKey { get; private set; } = string.Empty;
    public string? MobileImageKey { get; private set; }
    public bool IsVisible { get; private set; }

    private HeroBanner() { }

    /// <summary>Creates a banner with required heading, desktop artwork and paired button content.</summary>
    /// <exception cref="HeroBannerHeadingRequiredException">Heading is blank.</exception>
    /// <exception cref="HeroBannerDesktopImageRequiredException">Desktop artwork is blank.</exception>
    /// <exception cref="HeroBannerButtonPairRequiredException">Only one button field is supplied.</exception>
    public static HeroBanner Create(string heading, string? description, string? buttonLabel,
        string? buttonLink, string desktopImageKey, string? mobileImageKey, bool isVisible)
    {
        var banner = new HeroBanner();
        banner.ChangeContent(heading, description, buttonLabel, buttonLink);
        banner.ChangeImages(desktopImageKey, mobileImageKey);
        banner.SetVisibility(isVisible);
        return banner;
    }

    /// <summary>Restores the complete configuration from a validated persisted row.</summary>
    public static HeroBanner Rehydrate(string heading, string? description, string? buttonLabel,
        string? buttonLink, string desktopImageKey, string? mobileImageKey, bool isVisible) =>
        Create(heading, description, buttonLabel, buttonLink, desktopImageKey, mobileImageKey, isVisible);

    /// <summary>Changes text atomically, preserving the required heading and button pair.</summary>
    /// <exception cref="HeroBannerHeadingRequiredException">Heading is blank.</exception>
    /// <exception cref="HeroBannerButtonPairRequiredException">Only one button field is supplied.</exception>
    public void ChangeContent(string heading, string? description, string? buttonLabel, string? buttonLink)
    {
        if (string.IsNullOrWhiteSpace(heading)) throw new HeroBannerHeadingRequiredException();
        if (string.IsNullOrWhiteSpace(buttonLabel) != string.IsNullOrWhiteSpace(buttonLink))
            throw new HeroBannerButtonPairRequiredException();
        Heading = heading;
        Description = description;
        ButtonLabel = buttonLabel;
        ButtonLink = buttonLink;
    }

    /// <summary>Replaces artwork keys while preserving required desktop artwork.</summary>
    /// <exception cref="HeroBannerDesktopImageRequiredException">Desktop artwork is blank.</exception>
    public void ChangeImages(string desktopImageKey, string? mobileImageKey)
    {
        if (string.IsNullOrWhiteSpace(desktopImageKey)) throw new HeroBannerDesktopImageRequiredException();
        DesktopImageKey = desktopImageKey;
        MobileImageKey = mobileImageKey;
    }

    /// <summary>Sets the saved visibility without removing the configuration.</summary>
    public void SetVisibility(bool isVisible) => IsVisible = isVisible;
}
