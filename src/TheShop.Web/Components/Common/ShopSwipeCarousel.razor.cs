using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Controlled, non-looping image carousel using ShopImage and a perceivable position indicator.</summary>
/// <typeparam name="TItem">Caller-owned slide data; the component has no business-data dependency.</typeparam>
public partial class ShopSwipeCarousel<TItem> : ShopComponentBase, IAsyncDisposable
{
    /// <summary>Ordered slides, rendered once without cloning.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<TItem> Items { get; set; } = [];
    /// <summary>Maps caller-owned data to an image source; blank sources use the shared placeholder.</summary>
    [Parameter, EditorRequired] public Func<TItem, string?> ImageSource { get; set; } = default!;
    /// <summary>Maps each item to its resource-backed accessible image description.</summary>
    [Parameter, EditorRequired] public Func<TItem, string> ImageAlt { get; set; } = default!;
    /// <summary>Resource-backed label displayed by ShopImage for missing or failed artwork.</summary>
    [Parameter, EditorRequired] public string PlaceholderLabel { get; set; } = string.Empty;
    /// <summary>Shared image frame and fit, defaulting to a square contained image.</summary>
    [Parameter] public ShopImagePreset ImagePreset { get; set; } = ShopImagePreset.SquareContain;
    /// <summary>Zero-based viewed slide; out-of-range values are clamped when items change.</summary>
    [Parameter] public int ActiveIndex { get; set; }
    /// <summary>Reports settled manual navigation without changing caller-owned item selection.</summary>
    [Parameter] public EventCallback<int> ActiveIndexChanged { get; set; }
    /// <summary>False retains slide markup for a caller-owned static layout, suspending navigation and position feedback.</summary>
    [Parameter] public bool Enabled { get; set; } = true;
    /// <summary>Resource-backed accessible name for the carousel region.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    /// <summary>Formats a resource-backed position announcement from one-based position and count.</summary>
    [Parameter, EditorRequired] public Func<int, int, string> PositionLabel { get; set; } = default!;
    /// <summary>Additional class on the viewport layout slot; static-layout geometry belongs to the caller.</summary>
    [Parameter] public string? ViewportClass { get; set; }
    /// <summary>Additional class on each slide layout slot.</summary>
    [Parameter] public string? SlideClass { get; set; }
    /// <summary>Signals that browser observation and the initial index have been applied.</summary>
    [Parameter] public EventCallback Ready { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;
    private ElementReference _viewport;
    private IJSObjectReference? _module;
    private IJSObjectReference? _controller;
    private DotNetObjectReference<ShopSwipeCarousel<TItem>>? _reference;
    private bool _disposed;
    private int SafeIndex => Items.Count == 0 ? 0 : Math.Clamp(ActiveIndex, 0, Items.Count - 1);
    private bool CanNavigate => Enabled && Items.Count > 1;
    private string ClassName => ShopCssClass.Join("shop-swipe-carousel", Class);
    private string ViewportClassName => ShopCssClass.Join("shop-swipe-carousel-viewport", ViewportClass);
    private string SlideClassName => ShopCssClass.Join("shop-swipe-carousel-slide", SlideClass);
    private string IndicatorStyle => FormattableString.Invariant($"--shop-carousel-count: {Items.Count}; --shop-carousel-index: {SafeIndex};");

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        var justMounted = _controller is null;
        if (_module is null)
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopSwipeCarousel.js");
        if (_disposed) return;
        if (_controller is null)
        {
            _reference = DotNetObjectReference.Create(this);
            _controller = await _module.InvokeAsync<IJSObjectReference>("mount", _viewport, _reference);
        }
        await _controller.InvokeVoidAsync("update", SafeIndex, Enabled, Items.Count);
        if (SafeIndex != ActiveIndex)
            await ActiveIndexChanged.InvokeAsync(SafeIndex);
        if (justMounted)
            await Ready.InvokeAsync();
    }

    /// <summary>Receives settled browser navigation; ignored after disposal or while static.</summary>
    [JSInvokable]
    public Task OnCarouselIndexChanged(int index) =>
        !_disposed && CanNavigate && index >= 0 && index < Items.Count && index != SafeIndex
            ? ActiveIndexChanged.InvokeAsync(index) : Task.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_controller is not null)
            {
                await _controller.InvokeVoidAsync("dispose");
                await _controller.DisposeAsync();
            }
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        finally { _reference?.Dispose(); }
    }
}
