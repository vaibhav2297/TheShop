using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Layout;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Top navigation bar rendered in <see cref="MainLayout"/>. Displays the brand logo,
/// primary nav links, and auth-aware action icons. The layout owns the account drawer state.
/// </summary>
public partial class ShopAppBar : ShopComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Raised when the compact navigation action is activated.</summary>
    [Parameter] public EventCallback OnMenuClick { get; set; }

    /// <summary>Raised when CSS hides the compact trigger; the layout closes its navigation modal.</summary>
    [Parameter] public EventCallback OnMenuUnavailable { get; set; }

    /// <summary>Whether the layout's navigation drawer is open.</summary>
    [Parameter] public bool MenuOpen { get; set; }

    /// <summary>The controlled navigation dialog's ID.</summary>
    [Parameter] public string? MenuDrawerId { get; set; }

    /// <summary>Raised when the authenticated account action is activated.</summary>
    [Parameter] public EventCallback OnAccountClick { get; set; }

    /// <summary>Whether the layout's account drawer is open; exposed to assistive technology.</summary>
    [Parameter] public bool AccountOpen { get; set; }

    private string ClassName => ShopCssClass.Join("shop-native", "shop-appbar", Class);

    private ElementReference _menuTrigger;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ShopAppBar>? _reference;
    private bool _disposed;

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopAppBar.js");
        if (_disposed)
        {
            await module.DisposeAsync();
            return;
        }
        _module = module;
        _reference = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("observeMenu", _menuTrigger, _reference);
    }

    /// <summary>Notifies the layout when the compact menu becomes unavailable.</summary>
    [JSInvokable]
    public Task MenuUnavailableAsync() => _disposed ? Task.CompletedTask : OnMenuUnavailable.InvokeAsync();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", _menuTrigger);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _reference?.Dispose(); }
    }
}
