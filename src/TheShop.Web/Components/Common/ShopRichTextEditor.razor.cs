using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Rich-text editor driving a vendored Quill 2.0.3 instance directly (plan Decision 1),
/// registered to exactly the header/bold/italic/list/link formats (Decision 2). The host
/// <c>&lt;div @ref&gt;</c> renders with no child content of its own — Quill mounts on an inner
/// div it appends itself, so Blazor never manages Quill's own DOM. Content crosses the boundary
/// only through <see cref="Value"/> / <see cref="ValueChanged"/> as whitelisted HTML, loaded via
/// <c>quill.clipboard.dangerouslyPasteHTML</c> so it is parsed into a Delta against the registered
/// formats rather than assigned to <c>innerHTML</c> (AC-11). Quill's own <c>snow</c> toolbar owns
/// formatting UI directly (Decision 2 waiver — Rules 2/3/19 waived for this component only, so no
/// consumer-side toolbar or format-state callback is needed). <c>Class</c>, <c>Style</c>, and
/// unmatched attributes target the host; its theme overrides live in
/// <c>Styles/components/_rich-text-editor.scss</c>.
/// </summary>
public partial class ShopRichTextEditor : ShopComponentBase, IAsyncDisposable
{
    private static readonly string[] Formats = ["header", "bold", "italic", "list", "link"];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// The editor's HTML content. Use with <c>@bind-Value</c>.
    /// </summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>
    /// Fires with the editor's current HTML whenever the staff member edits it.
    /// </summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// Fires with the plain-text length (Decision 5's counting rule) on every edit, for a consumer-rendered counter.
    /// </summary>
    [Parameter] public EventCallback<int> TextLengthChanged { get; set; }

    /// <summary>
    /// Fires once per paste whose clipboard HTML carried formatting outside the grammar (FR-7, AC-10).
    /// </summary>
    [Parameter] public EventCallback OnUnsupportedPaste { get; set; }

    /// <summary>
    /// Makes the editor read-only and dims its toolbar.
    /// </summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Optional resource-backed hint shown inside an empty editor; it never replaces the label.
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// The id of the element that labels this editor, applied to its editable region.
    /// </summary>
    [Parameter] public string? AriaLabelledBy { get; set; }

    private ElementReference _host;

    private IJSObjectReference? _jsModule;

    private DotNetObjectReference<ShopRichTextEditor>? _dotNetRef;

    private bool _initialized;

    private string? _lastAppliedValue;

    private bool _lastAppliedDisabled;

    private string ClassName => ShopCssClass.Join(
        "shop-native",
        "shop-rich-text-editor",
        Disabled ? "shop-rich-text-editor-disabled" : null,
        Class);

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shop-rich-text-editor.js");

        var options = new
        {
            formats = Formats,
            disabled = Disabled,
            initialHtml = Value,
            placeholder = Placeholder,
            ariaLabelledBy = AriaLabelledBy,
        };

        await _jsModule.InvokeVoidAsync("init", _host, _dotNetRef, options);

        _initialized = true;
        _lastAppliedValue = Value;
        _lastAppliedDisabled = Disabled;
    }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized || _jsModule is null)
            return;

        if (Value != _lastAppliedValue)
        {
            _lastAppliedValue = Value;
            await _jsModule.InvokeVoidAsync("setHtml", _host, Value ?? string.Empty);
        }

        if (Disabled != _lastAppliedDisabled)
        {
            _lastAppliedDisabled = Disabled;
            await _jsModule.InvokeVoidAsync("setDisabled", _host, Disabled);
        }
    }

    /// <summary>
    /// Called by the JS <c>text-change</c> listener with the editor's current semantic HTML and plain-text length.
    /// </summary>
    [JSInvokable]
    public async Task OnTextChanged(string html, int textLength)
    {
        _lastAppliedValue = html;
        Value = html;

        if (ValueChanged.HasDelegate)
            await ValueChanged.InvokeAsync(html);

        if (TextLengthChanged.HasDelegate)
            await TextLengthChanged.InvokeAsync(textLength);
    }

    /// <summary>
    /// Called by the JS paste listener when the clipboard HTML carried unsupported formatting.
    /// </summary>
    [JSInvokable]
    public async Task OnPasteUnsupported()
    {
        if (OnUnsupportedPaste.HasDelegate)
            await OnUnsupportedPaste.InvokeAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("dispose", _host);
            await _jsModule.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }
}
