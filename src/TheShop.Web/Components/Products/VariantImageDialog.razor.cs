using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Products;

/// <summary>
/// The variant image pin picker (FR-16, AC-10a; Figma node <c>2946:14712</c>): shows the
/// product's own gallery and, when this variant shares an option value with sibling variants,
/// an "Apply To" choice between pinning this variant alone or every variant sharing that value.
/// Owned by the variants card; cancellation is distinct from saving an empty selection.
/// </summary>
public partial class VariantImageDialog : ComponentBase
{
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>Appends browser files to the owning product gallery before the input resets.</summary>
    [Parameter] public EventCallback<IReadOnlyList<IBrowserFile>> AddGalleryFiles { get; set; }

    /// <summary>File rejection messages from the product's gallery owner.</summary>
    [Parameter] public IReadOnlyList<string> UploadErrors { get; set; } = [];

    private readonly string _uploadKey = $"{BusyKeys.ImageSelection}.{Guid.NewGuid():N}";
    private HashSet<Guid>? _imagesBeforeUpload;
    /// <summary>Completes once: null cancels; a result with null ImageId explicitly clears the pin.</summary>
    [Parameter, EditorRequired] public EventCallback<VariantImagePinResult?> OnCompleted { get; set; }

    /// <summary>The variant's option-value label (e.g. "Mango / 50mg"), shown in the dialog title.</summary>
    [Parameter, EditorRequired]
    public string VariantLabel { get; set; } = string.Empty;

    /// <summary>The product's own gallery to choose a pin from.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ProductImageDto> GalleryImages { get; set; } = [];

    /// <summary>The variant's current pin, if any.</summary>
    [Parameter]
    public Guid? CurrentImageId { get; set; }

    /// <summary>
    /// The shared option-value label to offer as a fan-out scope (e.g. "Color = Red"), or
    /// <c>null</c> when this variant's option value isn't shared by another variant.
    /// </summary>
    [Parameter]
    public string? SharedScopeLabel { get; set; }

    /// <summary>The number of variants — including this one — sharing <see cref="SharedScopeLabel"/>.</summary>
    [Parameter]
    public int SharedScopeCount { get; set; }

    private Guid? _selectedImageId;
    private bool _applyToAllSharing;
    private bool _completed;

    private bool ShowScopeChoice => SharedScopeLabel is not null && SharedScopeCount > 1;

    /// <inheritdoc/>
    protected override void OnInitialized() => _selectedImageId = CurrentImageId;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (_imagesBeforeUpload is not null && GalleryImages.FirstOrDefault(image => !_imagesBeforeUpload.Contains(image.Id)) is { } added)
        {
            _selectedImageId = added.Id;
            _imagesBeforeUpload = null;
        }
        if (_selectedImageId is Guid selected && !GalleryImages.Any(image => image.Id == selected))
            _selectedImageId = null;
        if (!ShowScopeChoice)
            _applyToAllSharing = false;
    }

    // Clicking the already-selected image deselects it — the dialog has no separate "clear" action.
    private void SelectImage(Guid imageId)
    {
        if (_completed || BusyState.IsBusy(_uploadKey)) return;
        _selectedImageId = _selectedImageId == imageId ? null : imageId;
    }

    private async Task AddFilesAsync(IReadOnlyList<IBrowserFile> files)
    {
        if (_completed || BusyState.IsBusy(_uploadKey) || !AddGalleryFiles.HasDelegate) return;
        _imagesBeforeUpload = GalleryImages.Select(image => image.Id).ToHashSet();
        await BusyState.RunAsync(_uploadKey, () => AddGalleryFiles.InvokeAsync(files));
    }

    private Task ConfirmAsync() => CompleteAsync(new VariantImagePinResult(_selectedImageId, ShowScopeChoice && _applyToAllSharing));

    private Task CancelAsync() => CompleteAsync(null);

    private Task CompleteAsync(VariantImagePinResult? result)
    {
        if (_completed || BusyState.IsBusy(_uploadKey))
            return Task.CompletedTask;
        _completed = true;
        return OnCompleted.InvokeAsync(result);
    }
}
