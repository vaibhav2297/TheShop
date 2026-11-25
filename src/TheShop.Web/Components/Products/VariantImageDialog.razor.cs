using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Products;

/// <summary>Stages an ordered variant gallery from the shared product image library.</summary>
public partial class VariantImageDialog : ComponentBase
{
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>Adds new files to the pending product library.</summary>
    [Parameter] public EventCallback<IReadOnlyList<IBrowserFile>> AddGalleryFiles { get; set; }
    /// <summary>Upload errors displayed beside the library.</summary>
    [Parameter] public IReadOnlyList<string> UploadErrors { get; set; } = [];
    /// <summary>Reports Apply or Cancel to the owning variants card.</summary>
    [Parameter, EditorRequired] public EventCallback<VariantImageSelectionResult?> OnCompleted { get; set; }
    /// <summary>Variant name shown in the dialog.</summary>
    [Parameter, EditorRequired] public string VariantLabel { get; set; } = string.Empty;
    /// <summary>Current images in the product library.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<ProductImageDto> GalleryImages { get; set; } = [];
    /// <summary>Ordered images assigned to the edited variant.</summary>
    [Parameter] public IReadOnlyList<Guid> CurrentImageIds { get; set; } = [];
    /// <summary>Identity of the edited variant.</summary>
    [Parameter] public Guid CurrentVariantId { get; set; }
    /// <summary>Option value shared by the variants eligible for bulk assignment.</summary>
    [Parameter] public string? SharedScopeLabel { get; set; }
    /// <summary>Variants sharing the selected option value.</summary>
    [Parameter] public IReadOnlyList<Guid> SharedVariantIds { get; set; } = [];
    /// <summary>Last applied scope for the shared option value during the current edit session.</summary>
    [Parameter] public bool InitiallyApplyToAllSharing { get; set; }

    private readonly string _uploadKey = $"{BusyKeys.ImageSelection}.{Guid.NewGuid():N}";
    private readonly List<Guid> _selected = [];
    private HashSet<Guid>? _imagesBeforeUpload;
    private bool _initialized, _bulk, _completed;
    private bool ShowScopeChoice => SharedScopeLabel is not null && SharedVariantIds.Count > 1;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _selected.AddRange(CurrentImageIds);
            _bulk = InitiallyApplyToAllSharing && ShowScopeChoice;
            _initialized = true;
        }
        if (_imagesBeforeUpload is not null)
        {
            foreach (var added in GalleryImages.Where(image => !_imagesBeforeUpload.Contains(image.Id)))
                if (!_selected.Contains(added.Id)) _selected.Add(added.Id);
            if (GalleryImages.Count > _imagesBeforeUpload.Count) _imagesBeforeUpload = null;
        }
        var available = GalleryImages.Select(image => image.Id).ToHashSet();
        _selected.RemoveAll(id => !available.Contains(id));
    }

    private void SelectImage(Guid id)
    {
        if (_completed || BusyState.IsBusy(_uploadKey)) return;
        if (!_selected.Remove(id)) _selected.Add(id);
    }

    private async Task AddFilesAsync(IReadOnlyList<IBrowserFile> files)
    {
        if (_completed || BusyState.IsBusy(_uploadKey) || !AddGalleryFiles.HasDelegate) return;
        _imagesBeforeUpload = GalleryImages.Select(image => image.Id).ToHashSet();
        await BusyState.RunAsync(_uploadKey, () => AddGalleryFiles.InvokeAsync(files));
    }

    private Task ConfirmAsync()
    {
        IReadOnlyList<Guid> targets = _bulk && ShowScopeChoice ? SharedVariantIds : [CurrentVariantId];
        return CompleteAsync(new VariantImageSelectionResult(_selected.ToArray(), targets, _bulk && ShowScopeChoice));
    }

    private Task CancelAsync() => CompleteAsync(null);

    private Task CompleteAsync(VariantImageSelectionResult? result)
    {
        if (_completed || BusyState.IsBusy(_uploadKey)) return Task.CompletedTask;
        _completed = true;
        return OnCompleted.InvokeAsync(result);
    }
}
