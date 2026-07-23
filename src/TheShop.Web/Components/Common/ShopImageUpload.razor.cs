using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable image upload with inline previews. Wraps <see cref="MudFileUpload{T}"/> with a dashed
/// dropzone activator and renders a preview thumbnail — each with a top-right remove button — for
/// every selected image. Supports both single (<see cref="Multiple"/> = <c>false</c>) and
/// multi-image selection; in multi mode the preview strip scrolls horizontally. Validates content
/// type and size against <see cref="AllowedContentTypes"/> / <see cref="MaxFileSize"/>, reads each
/// accepted file into a <see cref="ShopUploadedImage"/> (bytes + <c>data:</c> preview URL), and
/// surfaces the current selection through <c>@bind-Files</c>. Inherits from
/// <see cref="MudComponentBase"/> so consumers can forward <c>Class</c>, <c>Style</c>, and
/// arbitrary attributes to the root element. All user-facing text is supplied by the consumer as
/// already-localized strings.
/// </summary>
public partial class ShopImageUpload : MudComponentBase
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
    /// MIME types accepted after selection; anything else raises <see cref="InvalidTypeError"/>.
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
    /// Localized message shown when a file's content type is not allowed.
    /// </summary>
    [Parameter] public string? InvalidTypeError { get; set; }

    /// <summary>
    /// Localized message shown when a file exceeds <see cref="MaxFileSize"/>.
    /// </summary>
    [Parameter] public string? MaxFileSizeError { get; set; }

    /// <summary>
    /// Side length, in pixels, of each square preview thumbnail. Defaults to 120.
    /// </summary>
    [Parameter] public int PreviewSize { get; set; } = 120;

    /// <summary>
    /// Height, in pixels, of the dashed dropzone. Defaults to 200.
    /// </summary>
    [Parameter] public int DropzoneHeight { get; set; } = 200;

    /// <summary>
    /// When <c>true</c>, the dropzone and remove buttons are disabled.
    /// </summary>
    [Parameter] public bool Disabled { get; set; }

    #endregion

    #region State

    private MudFileUpload<IReadOnlyList<IBrowserFile>> _fileUpload = default!;
    private string? _error;

    // Guards against the FilesChanged callback that MudFileUpload raises when we reset it
    // after processing a batch (the reset exists so re-selecting the same file fires again).
    private bool _resetting;

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

    private string PreviewStyle => new StyleBuilder()
        .AddStyle("width", $"{PreviewSize}px")
        .AddStyle("height", $"{PreviewSize}px")
        .Build();

    #endregion

    #region Event Handlers

    private async Task OpenPickerAsync(MudFileUpload<IReadOnlyList<IBrowserFile>> upload)
    {
        if (Disabled)
            return;

        await upload.OpenFilePickerAsync();
    }

    private async Task OnFilesChangedAsync(IReadOnlyList<IBrowserFile>? files)
    {
        if (_resetting || files is null || files.Count == 0) return;

        _error = null;

        // In single mode a new selection replaces the current image; in multi mode it appends.
        var accepted = Multiple ? new List<ShopUploadedImage>(Files) : [];
        var remaining = EffectiveMaxFileCount - accepted.Count;

        foreach (var file in files)
        {
            if (remaining <= 0) break;

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                _error = InvalidTypeError;
                continue;
            }

            if (file.Size > MaxFileSize)
            {
                _error = MaxFileSizeError;
                continue;
            }

            accepted.Add(await ReadImageAsync(file));
            remaining--;
        }

        await FilesChanged.InvokeAsync(accepted);

        // Reset the input so selecting the same file again still raises FilesChanged.
        _resetting = true;
        await _fileUpload.ClearAsync();
        _resetting = false;
    }

    private async Task RemoveAsync(ShopUploadedImage image)
    {
        _error = null;
        var updated = Files.Where(f => !ReferenceEquals(f, image)).ToList();
        await FilesChanged.InvokeAsync(updated);
    }

    private async Task<ShopUploadedImage> ReadImageAsync(IBrowserFile file)
    {
        using var stream = file.OpenReadStream(MaxFileSize);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        var bytes = buffer.ToArray();
        var previewUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(bytes)}";
        return new ShopUploadedImage(bytes, file.Name, file.ContentType, previewUrl);
    }

    #endregion
}
