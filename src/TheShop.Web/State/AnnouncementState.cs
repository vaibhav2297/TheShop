namespace TheShop.Web.State;

/// <summary>
/// Client-side state store for the site-wide announcement bar. Holds the active
/// announcement <b>message text</b> as supplied by the server (dynamic content, not a
/// localized UI string), or <c>null</c> when none is active. The layout renders the
/// announcement bar only while <see cref="HasAnnouncement"/> is <c>true</c>, so the
/// header re-flows automatically as announcements come and go.
/// </summary>
public sealed class AnnouncementState
{
    /// <summary>
    /// The active announcement text exactly as provided by the server, or <c>null</c>
    /// when no announcement is active.
    /// </summary>
    public string? Message { get; private set; }

    /// <summary>
    /// Whether an announcement is currently active and should be shown.
    /// </summary>
    public bool HasAnnouncement => !string.IsNullOrWhiteSpace(Message);

    /// <summary>
    /// Fires whenever the active announcement changes. Subscribe to re-render
    /// components that read this store directly.
    /// </summary>
    public event Action? OnChange;

    /// <summary>Activates the announcement bar with the server-provided message text.</summary>
    public void Set(string? message)
    {
        Message = message;
        NotifyStateChanged();
    }

    /// <summary>Hides the announcement bar.</summary>
    public void Clear()
    {
        Message = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
