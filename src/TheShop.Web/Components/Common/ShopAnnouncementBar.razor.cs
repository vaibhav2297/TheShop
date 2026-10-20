using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Site-wide announcement bar shown above the <see cref="ShopAppBar"/>. Renders the
/// dynamic, server-provided announcement <see cref="Message"/>. Visibility is owned by
/// the layout, which only renders this bar while an announcement is active.
/// </summary>
public partial class ShopAnnouncementBar : ShopComponentBase
{
    /// <summary>The announcement text to display.</summary>
    [Parameter, EditorRequired]
    public string? Message { get; set; }

    private string ClassName => ShopCssClass.Join("shop-native", "shop-announcement-bar", Class);
}
