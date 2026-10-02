using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Shows one image of a gallery large, with every image as a selectable preview beneath it. The
/// caller owns which image is selected through <see cref="SelectedImageId"/> and
/// <see cref="SelectedImageIdChanged"/>; the gallery owns only how far its preview strip is
/// scrolled, and its previous/next controls scroll that strip without changing the selection.
/// An empty gallery shows <see cref="PlaceholderLabel"/> in the large frame and no previews.
/// Inherits from <see cref="MudComponentBase"/> so <c>Class</c>, <c>Style</c>, and arbitrary
/// attributes reach the root.
/// </summary>
public partial class ShopImageGallery : MudComponentBase, IAsyncDisposable
{
    #region Parameters

    /// <summary>
    /// The gallery images in display order.
    /// </summary>
    [Parameter, EditorRequired] public IReadOnlyList<ProductImageDto> Images { get; set; } = [];

    /// <summary>
    /// The image shown large. An id outside <see cref="Images"/> — or <c>null</c> — shows the
    /// first image.
    /// </summary>
    [Parameter] public Guid? SelectedImageId { get; set; }

    /// <summary>
    /// Raised with the image id when a preview is activated.
    /// </summary>
    [Parameter] public EventCallback<Guid?> SelectedImageIdChanged { get; set; }

    /// <summary>
    /// Localized description of the large image.
    /// </summary>
    [Parameter] public string ImageAlt { get; set; } = string.Empty;

    /// <summary>
    /// Localized label shown when an image is missing or cannot be displayed.
    /// </summary>
    [Parameter, EditorRequired] public string PlaceholderLabel { get; set; } = string.Empty;

    #endregion

    #region State

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Stable per instance so the JS bridge can find the preview strip without an element
    // reference (the strip is a MudStack, which exposes none).
    private readonly string _windowId = $"shop-image-gallery-{Guid.NewGuid():N}";

    private IJSObjectReference? _jsModule;
    private Guid? _revealedImageId;

    private ProductImageDto? SelectedImage =>
        Images.FirstOrDefault(i => i.Id == SelectedImageId) ?? Images.FirstOrDefault();

    private bool CanNavigate => Images.Count > 1;

    private Dictionary<string, object?> WindowAttributes => new() { ["data-shop-image-gallery"] = _windowId };

    #endregion

    #region CSS Forwarding

    protected string Classname => new CssBuilder("shop-image-gallery")
        .AddClass(Class)
        .Build();

    protected string Stylename => new StyleBuilder()
        .AddStyle(Style)
        .Build();

    private static string PreviewClassname(bool isSelected) =>
        new CssBuilder("shop-image-gallery__preview")
            .AddClass("mud-tertiary")
            .AddClass("shop-image-gallery__preview--selected", isSelected)
            .Build();

    #endregion

    #region Lifecycle

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopImageGallery.js");

        // Keep the selected preview in view when the selection changes from outside the strip,
        // such as a variant switch selecting its linked image.
        var selectedId = SelectedImage?.Id;
        if (_jsModule is null || selectedId == _revealedImageId)
            return;

        _revealedImageId = selectedId;
        await _jsModule.InvokeVoidAsync("reveal", _windowId);
    }

    #endregion

    #region Event Handlers

    private Task SelectAsync(Guid imageId) => SelectedImageIdChanged.InvokeAsync(imageId);

    private Task ScrollPreviousAsync() => ScrollAsync(-1);

    private Task ScrollNextAsync() => ScrollAsync(1);

    private async Task ScrollAsync(int direction)
    {
        if (_jsModule is not null)
            await _jsModule.InvokeVoidAsync("scrollPage", _windowId, direction);
    }

    #endregion

    #region Disposal

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is null)
            return;

        try
        {
            await _jsModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The runtime is already gone; the module went with the page.
        }
    }

    #endregion
}
