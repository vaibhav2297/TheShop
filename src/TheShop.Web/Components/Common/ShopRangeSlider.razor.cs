using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>A two-thumb decimal range with synchronized numeric editors. Formatting is display-only; Class, Style and attributes target the fieldset.</summary>
public partial class ShopRangeSlider : ShopComponentBase, IAsyncDisposable
{
    /// <summary>Accessible name for the entire range.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    /// <summary>Accessible thumb name and visible lower-editor label.</summary>
    [Parameter, EditorRequired] public string MinimumLabel { get; set; } = string.Empty;
    /// <summary>Accessible thumb name and visible upper-editor label.</summary>
    [Parameter, EditorRequired] public string MaximumLabel { get; set; } = string.Empty;
    /// <summary>Inclusive allowed lower bound.</summary>
    [Parameter] public decimal Min { get; set; }
    /// <summary>Inclusive allowed upper bound; equal bounds disable interaction.</summary>
    [Parameter] public decimal Max { get; set; } = 100m;
    /// <summary>Positive increment anchored at Min. Max remains selectable when the span is not a whole number of steps.</summary>
    [Parameter] public decimal Step { get; set; } = 1m;
    /// <summary>Controlled lower/upper pair. Out-of-range values are normalized for display without dispatching a callback.</summary>
    [Parameter, EditorRequired] public ShopRangeValue Value { get; set; }
    /// <summary>Emits both bounds for each valid interaction. Query debounce belongs to the consumer.</summary>
    [Parameter] public EventCallback<ShopRangeValue> ValueChanged { get; set; }
    /// <summary>Formats labels and unfocused editors; editing accepts plain invariant decimal numbers, never formatted currency.</summary>
    [Parameter] public Func<decimal, string>? ValueFormatter { get; set; }
    /// <summary>Prevents thumb and editor updates.</summary>
    [Parameter] public bool Disabled { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;
    private readonly string _id = $"shop-range-{Guid.NewGuid():N}";
    private Task<IJSObjectReference>? _moduleTask;
    private ShopRangeValue _current;
    private ShopRangeValue _supplied;
    private (decimal Min, decimal Max, decimal Step)? _bounds;
    private decimal? _lowerValue => _current.Lower;
    private decimal? _upperValue => _current.Upper;
    private bool _disposed;
    private int? _activeThumb;
    private bool IsDisabled => Disabled || Min == Max;
    private string ClassName => ShopCssClass.Join("shop-native", "shop-range", IsDisabled ? "shop-range-disabled" : null, Class);
    private string BubbleClass => ShopCssClass.Join("shop-range-bubble", _activeThumb == 0 ? "shop-range-bubble-lower" : "shop-range-bubble-upper");
    private string TrackStyle => $"--shop-range-lower: {Number(Percent(_current.Lower))}%; --shop-range-upper: {Number(Percent(_current.Upper))}%;";
    private static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private string Format(decimal value) => ValueFormatter?.Invoke(value) ?? Number(value);
    private decimal Percent(decimal value) => Max == Min ? 0 : (value - Min) / (Max - Min) * 100;
    private string ErrorText(bool lower) => string.Format(Strings.Range_InvalidNumber,
        Format(lower ? Min : _current.Lower), Format(lower ? _current.Upper : Max));

    /// <summary>Rejects invalid labels/bounds/steps and synchronizes genuine external changes without resetting a live draft on unrelated renders.</summary>
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        ArgumentException.ThrowIfNullOrWhiteSpace(MinimumLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(MaximumLabel);
        if (Max < Min || Step <= 0) throw new ArgumentOutOfRangeException(nameof(Step), "Bounds must be ordered and step positive.");
        try { _ = Max - Min; }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(Max), "The range span must fit in decimal."); }
        var bounds = (Min, Max, Step);
        if (_bounds != bounds || (Value != _supplied && Value != _current))
        {
            var lower = Snap(Value.Lower);
            _current = new(lower, Math.Max(lower, Snap(Value.Upper)));
        }
        _bounds = bounds;
        _supplied = Value;
        if (IsDisabled) _activeThumb = null;
    }

    private decimal Snap(decimal value)
    {
        value = Math.Clamp(value, Min, Max);
        if (value == Max || value == Min) return value;
        var offset = value - Min;
        var remainder = offset % Step;
        var down = value - remainder;
        var distanceUp = Math.Min(Step - remainder, Max - value);
        return remainder < distanceUp ? down : value + distanceUp;
    }

    private Task SetLowerAsync(decimal value) => PublishAsync(new(Math.Min(Snap(value), _current.Upper), _current.Upper));
    private Task SetUpperAsync(decimal value) => PublishAsync(new(_current.Lower, Math.Max(Snap(value), _current.Lower)));

    private async Task PublishAsync(ShopRangeValue value)
    {
        if (IsDisabled || _disposed || value == _current) return;
        _current = value;
        await ValueChanged.InvokeAsync(value);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        _moduleTask ??= JS.InvokeAsync<IJSObjectReference>("import", "./js/shopRangeSlider.js").AsTask();
        var module = await _moduleTask;
        if (!_disposed) await module.InvokeVoidAsync("sync", _id);
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
            await module.InvokeVoidAsync("dispose", _id);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }
}
