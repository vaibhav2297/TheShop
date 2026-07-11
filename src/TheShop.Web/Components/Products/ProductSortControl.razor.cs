using Microsoft.AspNetCore.Components;
using MudBlazor;
using TheShop.Domain.Enums;

namespace TheShop.Web.Components.Products;

/// <summary>
/// Sort-order picker for the product catalogue. A thin <see cref="MudSelect{T}"/> wrapper
/// over the fixed <see cref="ProductSortOption"/> set — the page owns the current value
/// and re-queries when <see cref="SortChanged"/> fires.
/// </summary>
public partial class ProductSortControl : MudComponentBase
{
    /// <summary>The currently selected sort order.</summary>
    [Parameter, EditorRequired]
    public ProductSortOption Sort { get; set; }

    /// <summary>Raised when the user selects a different sort order.</summary>
    [Parameter]
    public EventCallback<ProductSortOption> SortChanged { get; set; }

    private Task OnValueChangedAsync(ProductSortOption value) => SortChanged.InvokeAsync(value);
}
