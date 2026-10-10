using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>A date-only field using Blazor parsing and the browser's native picker. Unmatched attributes target the input.</summary>
public partial class ShopDateField : InputDate<DateOnly?>
{
    /// <summary>Required visible label and accessible name.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    /// <summary>Optional associated instruction below the input.</summary>
    [Parameter] public string? HelperText { get; set; }
    /// <summary>Inclusive earliest date; null means no lower bound.</summary>
    [Parameter] public DateOnly? Min { get; set; }
    /// <summary>Inclusive latest date; null means no upper bound.</summary>
    [Parameter] public DateOnly? Max { get; set; }
    /// <summary>Requires a nonempty date in native and Blazor validation.</summary>
    [Parameter] public bool Required { get; set; }
    /// <summary>Optional localized required-date error.</summary>
    [Parameter] public string? RequiredError { get; set; }
    /// <summary>Prevents native editing and synthetic change events.</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>Optional synchronous form-owned rule; its error takes precedence over bound errors.</summary>
    [Parameter] public Func<DateOnly?, string?>? Validation { get; set; }

    private readonly string _id = $"shop-date-{Guid.NewGuid():N}";
    private ValidationMessageStore? _messages;
    private string? _parseError, _ruleError;
    private DateOnly? _previousValue;
    private bool _validated, _disposed;
    private string InputId => string.IsNullOrWhiteSpace(AttributeText("id")) ? _id : AttributeText("id")!;
    private string HelperId => $"{InputId}-hint";
    private string ErrorId => $"{InputId}-error";
    private string InputClass => ShopCssClass.Join("shop-field-input", CssClass);
    private IEnumerable<string> Errors => (EditContext?.GetValidationMessages(FieldIdentifier) ?? [])
        .Concat(new[] { _parseError, _ruleError }.OfType<string>()).Distinct();
    private string? InvalidAttribute => Errors.Any() ? "true" : null;
    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private string? AttributeText(string key) => AdditionalAttributes?
        .FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    private IReadOnlyDictionary<string, object> InputAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (AdditionalAttributes is not null)
                foreach (var attribute in AdditionalAttributes) attributes[attribute.Key] = attribute.Value;
            string[] owned = ["type", "value", "min", "max", "required", "disabled", "aria-label", "aria-labelledby", "aria-invalid", "aria-required", "onchange", "oninput", "onblur"];
            foreach (var key in owned) attributes.Remove(key);
            attributes["aria-describedby"] = string.Join(' ', new[] { AttributeText("aria-describedby"), string.IsNullOrWhiteSpace(HelperText) ? null : HelperId, ErrorId }
                .OfType<string>().SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal));
            return attributes;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_disposed) return;
        Type = InputDateType.Date;
        ParsingErrorMessage = Strings.Date_Invalid;
        base.OnParametersSet();
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        if (Min > Max) throw new ArgumentOutOfRangeException(nameof(Max), "Bounds must be ordered.");
        if (EditContext is not null && _messages is null)
        {
            _messages = new ValidationMessageStore(EditContext);
            EditContext.OnValidationRequested += ValidateRequested;
        }
        if (Value != _previousValue)
        {
            _parseError = null;
            CurrentValueAsString = FormatValueAsString(Value);
        }
        _previousValue = Value;
        if (_validated) UpdateRules();
    }

    private void Change(ChangeEventArgs args)
    {
        if (Disabled || _disposed) return;
        _parseError = null;
        CurrentValueAsString = args.Value?.ToString();
        _previousValue = Value;
        Validate();
    }

    private void OnBlur() { if (!Disabled && !_disposed) Validate(); }
    private void ValidateRequested(object? sender, ValidationRequestedEventArgs args) => Validate();

    /// <summary>Validates the current input, including parsing, required and bounds rules. Call before submitting a legacy form outside EditForm.</summary>
    /// <returns>True when the field has no local or EditContext errors.</returns>
    public bool Validate()
    {
        if (_disposed) return false;
        _validated = true;
        UpdateRules();
        StateHasChanged();
        return !Errors.Any();
    }

    private void UpdateRules()
    {
        _ruleError = _parseError is not null ? null
            : Required && Value is null ? RequiredError ?? Strings.Date_Required
            : Validation?.Invoke(Value);
        if (_ruleError is null && _parseError is null && Value is { } date)
        {
            if (Min is { } min && date < min) _ruleError = string.Format(Strings.Date_Minimum, Iso(min));
            else if (Max is { } max && date > max) _ruleError = string.Format(Strings.Date_Maximum, Iso(max));
        }
        if (_messages is null) return;
        var errors = new[] { _ruleError }.OfType<string>().ToArray();
        if (_messages[FieldIdentifier].SequenceEqual(errors)) return;
        _messages.Clear(FieldIdentifier);
        _messages.Add(FieldIdentifier, errors);
        EditContext!.NotifyValidationStateChanged();
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, out DateOnly? result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        var valid = base.TryParseValueFromString(value, out result, out validationErrorMessage);
        _parseError = validationErrorMessage;
        return valid;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (EditContext is not null)
        {
            EditContext.OnValidationRequested -= ValidateRequested;
            _messages?.Clear();
        }
        base.Dispose(disposing);
        EditContext?.NotifyValidationStateChanged();
    }
}
