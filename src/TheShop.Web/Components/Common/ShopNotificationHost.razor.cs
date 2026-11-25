using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.Notifications;

namespace TheShop.Web.Components.Common;

/// <summary>Owns one polite live region; notifications remain in the scoped service across layout changes.</summary>
public partial class ShopNotificationHost : ComponentBase, IDisposable
{
    [Inject] private ShopNotificationService Notifications { get; set; } = default!;
    private bool _ready;
    private bool _disposed;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Notifications.AttachHost(this);
        Notifications.Changed += OnChanged;
    }

    /// <inheritdoc/>
    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;
        _ready = true;
        StateHasChanged();
    }

    private void OnChanged() => _ = InvokeAsync(() => { if (!_disposed) StateHasChanged(); });

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        Notifications.Changed -= OnChanged;
        Notifications.DetachHost(this);
    }
}
