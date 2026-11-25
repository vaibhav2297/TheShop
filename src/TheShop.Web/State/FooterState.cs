namespace TheShop.Web.State;

/// <summary>
/// Scoped state store for the layout-mounted site footer. The footer is shown by
/// default; a page opts out by calling <see cref="Hide"/> in its initialization, and
/// <c>MainLayout</c> resets the footer to visible on every navigation so the opt-out
/// applies only to the page that asked for it.
/// </summary>
public sealed class FooterState
{
    /// <summary>
    /// Whether the footer is currently shown. <c>true</c> by default. The layout renders
    /// the footer slot only while this is <c>true</c>.
    /// </summary>
    public bool Visible { get; private set; } = true;

    /// <summary>
    /// Fires whenever the visibility changes. Subscribe to re-render components that read
    /// this store directly (typically <c>MainLayout</c>).
    /// </summary>
    public event Action? OnChange;

    /// <summary>Hides the footer for the current page.</summary>
    public void Hide()
    {
        if (!Visible) return;
        Visible = false;
        NotifyStateChanged();
    }

    /// <summary>Shows the footer (called by the layout on navigation to reset opt-outs).</summary>
    public void Show()
    {
        if (Visible) return;
        Visible = true;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
