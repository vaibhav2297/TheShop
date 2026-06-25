using MudBlazor;

namespace TheShop.Web.State;

/// <summary>
/// Scoped state store for the layout-mounted breadcrumb trail. Pages push their
/// trail by calling <see cref="Set"/>; the layout renders the trail while
/// <see cref="HasTrail"/> is <c>true</c> and clears it on every navigation.
/// </summary>
public sealed class BreadcrumbState
{
    /// <summary>
    /// The active breadcrumb items, or an empty list when no trail is set.
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Trail { get; private set; } = [];

    /// <summary>
    /// <c>true</c> when the trail contains more than the root item alone.
    /// The layout renders the breadcrumb slot only while this is <c>true</c>.
    /// </summary>
    public bool HasTrail => Trail.Count > 1;

    /// <summary>
    /// Fires whenever the trail changes. Subscribe to re-render components
    /// that read this store directly (typically <c>MainLayout</c>).
    /// </summary>
    public event Action? OnChange;

    /// <summary>Activates the breadcrumb trail with the supplied items.</summary>
    public void Set(IReadOnlyList<BreadcrumbItem> items)
    {
        Trail = items;
        NotifyStateChanged();
    }

    /// <summary>Clears the breadcrumb trail (called by the layout on navigation).</summary>
    public void Clear()
    {
        if (Trail.Count == 0) return;
        Trail = [];
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
