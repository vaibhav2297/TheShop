using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TheShop.Web.Common;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>Controlled image selection with previews, validation, removal, and ordering.
/// Emits ordered snapshots; persistence stays with the form. Owns only URLs it creates.</summary>
public partial class ShopImageUpload : ShopComponentBase, IAsyncDisposable
{
    /// <summary>Ordered selection, including existing images and rejected files.</summary>
    [Parameter] public IReadOnlyList<ShopUploadedImage> Files { get; set; } = [];
    /// <summary>Emits selection changes; the consumer echoes them through Files.</summary>
    [Parameter] public EventCallback<IReadOnlyList<ShopUploadedImage>> FilesChanged { get; set; }
    /// <summary>Appends and enables reordering; false replaces the single selection.</summary>
    [Parameter] public bool Multiple { get; set; }
    /// <summary>Retained entry limit in multiple mode, including rejected entries.</summary>
    [Parameter] public int MaxFileCount { get; set; } = 10;
    /// <summary>Browser picker filter; independent of validation.</summary>
    [Parameter] public string Accept { get; set; } = ".png,.jpg,.jpeg,.webp";
    /// <summary>Permitted browser MIME types; server validation remains authoritative.</summary>
    [Parameter] public IReadOnlyCollection<string> AllowedContentTypes { get; set; } = ["image/png", "image/jpeg", "image/webp"];
    /// <summary>Hard read limit in bytes. Oversized files are never buffered.</summary>
    [Parameter] public long MaxFileSize { get; set; } = 2 * 1024 * 1024;
    /// <summary>Optional localized empty picker text.</summary>
    [Parameter] public string? UploadText { get; set; }
    /// <summary>Optional removal label, supplemented by the image name.</summary>
    [Parameter] public string? RemoveLabel { get; set; }
    /// <summary>Group label and description for persisted images without filenames.</summary>
    [Parameter] public string? PreviewAlt { get; set; }
    /// <summary>Highlights the first valid image and gives it a two-by-two grid allocation.</summary>
    [Parameter] public bool ShowPrimaryBadge { get; set; }
    /// <summary>Optional localized primary badge text.</summary>
    [Parameter] public string? PrimaryLabel { get; set; }
    /// <summary>Optional localized type rejection message.</summary>
    [Parameter] public string? InvalidTypeError { get; set; }
    /// <summary>Optional localized size rejection message.</summary>
    [Parameter] public string? MaxFileSizeError { get; set; }
    /// <summary>Preview fitting; logo inputs can retain BrandLogo.</summary>
    [Parameter] public ShopImagePreset PreviewPreset { get; set; } = ShopImagePreset.SquareContain;
    /// <summary>Prevents selection, removal, and ordering.</summary>
    [Parameter] public bool Disabled { get; set; }

    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    private readonly string _busyKey = $"{BusyKeys.ImageSelection}.{Guid.NewGuid():N}";
    private readonly string _helpId = $"shop-upload-help-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<string> _ownedUrls = [];
    private List<ShopUploadedImage> _pending = [];
    private IReadOnlyList<ShopUploadedImage> _batchBase = [];
    private Task _processing = Task.CompletedTask;
    private Task<IJSObjectReference>? _moduleTask;
    private DotNetObjectReference<ShopImageUpload>? _reference;
    private ElementReference _root;
    private bool _disposed;
    private Guid? _activeId;
    private string? _selectionError;
    private string? _announcement;
    private bool Blocked => Disabled || _disposed || BusyState.IsBusy(_busyKey);
    private bool AtCapacity => Multiple && Files.Count >= MaxFileCount;
    private int ActiveIndex => Files.ToList().FindIndex(image => image.ClientId == _activeId);
    private IReadOnlyList<ShopUploadedImage> DisplayedImages => _pending.Count == 0 ? Files : [.. _batchBase, .. _pending];
    private string ClassName => ShopCssClass.Join("shop-image-upload shop-native", Class);
    private string Hint => string.Format(Strings.ImageUpload_TypesHint,
        Accept == ".png,.jpg,.jpeg,.webp" ? Strings.ImageUpload_DefaultFormats : Accept,
        (MaxFileSize / 1048576d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    private string Instructions => Multiple && Files.Count > 0 ? Strings.ImageUpload_ReorderHint : ShowPrimaryBadge ? Strings.ImageUpload_PrimaryHint : Strings.ImageUpload_PickHint;
    private bool IsPrimary(ShopUploadedImage image) => ShowPrimaryBadge && DisplayedImages.FirstOrDefault(i => i.Error is null)?.ClientId == image.ClientId;
    private string ItemClass(ShopUploadedImage image) => ShopCssClass.Join("shop-image-upload-item", IsPrimary(image) ? "shop-image-upload-primary" : null);
    private string ImageName(ShopUploadedImage image) => string.IsNullOrWhiteSpace(image.FileName)
        ? string.Format(Strings.ImageUpload_ImageName, PreviewAlt ?? Strings.ImageUpload_Label, DisplayedImages.ToList().FindIndex(i => i.ClientId == image.ClientId) + 1)
        : image.FileName;
    private string RemoveName(ShopUploadedImage image) => $"{RemoveLabel ?? Strings.ImageUpload_Remove}: {ImageName(image)}";

    protected override async Task OnParametersSetAsync()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxFileCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxFileSize, 1);
        if (BusyState.IsBusy(_busyKey)) return;
        await RevokeUnusedAsync();
    }

    private async Task RevokeUnusedAsync()
    {
        var retained = Files.Select(f => f.PreviewUrl).ToHashSet();
        foreach (var url in _ownedUrls.Where(url => !retained.Contains(url)).ToArray()) await RevokeAsync(url);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var module = await ModuleAsync();
        if (_disposed || module is null) return;
        _reference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("bindOrder", _root, _reference);
    }

    /// <summary>Waits for file preparation and its callback before the owning form submits.</summary>
    public Task WaitForPendingFilesAsync() => _processing;

    private Task SelectAsync(IReadOnlyList<IBrowserFile> files)
    {
        if (Blocked || files.Count == 0) return Task.CompletedTask;
        _processing = ProcessAsync(files);
        return _processing;
    }

    private async Task ProcessAsync(IReadOnlyList<IBrowserFile> files)
    {
        var limit = Multiple ? Math.Max(0, MaxFileCount - Files.Count) : 1;
        _selectionError = files.Count > limit ? string.Format(Strings.ImageUpload_CountError, Multiple ? MaxFileCount : 1) : null;
        if (limit == 0) return;
        var batch = files.Take(limit).ToArray();
        var prior = Files.ToArray();
        _batchBase = Multiple ? prior : [];
        _pending = [.. batch.Select(file => new ShopUploadedImage([], file.Name, file.ContentType, string.Empty))];
        var committed = false;
        try
        {
            await BusyState.RunAsync(_busyKey, async () =>
            {
                StateHasChanged();
                for (var index = 0; index < batch.Length; index++)
                {
                    var file = batch[index];
                    var error = !AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)
                        ? InvalidTypeError ?? Strings.ImageUpload_InvalidType
                        : file.Size > MaxFileSize ? MaxFileSizeError ?? string.Format(Strings.ImageUpload_SizeError, MaxFileSize / 1048576d) : null;
                    var item = _pending[index];
                    if (error is null)
                    {
                        try
                        {
                            using var stream = file.OpenReadStream(MaxFileSize, _lifetime.Token);
                            using var buffer = new MemoryStream();
                            await stream.CopyToAsync(buffer, _lifetime.Token);
                            var bytes = buffer.ToArray();
                            var module = await ModuleAsync();
                            _lifetime.Token.ThrowIfCancellationRequested();
                            using var reference = new DotNetStreamReference(new MemoryStream(bytes));
                            var url = module is null ? string.Empty : await module.InvokeAsync<string>("createObjectUrl", reference, file.ContentType);
                            if (!string.IsNullOrEmpty(url)) _ownedUrls.Add(url);
                            item = item with { Bytes = bytes, PreviewUrl = url };
                        }
                        catch (Exception exception) when (exception is IOException or JSException)
                        {
                            error = Strings.ImageUpload_ReadError;
                        }
                    }
                    _pending[index] = item with { Error = error };
                    _lifetime.Token.ThrowIfCancellationRequested();
                    if (!_disposed) StateHasChanged();
                }
                // An external reset during preparation takes precedence over this batch.
                if (!Files.SequenceEqual(prior)) return;
                IReadOnlyList<ShopUploadedImage> updated = Multiple ? [.. prior, .. _pending] : [.. _pending];
                await FilesChanged.InvokeAsync(updated);
                committed = true;
                if (!Multiple) foreach (var old in prior) await RevokeAsync(old.PreviewUrl);
            });
        }
        catch (OperationCanceledException) when (_disposed) { }
        finally
        {
            if (!committed) foreach (var pending in _pending) await RevokeAsync(pending.PreviewUrl);
            _pending = [];
            _batchBase = [];
            if (!_disposed)
            {
                await RevokeUnusedAsync();
                StateHasChanged();
            }
        }
    }

    private async Task RemoveAsync(Guid id)
    {
        if (Blocked) return;
        var removed = Files.FirstOrDefault(f => f.ClientId == id);
        if (removed is null) return;
        var name = ImageName(removed);
        var index = Files.ToList().FindIndex(f => f.ClientId == id);
        var updated = Files.Where(f => f.ClientId != id).ToArray();
        await FilesChanged.InvokeAsync(updated);
        await RevokeAsync(removed.PreviewUrl);
        _activeId = updated.Length == 0 ? null : updated[Math.Min(index, updated.Length - 1)].ClientId;
        _selectionError = null;
        _announcement = string.Format(Strings.ImageUpload_Removed, name);
        await FocusAsync(_activeId);
    }

    private Task MoveActiveAsync(int offset) => ActiveIndex < 0 ? Task.CompletedTask : MoveAsync(_activeId!.Value, ActiveIndex + offset);
    private Task ReorderKeyAsync(Guid id, KeyboardEventArgs args)
    {
        if (!args.AltKey || args.Key is not ("ArrowLeft" or "ArrowRight")) return Task.CompletedTask;
        var index = Files.ToList().FindIndex(f => f.ClientId == id);
        return MoveAsync(id, index + (args.Key == "ArrowLeft" ? -1 : 1));
    }

    /// <summary>Requests movement between current client identities from the scoped drag bridge.</summary>
    [JSInvokable]
    public Task ReorderAsync(string source, string target)
    {
        if (!Guid.TryParse(source, out var id) || !Guid.TryParse(target, out var targetId)) return Task.CompletedTask;
        return MoveAsync(id, Files.ToList().FindIndex(f => f.ClientId == targetId));
    }

    private async Task MoveAsync(Guid id, int destination)
    {
        if (Blocked || !Multiple || destination < 0 || destination >= Files.Count) return;
        var updated = Files.ToList();
        var index = updated.FindIndex(f => f.ClientId == id);
        if (index < 0 || index == destination) return;
        var image = updated[index];
        updated.RemoveAt(index);
        updated.Insert(destination, image);
        _activeId = id;
        await FilesChanged.InvokeAsync(updated);
        _announcement = string.Format(Strings.ImageUpload_Moved, ImageName(image), destination + 1);
        await FocusAsync(id);
    }

    private async Task FocusAsync(Guid? id)
    {
        if (_disposed) return;
        await InvokeAsync(StateHasChanged);
        var module = await ModuleAsync();
        if (module is not null) await module.InvokeVoidAsync("focusImage", _root, id?.ToString());
    }

    private Task<IJSObjectReference> ModuleAsync() => _moduleTask ??= JS.InvokeAsync<IJSObjectReference>("import", "./js/shopImageUpload.js").AsTask();
    private async Task RevokeAsync(string url)
    {
        if (!_ownedUrls.Remove(url)) return;
        var module = await ModuleAsync();
        if (module is not null) await module.InvokeVoidAsync("revokeObjectUrl", url);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _lifetime.CancelAsync();
        try
        {
            try { await _processing; }
            finally
            {
                foreach (var url in _ownedUrls.ToArray()) await RevokeAsync(url);
                if (_moduleTask is not null && await _moduleTask is { } module)
                {
                    await module.InvokeVoidAsync("unbindOrder", _root);
                    await module.DisposeAsync();
                }
            }
        }
        catch (JSDisconnectedException) { }
        finally { _reference?.Dispose(); _lifetime.Dispose(); }
    }
}
