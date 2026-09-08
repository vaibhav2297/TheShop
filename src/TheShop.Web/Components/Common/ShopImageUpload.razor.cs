using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable image upload rendered as a dashed dropzone activator plus a list of preview rows, one
/// per selected image. Supports both single (<see cref="Multiple"/> = <c>false</c>) and multi-image
/// selection; in multi mode the first row is always treated as the primary image
/// (<see cref="ShowPrimaryBadge"/>) — selection order decides it, and the only way to change it is to
/// remove and re-add images in the desired order. Every selected file is read into a
/// <see cref="ShopUploadedImage"/> (bytes + a browser object-URL preview) and kept in the selection
/// regardless of whether it passes the <see cref="AllowedContentTypes"/> / <see cref="MaxFileSize"/>
/// guard — a failing entry carries its localized <see cref="ShopUploadedImage.Error"/> and renders
/// that message on its own row instead of a size caption, so a consumer must filter
/// <c>Error is null</c> before using the selection for anything but display. The current selection
/// surfaces through <c>@bind-Files</c>. A consumer may seed <see cref="Files"/> with
/// <see cref="ShopUploadedImage.Existing"/> entries so already-stored images take part in the same
/// selection. Inherits from <see cref="MudComponentBase"/> so consumers can forward <c>Class</c>,
/// <c>Style</c>, and arbitrary attributes to the root element. All user-facing text is supplied by
/// the consumer as already-localized strings.
/// </summary>
public partial class ShopImageUpload : MudComponentBase, IAsyncDisposable
{
    #region Parameters

    /// <summary>
    /// The currently selected images. Use with <c>@bind-Files</c>.
    /// </summary>
    [Parameter] public IReadOnlyList<ShopUploadedImage> Files { get; set; } = [];

    /// <summary>
    /// Fires with the new selection whenever an image is added or removed.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyList<ShopUploadedImage>> FilesChanged { get; set; }

    /// <summary>
    /// When <c>true</c>, multiple images may be selected; otherwise a single image replaces the current one.
    /// </summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>
    /// Upper bound on retained images in multi mode. Ignored when <see cref="Multiple"/> is <c>false</c> (capped at 1).
    /// </summary>
    [Parameter] public int MaxFileCount { get; set; } = 10;

    /// <summary>
    /// The <c>accept</c> filter passed to the underlying file input.
    /// </summary>
    [Parameter] public string Accept { get; set; } = ".png,.jpg,.jpeg,.webp";

    /// <summary>
    /// MIME types accepted after selection; anything else flags the row with <see cref="InvalidTypeError"/>.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<string> AllowedContentTypes { get; set; } =
        ["image/png", "image/jpeg", "image/webp"];

    /// <summary>
    /// Maximum size, in bytes, of a single image. Defaults to 2 MB.
    /// </summary>
    [Parameter] public long MaxFileSize { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Localized label shown inside the dropzone.
    /// </summary>
    [Parameter] public string? UploadText { get; set; }

    /// <summary>
    /// Localized ARIA label for each preview's remove button.
    /// </summary>
    [Parameter] public string? RemoveLabel { get; set; }

    /// <summary>
    /// Localized alt text applied to every preview image.
    /// </summary>
    [Parameter] public string? PreviewAlt { get; set; }

    /// <summary>
    /// When <c>true</c>, the first image in the selection is badged with <see cref="PrimaryLabel"/>
    /// to show that position — not a separate flag — is what makes an image primary.
    /// </summary>
    [Parameter] public bool ShowPrimaryBadge { get; set; }

    /// <summary>
    /// Localized text of the badge shown on the first image when <see cref="ShowPrimaryBadge"/> is set.
    /// </summary>
    [Parameter] public string? PrimaryLabel { get; set; }

    /// <summary>
    /// Localized message shown on a row whose file's content type is not allowed.
    /// </summary>
    [Parameter] public string? InvalidTypeError { get; set; }

    /// <summary>
    /// Localized message shown on a row whose file exceeds <see cref="MaxFileSize"/>.
    /// </summary>
    [Parameter] public string? MaxFileSizeError { get; set; }

    /// <summary>
    /// Side length, in pixels, of each row's square thumbnail. Defaults to 32.
    /// </summary>
    [Parameter] public int PreviewSize { get; set; } = 32;

    /// <summary>
    /// Height, in pixels, of the dashed dropzone. Defaults to 200.
    /// </summary>
    [Parameter] public int DropzoneHeight { get; set; } = 200;

    /// <summary>
    /// When <c>true</c>, the dropzone and remove buttons are disabled.
    /// </summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Localized composite format (e.g. <c>"{0} KB"</c>) used to render a newly picked file's size
    /// below its name. A <c>null</c> value, or an entry with no <see cref="ShopUploadedImage.Bytes"/>
    /// (an already-stored image), omits the size line.
    /// </summary>
    [Parameter] public string? FileSizeFormat { get; set; }

    #endregion

    #region State

    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private MudFileUpload<IReadOnlyList<IBrowserFile>> _fileUpload = default!;
    private IJSObjectReference? _jsModule;

    // Guards against the FilesChanged callback that MudFileUpload raises when we reset it
    // after processing a batch (the reset exists so re-selecting the same file fires again).
    private bool _resetting;

    // Unique per instance so two ShopImageUpload components on the same page (e.g. a gallery and a
    // variant dialog) never share a busy signal — each only reports its own picking as in flight.
    private readonly string _busyKey = $"shop-image-upload.{Guid.NewGuid():N}";

    // Number of files from the current pick still being read into the selection — drives the
    // skeleton placeholders rendered after the real rows, one per file still in flight.
    private int _pendingCount;

    private int EffectiveMaxFileCount => Multiple ? MaxFileCount : 1;

    #endregion

    #region CSS Forwarding

    protected string Classname => new CssBuilder("shop-image-upload")
        .AddClass(Class)
        .Build();

    protected string Stylename => new StyleBuilder()
        .AddStyle(Style)
        .Build();

    private string DropzoneStyle => new StyleBuilder()
        .AddStyle("height", $"{DropzoneHeight}px")
        .Build();

    private string PreviewClassname => new CssBuilder("preview")
        .AddClass("border")
        .AddClass("rounded-0")
        .AddClass("pa-4")
        .AddClass("mud-border-lines-default")
        .Build();

    private string ThumbnailStyle => new StyleBuilder()
        .AddStyle("width", $"{PreviewSize}px")
        .AddStyle("height", $"{PreviewSize}px")
        .Build();

    #endregion

    #region Event Handlers

    private async Task OpenPickerAsync(MudFileUpload<IReadOnlyList<IBrowserFile>> upload, bool picking)
    {
        if (Disabled || picking)
            return;

        await upload.OpenFilePickerAsync();
    }

    private async Task OnFilesChangedAsync(IReadOnlyList<IBrowserFile>? files)
    {
        if (_resetting || files is null || files.Count == 0) return;

        var priorCount = Multiple ? Files.Count : 0;
        _pendingCount = Math.Min(files.Count, EffectiveMaxFileCount - priorCount);

        try
        {
            // Reading each file is async (the whole file is buffered into bytes), so the dropzone
            // routes through the shared busy state to guard against re-entrant picks while a batch
            // is still being read.
            await BusyState.RunAsync(_busyKey, async () =>
            {
                // In single mode a new selection replaces the current image — its object URL(s), if
                // any, are no longer referenced by anything and must be released explicitly.
                if (!Multiple)
                    await RevokeAllAsync(Files);

                var accepted = Multiple ? new List<ShopUploadedImage>(Files) : [];
                var remaining = EffectiveMaxFileCount - accepted.Count;

                foreach (var file in files)
                {
                    if (remaining <= 0) break;

                    // A failing file still becomes a row — carrying its own error — rather than being
                    // silently dropped, so the selection stays visible until the user removes it.
                    var error = !AllowedContentTypes.Contains(file.ContentType)
                        ? InvalidTypeError
                        : file.Size > MaxFileSize
                            ? MaxFileSizeError
                            : null;

                    accepted.Add(await ReadImageAsync(file, error));
                    remaining--;
                }

                await FilesChanged.InvokeAsync(accepted);
            });
        }
        finally
        {
            _pendingCount = 0;
        }

        // Reset the input so selecting the same file again still raises FilesChanged.
        _resetting = true;
        await _fileUpload.ClearAsync();
        _resetting = false;
    }

    private async Task RemoveAsync(ShopUploadedImage image)
    {
        await RevokeAsync(image);
        var updated = Files.Where(f => f.ClientId != image.ClientId).ToList();
        await FilesChanged.InvokeAsync(updated);
    }

    private bool IsPrimary(ShopUploadedImage image) => IndexOf(image) == 0;

    // Only a valid, newly picked file carries bytes to size; an invalid entry shows its error
    // instead, and an already-stored entry has no bytes to report.
    private string? FormatFileSize(ShopUploadedImage image) =>
        image.Error is null && image.Bytes.Length > 0 && FileSizeFormat is not null
            ? string.Format(FileSizeFormat, (image.Bytes.Length / 1024.0).ToString("F2"))
            : null;

    // Two picks of the same file are equal by record value, so position is resolved by the entry's
    // own ClientId rather than by equality.
    private int IndexOf(ShopUploadedImage image)
    {
        for (var i = 0; i < Files.Count; i++)
        {
            if (Files[i].ClientId == image.ClientId)
                return i;
        }

        return -1;
    }

    // Reads the full file regardless of MaxFileSize so an oversized entry still gets a real
    // thumbnail on its row — the browser already holds the whole file in memory either way, since
    // it was read from disk in full the moment the user picked it.
    private async Task<ShopUploadedImage> ReadImageAsync(IBrowserFile file, string? error)
    {
        using var stream = file.OpenReadStream(file.Size);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        var bytes = buffer.ToArray();
        var previewUrl = await CreatePreviewUrlAsync(bytes, file.ContentType);
        return new ShopUploadedImage(bytes, file.Name, file.ContentType, previewUrl) { Error = error };
    }

    // An object URL keeps a picked file's bytes out of the render tree entirely — unlike a
    // data: URI, which would otherwise hold the whole file a second time as a giant string that
    // Blazor re-diffs on every unrelated re-render, this is a short handle the browser resolves
    // internally. The bytes are transferred via a stream reference so they never cross the JS
    // interop boundary as base64-encoded JSON either.
    private async Task<string> CreatePreviewUrlAsync(byte[] bytes, string contentType)
    {
        var module = await GetModuleAsync();
        using var streamRef = new DotNetStreamReference(new MemoryStream(bytes));
        return await module.InvokeAsync<string>("createObjectUrl", streamRef, contentType);
    }

    private Task RevokeAllAsync(IEnumerable<ShopUploadedImage> images) =>
        Task.WhenAll(images.Select(RevokeAsync));

    // Only a newly picked file's preview is an object URL we created (and must release); an
    // already-stored image's preview is the server's own URL.
    private async Task RevokeAsync(ShopUploadedImage image)
    {
        if (image.IsExisting) return;

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("revokeObjectUrl", image.PreviewUrl);
    }

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _jsModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopImageUpload.js");

    #endregion

    #region Disposal

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is null) return;

        // The component going away means nothing else can still be showing these previews, so
        // their object URLs are released here rather than left to leak for the rest of the SPA
        // session (unlike a full page load, navigating within a Blazor WASM app never reclaims them).
        await RevokeAllAsync(Files.Where(image => !image.IsExisting));
        await _jsModule.DisposeAsync();
    }

    #endregion
}
