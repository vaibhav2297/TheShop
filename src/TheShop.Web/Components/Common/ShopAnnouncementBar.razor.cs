using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Site-wide announcement bar shown above the <see cref="ShopAppBar"/>. Renders the
/// dynamic, server-provided announcement <see cref="Message"/>. Visibility is owned by
/// the layout, which only renders this bar while an announcement is active.
/// </summary>
public partial class ShopAnnouncementBar : MudComponentBase
{
    /// <summary>The announcement text to display.</summary>
    [Parameter, EditorRequired]
    public string? Message { get; set; }

    private string Classname => new CssBuilder("mud-theme-dark")
        .AddClass(Class)
        .Build();
}
