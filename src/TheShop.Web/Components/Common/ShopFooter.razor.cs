using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Layout-mounted site footer. Renders the brand column (logo, tagline, social links) and
/// the data-driven link sections from <see cref="Common.FooterContent"/> on a tertiary
/// band. Pages never render this directly — <c>MainLayout</c> shows it in its footer slot
/// while <see cref="State.FooterState.Visible"/> is <c>true</c>.
/// </summary>
public partial class ShopFooter : MudComponentBase
{
    private string Classname =>
        new CssBuilder("shop-footer")
            .AddClass("mud-theme-tertiary")
            .AddClass("pa-8")
            .AddClass(Class)
            .Build();

    private string Stylename =>
        new StyleBuilder()
            .AddStyle(Style)
            .Build();
}
