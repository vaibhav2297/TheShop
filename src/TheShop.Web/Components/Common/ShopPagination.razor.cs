using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable pagination control. Wraps <see cref="MudPagination"/> behind a minimal
/// page/total-pages contract so any paged feature can drop it in without re-implementing
/// paging math or metadata — bind <see cref="Page"/> and <see cref="TotalPages"/> straight
/// from a <c>PagedResult&lt;T&gt;</c>.
/// </summary>
public partial class ShopPagination : MudComponentBase
{
    /// <summary>The currently selected 1-based page number.</summary>
    [Parameter, EditorRequired]
    public int Page { get; set; } = 1;

    /// <summary>The total number of pages available.</summary>
    [Parameter, EditorRequired]
    public int TotalPages { get; set; } = 1;

    /// <summary>Raised when the user selects a different page.</summary>
    [Parameter]
    public EventCallback<int> PageChanged { get; set; }

    private Task OnSelectedChangedAsync(int page) => PageChanged.InvokeAsync(page);
}
