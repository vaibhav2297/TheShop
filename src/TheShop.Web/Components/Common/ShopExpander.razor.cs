using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;
using TheShop.Web.Theme;

namespace TheShop.Web.Components.Common;

/// <summary>Independent disclosure with a named heading, persistent body and optional two-way expanded state.</summary>
public partial class ShopExpander : ShopComponentBase
{
    /// <summary>Localized heading used when TitleContent is omitted.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Optional non-interactive phrasing content replacing the default heading text.</summary>
    [Parameter] public RenderFragment? TitleContent { get; set; }

    /// <summary>Optional non-interactive summary shown beside the heading.</summary>
    [Parameter] public RenderFragment? OverviewContent { get; set; }

    /// <summary>Body content; remains mounted but hidden and unfocusable while collapsed.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Initial or externally controlled expanded state; defaults to collapsed.</summary>
    [Parameter] public bool Expanded { get; set; }

    /// <summary>Raised after activation changes the expanded state; supports @bind-Expanded.</summary>
    [Parameter] public EventCallback<bool> ExpandedChanged { get; set; }

    /// <summary>Prevents user toggling without discarding body state.</summary>
    [Parameter] public bool Disabled { get; set; }

    private readonly string _headerId = $"shop-expander-header-{Guid.NewGuid():N}";
    private readonly string _contentId = $"shop-expander-content-{Guid.NewGuid():N}";
    private bool _initialized;
    private bool _lastExpanded;
    private bool _expanded;

    private string ClassName => ShopCssClass.Join("shop-native", "shop-expander",
        _expanded ? "shop-expander-expanded" : null, Class);
    private string ToggleIcon => _expanded ? ShopIcons.Outlined.Remove_Minus : ShopIcons.Outlined.Add_Plus;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (TitleContent is null && string.IsNullOrWhiteSpace(Title))
            throw new ArgumentException("An expander requires Title or non-interactive TitleContent.", nameof(Title));
        if (!_initialized || ExpandedChanged.HasDelegate || Expanded != _lastExpanded)
            _expanded = Expanded;
        _lastExpanded = Expanded;
        _initialized = true;
    }

    private async Task ToggleAsync()
    {
        if (Disabled)
            return;
        _expanded = !_expanded;
        await ExpandedChanged.InvokeAsync(_expanded);
    }
}
