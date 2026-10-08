using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>Reusable file intake. Awaited consumers must read browser files before the input resets.
/// Root attributes/style target the drop surface; custom content opens its picker with data-file-picker.</summary>
public partial class ShopFileDropzone : ShopComponentBase, IAsyncDisposable
{
    /// <summary>Accessible picker name and default trigger text.</summary>
    [Parameter] public string Label { get; set; } = Strings.FileDropzone_AddFiles;
    /// <summary>Instruction displayed while files hover over the default trigger.</summary>
    [Parameter] public string DragLabel { get; set; } = Strings.FileDropzone_DropFiles;
    /// <summary>Instructions associated with the file input and trigger.</summary>
    [Parameter] public string? HelperText { get; set; }
    /// <summary>Caller-owned validation message and error treatment.</summary>
    [Parameter] public string? ErrorText { get; set; }
    /// <summary>Browser picker filter; consumers still validate selected files.</summary>
    [Parameter] public string? Accept { get; set; }
    /// <summary>Allows a batch of files in one selection.</summary>
    [Parameter] public bool Multiple { get; set; }
    /// <summary>Prevents browsing, drops, and callback dispatch.</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>Consumes a transient batch before reset; no selection is retained here.</summary>
    [Parameter] public EventCallback<IReadOnlyList<IBrowserFile>> FilesSelected { get; set; }
    /// <summary>Optional drop-surface content, including an enabled data-file-picker button.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;
    private readonly string _inputId = $"shop-files-{Guid.NewGuid():N}";
    private string _hintId => $"{_inputId}-hint";
    private ElementReference _root;
    private InputFile _input = default!;
    private IJSObjectReference? _module;
    private bool _receiving;
    private bool _disposed;
    private bool EffectiveDisabled => Disabled || _receiving || _disposed;
    private string ClassName => ShopCssClass.Join("shop-file-dropzone", ErrorText is not null ? "shop-file-dropzone-error" : null, Class);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopFileDropzone.js");
        if (_disposed) { if (module is not null) await module.DisposeAsync(); return; }
        _module = module;
        if (_module is not null) await _module.InvokeVoidAsync("bind", _root, _input.Element);
    }

    private async Task ReceiveAsync(InputFileChangeEventArgs args)
    {
        if (EffectiveDisabled) return;
        _receiving = true;
        try
        {
            await FilesSelected.InvokeAsync(args.GetMultipleFiles(int.MaxValue));
        }
        finally
        {
            _receiving = false;
            if (!_disposed && _module is not null) await _module.InvokeVoidAsync("reset", _root);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try { await _module.InvokeVoidAsync("unbind", _root); await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
