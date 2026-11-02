using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Admin;

/// <summary>Repeated fixed-tile composition using the existing upload and text controls.</summary>
public partial class FeaturedTileEditor : ComponentBase
{
    /// <summary>Fixed position, used for unique labels and input IDs.</summary>
    [Parameter, EditorRequired] public int Number { get; set; }
    /// <summary>The parent's accepted local tile draft.</summary>
    [Parameter, EditorRequired] public FeaturedTileFormModel Model { get; set; } = default!;
    /// <summary>Shows saved content without mutation controls.</summary>
    [Parameter] public bool ReadOnly { get; set; }
    /// <summary>Prevents editing during parent work.</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>Application-backed candidate validation before preview replacement.</summary>
    [Parameter] public Func<ShopUploadedImage, CancellationToken, Task<string?>>? ValidateFileAsync { get; set; }
    [CascadingParameter] private EditContext? EditContext { get; set; }
    private ShopImageUpload? _upload;
    private string TestId => $"featured-tile-{Number}";
    private string TitleId => $"{TestId}-heading";
    private string LabelId => $"{TestId}-label";
    private string TileTitleId => $"{TestId}-title";
    private string ButtonLabelId => $"{TestId}-button-label";
    private string ButtonLinkId => $"{TestId}-button-link";
    private string HintId => $"{TestId}-hint";
    private string ErrorId => $"{TestId}-image-error";
    private string ImageDescriptionIds => $"{HintId} {ErrorId}";
    private string TileName => string.Format(Strings.Featured_Tile, Number);
    private string RemoveLabel => string.Format(Strings.Featured_RemoveImage, Number);

    private void ImagesChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        if (ReadOnly) return;
        Model.Images = images;
        EditContext?.NotifyFieldChanged(new(Model, nameof(Model.Images)));
    }

    /// <summary>Waits for accepted file preparation before the parent constructs a command.</summary>
    public Task WaitForPendingFilesAsync() => _upload?.WaitForPendingFilesAsync() ?? Task.CompletedTask;
}
