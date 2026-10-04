using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Native breadcrumb landmark with CSS-responsive ancestor disclosure and a non-link current page.</summary>
public partial class ShopBreadcrumbs : ShopComponentBase
{
    /// <summary>Ordered trail; the final item is current. Class, Style and attributes target the nav landmark.</summary>
    [Parameter] public IReadOnlyList<ShopBreadcrumbItem>? Items { get; set; }

    private readonly string _listId = $"shop-breadcrumbs-{Guid.NewGuid():N}";
    private IReadOnlyList<ShopBreadcrumbItem> _items = [];
    private bool _expanded;
    private bool _focusExpandedAncestor;
    private readonly Dictionary<int, ElementReference> _itemElements = [];

    private string ClassName => ShopCssClass.Join("shop-native", "shop-breadcrumbs",
        _expanded ? "shop-breadcrumbs-expanded" : null, Class);

    private string ItemClass(int index) => ShopCssClass.Join("shop-breadcrumb-entry",
        index > 0 && index < _items.Count - 1 ? "shop-breadcrumb-middle" : null);

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        var items = Items ?? [];
        if (!_items.SequenceEqual(items))
        {
            _expanded = false;
            _focusExpandedAncestor = false;
        }
        _items = items.ToArray();
    }

    private void Expand()
    {
        _expanded = true;
        _focusExpandedAncestor = true;
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusExpandedAncestor) return;
        _focusExpandedAncestor = false;
        await _itemElements[1].FocusAsync(preventScroll: true);
    }
}
