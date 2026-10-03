using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>An outlined, single-select field with no free-text entry. Attributes and lowercase class/style target its combobox button.</summary>
/// <typeparam name="TValue">The option value type; use a nullable type when no selection is valid.</typeparam>
public partial class ShopSelect<TValue> : InputBase<TValue>, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    private readonly string _generatedId = $"shop-select-{Guid.NewGuid():N}";
    private List<(string Id, ShopSelectOption<TValue> Option)> _entries = [];
    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ShopSelect<TValue>>? _reference;
    private bool _disposed;

    /// <summary>Required resource-backed floating label and accessible name.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>Ordered options with unique typed values. Labels are localized by the caller.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<ShopSelectOption<TValue>> Options { get; set; } = [];

    /// <summary>Optional resource-backed hint associated with the combobox.</summary>
    [Parameter] public string? HelperText { get; set; }

    /// <summary>Prevents opening and value changes, including late browser callbacks.</summary>
    [Parameter] public bool Disabled { get; set; }

    private string InputId => string.IsNullOrWhiteSpace(AttributeText("id")) ? _generatedId : AttributeText("id")!;
    private string LabelId => $"{InputId}-label";
    private string ListId => $"{InputId}-options";
    private string HelperId => $"{InputId}-hint";
    private string ErrorId => $"{InputId}-error";
    private string? SelectedId => _entries.FirstOrDefault(entry => IsSelected(entry.Option.Value)).Id;
    private string? SelectedLabel => _entries.FirstOrDefault(entry => IsSelected(entry.Option.Value)).Option?.Label;
    private string ControlClass => ShopCssClass.Join("shop-select-control", SelectedId is not null ? "shop-select-populated" : null);
    private string TriggerClass => ShopCssClass.Join("shop-select-trigger", CssClass);
    private string? InvalidAttribute => EditContext?.GetValidationMessages(FieldIdentifier).Any() == true
        ? "true" : AttributeText("aria-invalid");

    private bool IsSelected(TValue value) => EqualityComparer<TValue>.Default.Equals(CurrentValue, value);
    private string? AttributeText(string name) => AdditionalAttributes?
        .FirstOrDefault(attribute => string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    private IReadOnlyDictionary<string, object> TriggerAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (AdditionalAttributes is not null)
                foreach (var attribute in AdditionalAttributes)
                    attributes[attribute.Key] = attribute.Value;
            string[] owned = ["aria-label", "aria-labelledby", "aria-activedescendant", "contenteditable", "value", "placeholder", "tabindex", "onclick", "onkeydown", "onkeyup"];
            foreach (var name in owned)
                attributes.Remove(name);
            string?[] descriptions = [AttributeText("aria-describedby"), string.IsNullOrWhiteSpace(HelperText) ? null : HelperId, EditContext is null ? null : ErrorId];
            var ids = string.Join(' ', descriptions).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal);
            var describedBy = string.Join(' ', ids);
            if (describedBy.Length > 0)
                attributes["aria-describedby"] = describedBy;
            else
                attributes.Remove("aria-describedby");
            return attributes;
        }
    }

    /// <summary>Rejects blank labels and ambiguous duplicate option values; preserves option identity across reordering.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        ArgumentNullException.ThrowIfNull(Options);
        HashSet<TValue> values = [];
        List<(string Id, ShopSelectOption<TValue> Option)> entries = [];
        foreach (var option in Options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(option.Label);
            if (!values.Add(option.Value))
                throw new ArgumentException("Select option values must be unique.", nameof(Options));
            var previous = _entries.FirstOrDefault(entry => EqualityComparer<TValue>.Default.Equals(entry.Option.Value, option.Value));
            entries.Add((previous.Id ?? $"shop-option-{Guid.NewGuid():N}", option));
        }
        _entries = entries;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;
        if (firstRender)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopSelect.js");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
            _reference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("initialize", _element, _reference);
        }
        if (_module is not null && !_disposed)
            await _module.InvokeVoidAsync("sync", _element);
    }

    /// <summary>Commits only a currently available option. Removed, disabled, stale and disposed selections are ignored.</summary>
    [JSInvokable]
    public Task SelectAsync(string? optionId) => _disposed ? Task.CompletedTask : InvokeAsync(() =>
    {
        if (_disposed || Disabled)
            return;
        var entry = _entries.FirstOrDefault(candidate => candidate.Id == optionId);
        if (entry.Option is null || entry.Option.Disabled)
            return;
        CurrentValue = entry.Option.Value;
        StateHasChanged();
    });

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, out TValue result,
        [NotNullWhen(false)] out string? validationErrorMessage) =>
        throw new NotSupportedException("ShopSelect commits typed options; free-text parsing is not supported.");

    /// <inheritdoc />
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
            ((IDisposable)this).Dispose();
        }
    }
}
