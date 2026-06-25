using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Navigation;

/// <summary>
/// Layout-mounted breadcrumb strip. Wraps <see cref="MudBreadcrumbs"/> with the
/// project's chevron separator, per-item truncation, responsive collapse, and the
/// localized <c>aria-label</c> nav landmark. Pages never render this directly —
/// they push a trail into <see cref="State.BreadcrumbState"/> and <c>MainLayout</c>
/// renders this component in its breadcrumb slot.
/// </summary>
public partial class ShopBreadcrumbs : MudComponentBase
{
    /// <summary>The breadcrumb items to display.</summary>
    [Parameter]
    public IReadOnlyList<BreadcrumbItem>? Items { get; set; }

    // byte? matches MudBreadcrumbs.MaxItems exactly — null means no collapse limit.
    private byte? _maxItems;

    /// <summary>
    /// Drives responsive collapse. Sets <see cref="_maxItems"/> to 2 on Sm and
    /// below (middle levels collapse behind the expander), or <c>null</c> at Md
    /// and above (full trail).
    /// </summary>
    private void OnBreakpointChanged(Breakpoint breakpoint)
    {
        _maxItems = breakpoint is Breakpoint.Xs or Breakpoint.Sm
            ? 2
            : null;

        StateHasChanged();
    }

    private string Classname =>
        new CssBuilder("shop-breadcrumbs")
            .AddClass(Class)
            .Build();

    private string Stylename =>
        new StyleBuilder()
            .AddStyle(Style)
            .Build();
}
