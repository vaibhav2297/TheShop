using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using TheShop.Web.Common.Dialogs;

namespace TheShop.Web.Components.Common;

/// <summary>Renders one queued confirmation and cancels requests on navigation or layout teardown.</summary>
public partial class ShopDialogHost : ComponentBase, IDisposable
{
    [Inject] private ShopDialogService Dialogs { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    private bool _disposed;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Dialogs.AttachHost(this);
        Dialogs.Changed += OnChanged;
        Navigation.LocationChanged += OnLocationChanged;
    }

    private void OnChanged() => _ = InvokeAsync(() => { if (!_disposed) StateHasChanged(); });
    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (Dialogs.IsHost(this))
            Dialogs.CancelAll();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        Dialogs.Changed -= OnChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        Dialogs.DetachHost(this);
    }
}
