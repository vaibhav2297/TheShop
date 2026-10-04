namespace TheShop.Web.Common.UI;

/// <summary>A breadcrumb label and optional destination; disabled ancestors remain plain text.</summary>
public sealed record ShopBreadcrumbItem
{
    /// <summary>Creates a trail item. Supply a centralized route for navigable ancestors.</summary>
    public ShopBreadcrumbItem(string text, string? href = null, bool disabled = false)
    {
        Text = text;
        Href = href;
        Disabled = disabled;
    }

    /// <summary>Localized or data-derived label, rendered as encoded text.</summary>
    public string Text { get; }

    /// <summary>Destination for a navigable ancestor, or null for plain text.</summary>
    public string? Href { get; }

    /// <summary>Suppresses navigation even when a destination is supplied.</summary>
    public bool Disabled { get; }
}
