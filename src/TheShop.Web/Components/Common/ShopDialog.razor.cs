using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Native modal chrome with browser-managed inertness, focus, and dismissal.</summary>
public partial class ShopDialog : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<ShopDialog> Logger { get; set; } = default!;

    /// <summary>Already-localized title markup, including an appropriate heading; supplies the dialog's accessible name.</summary>
    [Parameter, EditorRequired] public RenderFragment? TitleContent { get; set; }

    /// <summary>Optional width cap. Omitted, null, or None fits content within viewport gutters.</summary>
    [Parameter] public ShopMaxWidth? MaxWidth { get; set; } = ShopMaxWidth.None;

    /// <summary>ID of the concise description in the body, when available.</summary>
    [Parameter] public string? DescriptionId { get; set; }

    /// <summary>Feature-owned body content.</summary>
    [Parameter] public RenderFragment? DialogContent { get; set; }

    /// <summary>Action buttons; the safe initial action carries data-dialog-initial-focus.</summary>
    [Parameter] public RenderFragment? DialogActions { get; set; }

    /// <summary>Raised on close, Escape, backdrop dismissal, or failure to initialize the browser modal.</summary>
    [Parameter] public EventCallback OnDismiss { get; set; }

    private readonly string _titleId = $"shop-dialog-title-{Guid.NewGuid():N}";
    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ShopDialog>? _reference;
    private bool _disposed;

    private string CssClass => ShopCssClass.Join("shop-native", "shop-dialog",
        ShopCssClass.Modifier("shop-dialog-width", MaxWidth ?? ShopMaxWidth.None, nameof(MaxWidth)));

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed)
            return;
        try
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopDialog.js");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
            _reference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("show", _element, _reference);
        }
        catch (JSException exception)
        {
            Logger.LogError(exception, "The native dialog could not be opened.");
            if (!_disposed)
                await OnDismiss.InvokeAsync();
        }
    }

    /// <summary>Synchronizes browser dismissal with the owning request's cancellation.</summary>
    [JSInvokable]
    public Task DismissAsync() => _disposed ? Task.CompletedTask : OnDismiss.InvokeAsync();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", _element);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            _reference?.Dispose();
        }
    }
}
