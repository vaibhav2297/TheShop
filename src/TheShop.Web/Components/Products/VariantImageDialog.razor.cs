using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Web.Components.Products;

/// <summary>
/// The variant image pin picker (FR-16, AC-10a; Figma node <c>2680:14477</c>): shows the
/// product's own gallery and, when this variant shares an option value with sibling variants,
/// an "Apply To" choice between pinning this variant alone or every variant sharing that value.
/// Shown via <see cref="IDialogService.ShowAsync{TComponent}(DialogParameters)"/>.
/// </summary>
public partial class VariantImageDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

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

    private bool ShowScopeChoice => SharedScopeLabel is not null && SharedScopeCount > 1;

    // The selected tile is the only one that gets the primary-colored border — everything else stays flat.
    private static string ThumbnailClassname(bool selected) => new CssBuilder("cursor-pointer")
        .AddClass("border mud-border-primary", selected)
        .Build();

    /// <inheritdoc/>
    protected override void OnInitialized() => _selectedImageId = CurrentImageId;

    // Clicking the already-selected image deselects it — the dialog has no separate "clear" action.
    private void SelectImage(Guid imageId) =>
        _selectedImageId = _selectedImageId == imageId ? null : imageId;

    private void Confirm() =>
        MudDialog.Close(DialogResult.Ok(new VariantImagePinResult(_selectedImageId, _applyToAllSharing)));

    private void Cancel() => MudDialog.Cancel();
}
