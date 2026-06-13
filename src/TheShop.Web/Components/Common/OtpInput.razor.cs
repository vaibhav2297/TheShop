using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable one-time-password (OTP) input. Renders <see cref="Length"/> single-digit
/// boxes backed by <see cref="MudNumericField{T}"/> so only 0–9 are accepted natively.
/// Supports auto-advance focus on entry, backspace navigation to the previous box,
/// arrow-key movement, and full paste distribution via a lightweight JS clipboard
/// intercept. A companion JS <c>focusin</c> listener selects all text whenever any box
/// gains focus, so typing always replaces the existing digit rather than appending.
/// Inherits from <see cref="MudComponentBase"/> so consumers can forward <c>Class</c>,
/// <c>Style</c>, and arbitrary HTML attributes to the root element.
/// </summary>
public partial class OtpInput : IAsyncDisposable
{
    #region Parameters

    /// <summary>Number of digit boxes to render. Defaults to 6.</summary>
    [Parameter] public int Length { get; set; } = 6;

    /// <summary>
    /// The concatenated OTP value (e.g. <c>"123456"</c>). Always all-digit and at most
    /// <see cref="Length"/> characters long. Use with <c>@bind-Value</c>.
    /// </summary>
    [Parameter] public string Value { get; set; } = string.Empty;

    /// <summary>Fires whenever the value changes, with the new concatenated value.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Fires once when the value reaches <see cref="Length"/> digits.</summary>
    [Parameter] public EventCallback<string> OnComplete { get; set; }

    /// <summary>When true, all boxes are disabled (e.g. while submission is in flight).</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>When true, the first empty box is focused on first render.</summary>
    [Parameter] public bool AutoFocus { get; set; } = true;

    /// <summary>Optional caption rendered beneath the boxes. Consumer supplies a localized string.</summary>
    [Parameter] public string? HelperText { get; set; }

    #endregion

    #region Injections

    [Inject] private IJSRuntime JS { get; set; } = default!;

    #endregion

    #region State

    private string[] _digits = [];
    private string _currentValue = string.Empty;
    private bool _completeFired;
    private bool _hasAutoFocused;

    #endregion

    #region JS Interop

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<OtpInput>? _dotNetRef;

    // Stable id used to locate the container element from JavaScript.
    private readonly string _containerId = $"otp-{Guid.NewGuid():N}";

    #endregion

    #region CSS Forwarding

    protected string Classname => new CssBuilder("shop-otp-input")
        .AddClass(Class)
        .Build();

    protected string Stylename => new StyleBuilder()
        .AddStyle(Style)
        .Build();

    #endregion

    #region Lifecycle

    protected override void OnParametersSet()
    {
        // Resize when Length changes, preserving digits that still fit.
        if (_digits.Length != Length)
        {
            var newDigits = new string[Length];
            for (var i = 0; i < Length; i++)
                newDigits[i] = i < _digits.Length ? _digits[i] : string.Empty;

            _digits = newDigits;
        }

        // Sync external Value → internal digits (skip if we originated the change).
        if (Value != _currentValue)
        {
            var sanitized = SanitizeDigits(Value);
            if (sanitized.Length > Length) sanitized = sanitized[..Length];

            for (var i = 0; i < Length; i++)
                _digits[i] = i < sanitized.Length ? sanitized[i].ToString() : string.Empty;

            _currentValue = sanitized;
            _completeFired = sanitized.Length == Length;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Load the JS module: registers the paste interceptor and the focusin
        // select-all handler on the container div.
        _dotNetRef = DotNetObjectReference.Create(this);
        _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopOtpInput.js");
        await _jsModule.InvokeVoidAsync("registerPaste", _containerId, _dotNetRef);

        if (AutoFocus && !_hasAutoFocused && Length > 0)
        {
            _hasAutoFocused = true;
            await FocusBoxAsync(FindFirstEmptyIndex());
        }
    }

    #endregion

    #region JS Invokable

    /// <summary>
    /// Called by the JS paste listener with the raw clipboard text. Strips non-digits
    /// and distributes one digit per box starting from index 0.
    /// </summary>
    [JSInvokable]
    public async Task HandlePasteAsync(string text)
    {
        var digits = SanitizeDigits(text);
        for (var i = 0; i < Length; i++)
            _digits[i] = i < digits.Length ? digits[i].ToString() : string.Empty;

        await EmitValueAsync();
        await FocusBoxAsync(FindFirstEmptyIndex());
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Called by the JS keydown listener for every digit key press. By the time
    /// this method is invoked, JS has already:
    /// <list type="bullet">
    ///   <item>Called <c>e.preventDefault()</c> — MudNumericField never receives
    ///   <c>oninput</c>, so its internal edit buffer is unchanged.</item>
    ///   <item>Written <c>e.target.value = key</c> — the digit is visible in the
    ///   DOM immediately and MudBlazor will read the correct digit on the upcoming
    ///   blur event (preventing it from firing <c>ValueChanged(null)</c> and
    ///   clearing the box).</item>
    /// </list>
    /// Focus is advanced <em>before</em> <see cref="EmitValueAsync"/> so the box
    /// is already unfocused when Blazor re-renders. MudNumericField only picks up
    /// the externally-bound <c>Value</c> prop when it is not in "edit mode" (i.e.
    /// not focused). Moving focus first guarantees the render lands in display mode.
    /// This also means same-digit repeats always advance focus — MudBlazor would
    /// suppress <c>ValueChanged</c> for an identical value, but
    /// <c>FocusBoxAsync</c> is called unconditionally before that check.
    /// </summary>
    [JSInvokable]
    public async Task HandleDigitKeyAsync(int index, string key)
    {
        if (index < 0 || index >= Length) return;

        var changed = _digits[index] != key;
        _digits[index] = key;

        // Move focus FIRST — before any StateHasChanged is queued — so the box
        // is in display mode (not edit mode) when Blazor re-renders it.
        if (index + 1 < Length)
            await FocusBoxAsync(index + 1);

        if (changed)
            await EmitValueAsync();

        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Event Handlers

    private async Task OnDigitChangedAsync(int index, int? value)
    {
        // JS keydown is now prevented for digit keys, so MudNumericField only fires
        // ValueChanged for non-keyboard input such as mobile browser autofill.
        // Guard against value being the same (autofill re-applying an identical digit).
        var newDigit = value.HasValue ? value.Value.ToString() : string.Empty;
        if (_digits[index] == newDigit) return;

        _digits[index] = newDigit;
        await EmitValueAsync();

        if (value.HasValue && index + 1 < Length)
            await FocusBoxAsync(index + 1);
    }

    private async Task OnKeyDownAsync(int index, KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            // Backspace on an empty box clears the previous box and focuses it.
            case "Backspace":
                if (_digits[index].Length == 0 && index > 0)
                {
                    _digits[index - 1] = string.Empty;
                    await EmitValueAsync();
                    await FocusBoxAsync(index - 1);
                }
                break;

            case "ArrowLeft":
                if (index > 0) await FocusBoxAsync(index - 1);
                break;

            case "ArrowRight":
                if (index + 1 < Length) await FocusBoxAsync(index + 1);
                break;
        }
    }

    #endregion

    #region Helpers

    private async Task EmitValueAsync()
    {
        _currentValue = string.Concat(_digits);
        StateHasChanged();

        if (ValueChanged.HasDelegate)
            await ValueChanged.InvokeAsync(_currentValue);

        var isComplete = _currentValue.Length == Length;
        if (isComplete && !_completeFired)
        {
            _completeFired = true;
            if (OnComplete.HasDelegate)
                await OnComplete.InvokeAsync(_currentValue);
        }
        else if (!isComplete)
        {
            _completeFired = false;
        }
    }

    private async Task FocusBoxAsync(int index)
    {
        if (index < 0 || index >= Length || _jsModule is null) return;
        await _jsModule.InvokeVoidAsync("focusInput", _containerId, index);
    }

    private int FindFirstEmptyIndex()
    {
        for (var i = 0; i < Length; i++)
            if (string.IsNullOrEmpty(_digits[i])) return i;
        return Length - 1;
    }

    private static string SanitizeDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
            if (char.IsDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
            await _jsModule.DisposeAsync();
        _dotNetRef?.Dispose();
    }

    #endregion
}
