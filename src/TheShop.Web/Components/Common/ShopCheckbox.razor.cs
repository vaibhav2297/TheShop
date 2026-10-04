using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>A labelled boolean checkbox with optional mixed presentation and built-in Blazor form binding. Native attributes and CSS classes target the input.</summary>
public partial class ShopCheckbox : InputCheckbox, IAsyncDisposable
{
    private readonly string _generatedId = $"shop-checkbox-{Guid.NewGuid():N}";
    private IJSObjectReference? _module;
    private bool _disposed;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Required resource-backed or caller-resolved visible label and accessible name.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>Selects the Figma icon and target dimensions; label typography remains unchanged.</summary>
    [Parameter] public ShopSize Size { get; set; } = ShopSize.Medium;

    /// <summary>Prevents native activation and synthetic changes. Supply the owning operation's BusyFor value when applicable.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Displays a partial selection without introducing a third bound value; the parent owns this state.</summary>
    [Parameter] public bool Indeterminate { get; set; }

    /// <summary>Visually hides the label while retaining its accessible association.</summary>
    [Parameter] public bool HideLabel { get; set; }

    private string InputId => string.IsNullOrWhiteSpace(AttributeText("id")) ? _generatedId : AttributeText("id")!;
    private string ErrorId => $"{InputId}-error";
    private string RootClass => ShopCssClass.Join("shop-native", "shop-checkbox", ShopCssClass.Modifier("shop-checkbox", Size));
    private string InputClass => ShopCssClass.Join("shop-checkbox-input", CssClass);
    private string LabelClass => ShopCssClass.Join("shop-checkbox-text", HideLabel ? "shop-visually-hidden" : null);
    private string? InvalidAttribute => EditContext?.GetValidationMessages(FieldIdentifier).Any() == true
        ? "true" : AttributeText("aria-invalid");

    private IReadOnlyDictionary<string, object> InputAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (AdditionalAttributes is not null)
                foreach (var attribute in AdditionalAttributes)
                    attributes[attribute.Key] = attribute.Value;
            attributes.Remove("aria-label");
            attributes.Remove("aria-labelledby");
            attributes.Remove("aria-checked");
            attributes.Remove("role");
            if (EditContext is not null)
            {
                var ids = $"{AttributeText("aria-describedby")} {ErrorId}"
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal);
                attributes["aria-describedby"] = string.Join(' ', ids);
            }
            return attributes;
        }
    }

    private string? AttributeText(string name) => AdditionalAttributes?
        .FirstOrDefault(attribute => string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    /// <summary>Rejects a blank label rather than rendering an unnamed checkbox.</summary>
    /// <exception cref="ArgumentException">Label is blank.</exception>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
    }

    private void SetValue(bool value)
    {
        if (!Disabled && !_disposed)
            CurrentValue = value;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || (!Indeterminate && _module is null))
            return;
        if (_module is null)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopCheckbox.js");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
        }
        await _module.InvokeVoidAsync("setIndeterminate", Element, Indeterminate);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        finally { ((IDisposable)this).Dispose(); }
    }
}
