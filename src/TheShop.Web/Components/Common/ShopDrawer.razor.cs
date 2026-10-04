using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Controlled right-side modal drawer with fixed header/actions and a scrollable body.</summary>
public partial class ShopDrawer : ShopComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<ShopDrawer> Logger { get; set; } = default!;

    /// <summary>Whether the drawer should be open. Keep the component mounted to animate closing.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>Requests an updated open state on Escape, close-button, backdrop, or modal replacement.</summary>
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Localized heading markup supplying the accessible name; the drawer owns the close button.</summary>
    [Parameter, EditorRequired] public RenderFragment? HeaderContent { get; set; }

    /// <summary>Feature-owned content; this is the only scrollable section.</summary>
    [Parameter] public RenderFragment? DrawerContent { get; set; }

    /// <summary>Optional fixed actions. An omitted fragment renders no footer or reserved space.</summary>
    [Parameter] public RenderFragment? ActionContent { get; set; }

    private readonly string _titleId = $"shop-drawer-title-{Guid.NewGuid():N}";
    private ElementReference _element;
    private Task<IJSObjectReference>? _moduleTask;
    private DotNetObjectReference<ShopDrawer>? _reference;
    private bool _disposed;
    private bool? _synchronizedOpen;
    private string ClassName => ShopCssClass.Join("shop-native", "shop-drawer", Class);
    private IReadOnlyDictionary<string, object>? RootAttributes => AdditionalAttributes?
        .Where(pair => !string.Equals(pair.Key, "open", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _synchronizedOpen == Open || (_moduleTask is null && !Open)) return;
        try
        {
            _moduleTask ??= JS.InvokeAsync<IJSObjectReference>("import", "./js/shopDialog.js").AsTask();
            var module = await _moduleTask;
            if (_disposed) return;
            _reference ??= DotNetObjectReference.Create(this);
            var open = Open;
            _synchronizedOpen = open;
            await module.InvokeVoidAsync("setOpen", _element, _reference, open);
        }
        catch (JSException exception)
        {
            _synchronizedOpen = null;
            Logger.LogError(exception, "The native drawer could not be synchronized.");
            if (!_disposed && Open) await OpenChanged.InvokeAsync(false);
        }
    }

    /// <summary>Requests closure without mutating the caller-owned state.</summary>
    [JSInvokable]
    public Task DismissAsync() => _disposed || !Open ? Task.CompletedTask : OpenChanged.InvokeAsync(false);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_moduleTask is not null)
            {
                var module = await _moduleTask;
                await module.InvokeVoidAsync("dispose", _element);
                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception) { Logger.LogDebug(exception, "The drawer was already unavailable during disposal."); }
        catch (ObjectDisposedException) { }
        finally { _reference?.Dispose(); }
    }
}
