using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>Native bounded page navigation; callers own fetching, URL state and committed page selection.</summary>
public partial class ShopPagination : ShopComponentBase
{
    /// <summary>Current 1-based page; out-of-range values are clamped for presentation without firing callbacks.</summary>
    [Parameter, EditorRequired] public int Page { get; set; } = 1;

    /// <summary>Total pages available; zero or negative totals render no navigation.</summary>
    [Parameter, EditorRequired] public int TotalPages { get; set; } = 1;

    /// <summary>Raised only when an enabled, different, in-range page is requested.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>Prevents all page changes while the caller is busy.</summary>
    [Parameter] public bool Disabled { get; set; }

    private int CurrentPage => Math.Clamp(Page, 1, Math.Max(1, TotalPages));
    private string ClassName => ShopCssClass.Join("shop-native", "shop-pagination", Class);
    private static string PageLabel(int page) => string.Format(Strings.Pagination_Page, page);

    private IReadOnlyList<int?> PageItems
    {
        get
        {
            if (TotalPages <= 0)
                return [];
            long start = TotalPages <= 7 || CurrentPage <= 4 ? 1
                : CurrentPage >= TotalPages - 3 ? TotalPages - 4L : CurrentPage - 1L;
            long end = TotalPages <= 7 ? TotalPages : CurrentPage <= 4 ? 5
                : CurrentPage >= TotalPages - 3 ? TotalPages : CurrentPage + 1L;
            var pages = new SortedSet<int> { 1, TotalPages };
            for (var page = start; page <= end; page++)
                pages.Add((int)page);
            var items = new List<int?>();
            var previous = 0;
            foreach (var page in pages)
            {
                if (previous > 0 && page - previous > 1)
                    items.Add(null);
                items.Add(page);
                previous = page;
            }
            return items;
        }
    }

    private Task PreviousAsync() => SelectAsync(CurrentPage - 1);
    private Task NextAsync() => CurrentPage < TotalPages ? SelectAsync(CurrentPage + 1) : Task.CompletedTask;

    private Task SelectAsync(int page) =>
        Disabled || page < 1 || page > TotalPages || page == CurrentPage
            ? Task.CompletedTask : PageChanged.InvokeAsync(page);
}
