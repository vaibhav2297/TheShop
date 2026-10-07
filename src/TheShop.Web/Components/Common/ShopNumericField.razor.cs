using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>A nullable decimal editor with plain-number drafts and display-only formatting. Attributes target the input.</summary>
public partial class ShopNumericField : InputBase<decimal?>
{
    /// <summary>Visible floating label; otherwise supply an accessible name through attributes.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Optional description associated with the input.</summary>
    [Parameter] public string? HelperText { get; set; }

    /// <summary>Inclusive lower bound; omitted means unbounded.</summary>
    [Parameter] public decimal? Min { get; set; }

    /// <summary>Inclusive upper bound; omitted means unbounded.</summary>
    [Parameter] public decimal? Max { get; set; }

    /// <summary>Formats committed, unfocused values only. Parsing always accepts plain invariant decimals.</summary>
    [Parameter] public Func<decimal, string>? ValueFormatter { get; set; }

    /// <summary>Rejects an empty draft instead of committing null.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Localized empty-value error; defaults to the shared required-number message.</summary>
    [Parameter] public string? RequiredError { get; set; }

    /// <summary>Optional localized override for parsing and bounds errors.</summary>
    [Parameter] public string? ParsingError { get; set; }

    /// <summary>Prevents editing and callbacks, including synthetic events.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>External validation error, owned by the consumer.</summary>
    [Parameter] public string? ErrorText { get; set; }

    /// <summary>Optional row-owned draft retained across virtualization. Omit for ordinary fields.</summary>
    [Parameter] public ShopNumericDraft DraftState { get; set; } = new();

    private readonly string _id = $"shop-number-{Guid.NewGuid():N}";
    private string? _draft { get => DraftState.Text; set => DraftState.Text = value; }
    private decimal? _supplied { get => DraftState.Supplied; set => DraftState.Supplied = value; }
    private decimal? _committed { get => DraftState.Committed; set => DraftState.Committed = value; }
    private bool _initialized, _editing, _disposed;
    private string? _error { get => DraftState.Error; set => DraftState.Error = value; }
    private ValidationMessageStore? _messages;
    private Task _pendingChange = Task.CompletedTask;
    private bool HasLabel => !string.IsNullOrWhiteSpace(Label);
    private string InputId => string.IsNullOrWhiteSpace(AttributeText("id")) ? _id : AttributeText("id")!;
    private string HelperId => $"{InputId}-hint";
    private string ErrorId => $"{InputId}-error";
    private string InputClass => ShopCssClass.Join("shop-field-input", CssClass);
    private string? DisplayText => _editing || DraftState.HasDraft || _error is not null ? _draft : Format(_committed);
    private IEnumerable<string> Errors => (EditContext?.GetValidationMessages(FieldIdentifier) ?? [])
        .Concat(new[] { _error, ErrorText }.OfType<string>()).Where(text => !string.IsNullOrWhiteSpace(text)).Distinct();
    private string? InvalidAttribute => Errors.Any() ? "true" : null;
    private static string? Number(decimal? value) => value?.ToString("0.############################", CultureInfo.InvariantCulture);
    private string? Format(decimal? value) => value is { } number ? ValueFormatter?.Invoke(number) ?? Number(number) : null;
    private string? AttributeText(string key) => AdditionalAttributes?
        .FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    private IReadOnlyDictionary<string, object> InputAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (AdditionalAttributes is not null)
                foreach (var attribute in AdditionalAttributes) attributes[attribute.Key] = attribute.Value;
            if (HasLabel) { attributes.Remove("aria-label"); attributes.Remove("aria-labelledby"); }
            string[] owned = ["value", "type", "inputmode", "placeholder", "disabled", "required", "aria-invalid", "aria-required", "oninput", "onchange", "onfocus", "onblur", "onkeydown"];
            foreach (var key in owned) attributes.Remove(key);
            attributes["aria-describedby"] = string.Join(' ', new[] { AttributeText("aria-describedby"), string.IsNullOrWhiteSpace(HelperText) ? null : HelperId, ErrorId }
                .OfType<string>().SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal));
            return attributes;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (!HasLabel && string.IsNullOrWhiteSpace(AttributeText("aria-label")) && string.IsNullOrWhiteSpace(AttributeText("aria-labelledby")))
            throw new ArgumentException("A label or ARIA accessible name is required.", nameof(Label));
        if (Min > Max) throw new ArgumentOutOfRangeException(nameof(Max), "Bounds must be ordered.");
        if (!DraftState.Initialized || (Value != _committed && (Value != _supplied || !DraftState.HasDraft)))
        {
            _committed = Value;
            _draft = Number(Value);
            DraftState.HasDraft = false;
            SetError(null);
        }
        if (!_initialized && EditContext is not null)
        {
            _messages = new ValidationMessageStore(EditContext);
            EditContext.OnValidationRequested += ValidateRequested;
        }
        _initialized = true;
        DraftState.Initialized = true;
        _supplied = Value;
        if (_messages is not null) SetError(_error);
    }

    private void BeginEdit()
    {
        if (Disabled || _disposed) return;
        _editing = true;
        if (_error is null && !DraftState.HasDraft) _draft = Number(_committed);
    }

    private void SetDraft(ChangeEventArgs args)
    {
        if (Disabled || _disposed) return;
        _editing = true;
        _draft = args.Value?.ToString();
        DraftState.HasDraft = true;
        SetError(null);
    }

    private async Task BlurAsync()
    {
        if (Disabled || _disposed) return;
        await ValidateAsync();
        _editing = false;
    }

    private Task EditKeyAsync(KeyboardEventArgs args)
    {
        if (Disabled || _disposed) return Task.CompletedTask;
        if (args.Key == "Enter") return ValidateAsync();
        if (args.Key == "Escape")
        {
            _draft = Number(_committed);
            DraftState.HasDraft = false;
            SetError(null);
        }
        return Task.CompletedTask;
    }

    /// <summary>Commits a valid pending draft and awaits its callback. Forms call this before submission; invalid drafts retain their previous value.</summary>
    /// <returns>Whether both the draft and associated validation messages are valid.</returns>
    public async Task<bool> ValidateAsync()
    {
        await _pendingChange;
        if (Disabled) return !Errors.Any();
        var draft = _draft;
        if (!TryParseValueFromString(_draft, out var value, out var error))
        {
            SetError(error);
            if (!_disposed) EditContext?.NotifyFieldChanged(FieldIdentifier);
            if (!_disposed) StateHasChanged();
            return false;
        }
        SetError(null);
        if (value != _committed)
        {
            Value = _committed = value;
            _pendingChange = ValueChanged.InvokeAsync(value);
            await _pendingChange;
            if (!_disposed) EditContext?.NotifyFieldChanged(FieldIdentifier);
            _committed = Value;
        }
        if (_draft == draft)
        {
            _draft = Number(_committed);
            DraftState.HasDraft = false;
        }
        if (!_disposed) StateHasChanged();
        return _error is null && (_disposed || !Errors.Any());
    }

    private void ValidateRequested(object? sender, ValidationRequestedEventArgs args)
    {
        if (!Disabled)
        {
            TryParseValueFromString(_draft, out _, out var error);
            SetError(error);
        }
    }

    private void SetError(string? error)
    {
        _error = error;
        if (_messages is null) return;
        var messages = new[] { error, ErrorText }.OfType<string>().Where(text => !string.IsNullOrWhiteSpace(text)).Distinct().ToArray();
        if (_messages[FieldIdentifier].SequenceEqual(messages)) return;
        _messages.Clear(FieldIdentifier);
        _messages.Add(FieldIdentifier, messages);
        EditContext!.NotifyValidationStateChanged();
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, out decimal? result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = null;
        validationErrorMessage = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (Required) validationErrorMessage = RequiredError ?? Strings.Numeric_Required;
            return !Required;
        }
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out var number))
            validationErrorMessage = ParsingError ?? Strings.Numeric_Invalid;
        else if ((Min is { } min && number < min) || (Max is { } max && number > max))
            validationErrorMessage = ParsingError ?? (Min.HasValue && Max.HasValue
                ? string.Format(Strings.Range_InvalidNumber, Format(Min), Format(Max))
                : Min.HasValue ? string.Format(Strings.Numeric_Minimum, Format(Min)) : string.Format(Strings.Numeric_Maximum, Format(Max)));
        else result = number;
        return validationErrorMessage is null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        if (EditContext is not null)
        {
            EditContext.OnValidationRequested -= ValidateRequested;
            _messages?.Clear();
            _messages = null;
            EditContext.NotifyValidationStateChanged();
        }
        base.Dispose(disposing);
    }
}
