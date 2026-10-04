using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>A semantic, templated table with optional parent-controlled, page-scoped selection. Styling and unmatched attributes target the table, not its scroll region.</summary>
/// <typeparam name="TItem">The caller-owned row data type.</typeparam>
public partial class ShopTable<TItem> : ShopComponentBase
{
    /// <summary>Rows on the current page; fetching, sorting and pagination remain caller-owned.</summary>
    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];

    /// <summary>Returns a unique, non-null, stable value-equality key for each displayed row.</summary>
    [Parameter, EditorRequired] public Func<TItem, object> ItemKey { get; set; } = default!;

    /// <summary>Resource-backed accessible name, rendered as a visually hidden caption and scroll-region label.</summary>
    [Parameter, EditorRequired] public string Caption { get; set; } = string.Empty;

    /// <summary>Native column headers, each with scope="col"; excludes the automatic selection column.</summary>
    [Parameter, EditorRequired] public RenderFragment HeaderContent { get; set; } = default!;

    /// <summary>Native data cells for one row; the component supplies the enclosing keyed tr.</summary>
    [Parameter, EditorRequired] public RenderFragment<TItem> RowTemplate { get; set; } = default!;

    /// <summary>Content rendered in one spanning cell when Items is empty.</summary>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <summary>Number of caller-supplied columns, excluding the optional selection column; must be positive.</summary>
    [Parameter, EditorRequired] public int ColumnCount { get; set; }

    /// <summary>Adds checkbox-only row selection and a current-page select-all checkbox.</summary>
    [Parameter] public bool Selectable { get; set; }

    /// <summary>Selected stable keys using normal object value equality; never mutated by the component.</summary>
    [Parameter] public IReadOnlySet<object> SelectedKeys { get; set; } = new HashSet<object>();

    /// <summary>Receives a fresh selection set after a user change; off-page keys are preserved.</summary>
    [Parameter] public EventCallback<IReadOnlySet<object>> SelectedKeysChanged { get; set; }

    /// <summary>Accessible label for each selection checkbox; required when Selectable is true.</summary>
    [Parameter] public Func<TItem, string>? RowSelectionLabel { get; set; }

    /// <summary>Prevents native and synthetic selection changes, typically supplied by BusyFor.</summary>
    [Parameter] public bool SelectionDisabled { get; set; }

    private List<RowEntry> _rows = [];
    private string ClassName => ShopCssClass.Join("shop-table", Class);
    private int EffectiveColumnCount => ColumnCount + (Selectable ? 1 : 0);
    private bool AllSelected => _rows.Count > 0 && _rows.All(row => IsSelected(row.Key));
    private bool PartiallySelected => !AllSelected && _rows.Any(row => IsSelected(row.Key));
    private bool IsSelected(object key) => SelectedKeys.Contains(key);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        ArgumentNullException.ThrowIfNull(ItemKey);
        ArgumentNullException.ThrowIfNull(HeaderContent);
        ArgumentNullException.ThrowIfNull(RowTemplate);
        ArgumentNullException.ThrowIfNull(SelectedKeys);
        ArgumentException.ThrowIfNullOrWhiteSpace(Caption);
        ArgumentOutOfRangeException.ThrowIfLessThan(ColumnCount, 1);
        if (Selectable)
            ArgumentNullException.ThrowIfNull(RowSelectionLabel);

        var keys = new HashSet<object>();
        var rows = new List<RowEntry>(Items.Count);
        foreach (var item in Items)
        {
            var key = ItemKey(item);
            if (key is null || !keys.Add(key))
                throw new ArgumentException("Table item keys must be non-null and unique.", nameof(ItemKey));
            if (Selectable)
                ArgumentException.ThrowIfNullOrWhiteSpace(RowSelectionLabel!(item));
            rows.Add(new(item, key, IsSelected(key)));
        }
        _rows = rows;
    }

    private Task SelectRowAsync(object key, bool selected)
    {
        if (!Selectable || SelectionDisabled || !_rows.Any(row => Equals(row.Key, key)))
            return Task.CompletedTask;
        var keys = SelectedKeys.ToHashSet();
        var changed = selected ? keys.Add(key) : keys.Remove(key);
        return changed ? SelectedKeysChanged.InvokeAsync(keys) : Task.CompletedTask;
    }

    private Task SelectPageAsync(bool selected)
    {
        if (!Selectable || SelectionDisabled || _rows.Count == 0)
            return Task.CompletedTask;
        var keys = SelectedKeys.ToHashSet();
        foreach (var row in _rows)
        {
            if (selected) keys.Add(row.Key);
            else keys.Remove(row.Key);
        }
        return keys.SetEquals(SelectedKeys) ? Task.CompletedTask : SelectedKeysChanged.InvokeAsync(keys);
    }

    private sealed record RowEntry(TItem Item, object Key, bool Selected);
}
