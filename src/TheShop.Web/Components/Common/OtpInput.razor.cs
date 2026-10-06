using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable one-time-password (OTP) input. Renders <see cref="Length"/> label-less
/// <see cref="ShopTextField"/> digit boxes that accept only 0–9, each named by a
/// resource-backed ARIA label. Supports auto-advance focus on entry, backspace navigation
/// to the previous box, arrow-key movement, and full paste or one-time-code autofill
/// distribution. A companion JS module selects the current digit whenever a box gains
/// focus, so typing always replaces it. <c>Class</c>, <c>Style</c>, and unmatched
/// attributes target the root element.
/// </summary>
public partial class OtpInput : ShopComponentBase, IAsyncDisposable
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

    /// <summary>Optional caption rendered beneath the boxes and associated with each box. Consumer supplies a localized string.</summary>
    [Parameter] public string? HelperText { get; set; }

    #endregion

    #region Injections

    [Inject] private IJSRuntime JS { get; set; } = default!;

    #endregion

    #region State

    private DigitSlot[] _digits = [];
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

    private string ClassName => ShopCssClass.Join("shop-native", "shop-otp-input", Class);

    private string? HelperId => string.IsNullOrEmpty(HelperText) ? null : $"{_containerId}-hint";

    #endregion

    #region Lifecycle

    protected override void OnParametersSet()
    {
        // Resize when Length changes, preserving digits that still fit.
        if (_digits.Length != Length)
        {
            var newDigits = new DigitSlot[Length];
            for (var i = 0; i < Length; i++)
                newDigits[i] = i < _digits.Length ? _digits[i] : new DigitSlot();

            _digits = newDigits;
        }

        // Sync external Value → internal digits (skip if we originated the change).
        if (Value != _currentValue)
        {
            var sanitized = SanitizeDigits(Value);
            if (sanitized.Length > Length) sanitized = sanitized[..Length];

            SetDigits(sanitized);
            _currentValue = sanitized;
            _completeFired = sanitized.Length == Length;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Load the JS module: registers the paste interceptor, digit keydown owner, and
        // focusin select-all handler on the container div.
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
        if (Disabled) return;

        SetDigits(SanitizeDigits(text));
        await EmitValueAsync();
        await FocusBoxAsync(FindFirstEmptyIndex());
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Called by the JS keydown listener for every digit key press. JS has already
    /// prevented the browser from inserting the character and written the digit into the
    /// box, so same-digit repeats still advance focus even though no input event fires.
    /// </summary>
    [JSInvokable]
    public async Task HandleDigitKeyAsync(int index, string key)
    {
        if (Disabled || index < 0 || index >= Length) return;

        var changed = _digits[index].Value != key;
        _digits[index].Value = key;

        if (index + 1 < Length)
            await FocusBoxAsync(index + 1);

        if (changed)
            await EmitValueAsync();

        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Event Handlers

    private async Task OnDigitChangedAsync(int index, string? value)
    {
        // Digit keys are owned by JS, so input events come from deletion, virtual
        // keyboards that report no key, and one-time-code autofill.
        var digits = SanitizeDigits(value);

        // A full code (autofill) fills every box; otherwise the newest digit wins.
        if (digits.Length >= Length)
        {
            SetDigits(digits[..Length]);
            await EmitValueAsync();
            await FocusBoxAsync(Length - 1);
            return;
        }

        var newDigit = digits.Length > 0 ? digits[^1].ToString() : string.Empty;

        // Rejected characters re-render the previous digit; the receiver re-renders
        // automatically after this callback.
        if (digits.Length == 0 && !string.IsNullOrEmpty(value)) return;
        if (_digits[index].Value == newDigit) return;

        _digits[index].Value = newDigit;
        await EmitValueAsync();

        if (newDigit.Length > 0 && index + 1 < Length)
            await FocusBoxAsync(index + 1);
    }

    private async Task OnKeyDownAsync(int index, KeyboardEventArgs e)
    {
        if (Disabled) return;

        switch (e.Key)
        {
            // Backspace on an empty box clears the previous box and focuses it.
            case "Backspace":
                if (string.IsNullOrEmpty(_digits[index].Value) && index > 0)
                {
                    _digits[index - 1].Value = string.Empty;
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

    private void SetDigits(string digits)
    {
        for (var i = 0; i < Length; i++)
            _digits[i].Value = i < digits.Length ? digits[i].ToString() : string.Empty;
    }

    private async Task EmitValueAsync()
    {
        _currentValue = string.Concat(_digits.Select(d => d.Value));
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
            if (string.IsNullOrEmpty(_digits[i].Value)) return i;
        return Length - 1;
    }

    private static string SanitizeDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
            if (char.IsAsciiDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    #endregion

    #region Disposal

    /// <summary>Releases the JS module and the .NET reference it holds.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
            await _jsModule.DisposeAsync();
        _dotNetRef?.Dispose();
    }

    #endregion

    // One mutable slot per box: ShopTextField's ValueExpression needs a member accessor.
    private sealed class DigitSlot
    {
        public string? Value { get; set; } = string.Empty;
    }
}
