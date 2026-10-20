using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Page-owned selection actions that fill their container and dock below the shell when scrolled past. Class, Style and unmatched attributes target the section.</summary>
public partial class ShopBulkActionBar : ShopComponentBase, IAsyncDisposable
{
    /// <summary>Whether a live selection should display the bar; hiding it removes its docking behavior.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Number of selected items; the page retains ownership of the selection.</summary>
    [Parameter, EditorRequired] public int SelectedCount { get; set; }

    /// <summary>Page-owned action controls, including their permission gates and busy state.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>Requests that the page clear its selection; the bar never changes Visible itself.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string _slotId = $"shop-bulk-actions-{Guid.NewGuid():N}";
    private Task<IJSObjectReference>? _moduleTask;
    private bool _disposed;
    private string ClassName => ShopCssClass.Join("shop-native", "shop-bulk-action-bar", Class);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || (!Visible && _moduleTask is null)) return;
        _moduleTask ??= JS.InvokeAsync<IJSObjectReference>("import", "./js/shopBulkActionBar.js").AsTask();
        var module = await _moduleTask;
        if (!_disposed) await module.InvokeVoidAsync("sync", _slotId);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_moduleTask is null) return;
            var module = await _moduleTask;
            await module.InvokeVoidAsync("dispose", _slotId);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }
}
