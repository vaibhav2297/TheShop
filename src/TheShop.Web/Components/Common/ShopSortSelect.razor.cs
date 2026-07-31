using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Sort-order picker. A thin <see cref="MudSelect{T}"/> wrapper generic over a feature's own sort
/// enum (<c>ProductSortOption</c>, <c>BrandSortOption</c>, …) — the page owns the current value,
/// supplies the fixed <see cref="Options"/> list, and re-queries when <see cref="SortChanged"/>
/// fires. Feature-agnostic — used by both the product catalogue and manage-brands (plan §5
/// Decision 4); the sort enums themselves stay per-feature and compile-time-checked.
/// </summary>
/// <typeparam name="TSort">The feature's sort-option enum.</typeparam>
public partial class ShopSortSelect<TSort> : MudComponentBase where TSort : struct, Enum
{
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    /// <summary>The currently selected sort order.</summary>
    [Parameter, EditorRequired]
    public TSort Sort { get; set; }

    /// <summary>Raised when the user selects a different sort order.</summary>
    [Parameter]
    public EventCallback<TSort> SortChanged { get; set; }

    /// <summary>
    /// The fixed, ordered set of selectable sort options, each paired with the resource key its
    /// label is resolved from. Supply a feature's <c>SortCatalogue.Picker</c>.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<(TSort Value, string LabelKey)> Options { get; set; } = [];

    private Task OnValueChangedAsync(TSort value) => SortChanged.InvokeAsync(value);
}
