using Microsoft.AspNetCore.Components;
using TheShop.Application.Common.Models;
using TheShop.Web.Components.Common;

namespace TheShop.Web.Components.Categories;

/// <summary>Shared category fields, upload control, validation feedback, and form actions.</summary>
public partial class CategoryForm : ComponentBase
{
    /// <summary>Values edited by this form; request validation belongs to Application.</summary>
    [Parameter, EditorRequired] public CategoryFormModel Model { get; set; } = default!;

    /// <summary>Disables editing and actions while the owning page is busy.</summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>Current uploaded or stored image previews.</summary>
    [Parameter] public IReadOnlyList<ShopUploadedImage> Images { get; set; } = [];

    /// <summary>Raised when the user changes the image selection.</summary>
    [Parameter] public EventCallback<IReadOnlyList<ShopUploadedImage>> ImagesChanged { get; set; }

    /// <summary>Dispatches submission to the route's create or update workflow.</summary>
    [Parameter] public EventCallback OnSave { get; set; }

    /// <summary>Prefix for the route's stable input and save test identifiers.</summary>
    [Parameter] public string TestIdPrefix { get; set; } = "category";

    private ShopImageUpload? _imageUpload;
    private ShopCommandValidation _validation = default!;
    private string NameTestId => $"{TestIdPrefix}-name";
    private string SaveTestId => $"{TestIdPrefix}-save";

    /// <summary>Clears previous command validation messages before submission.</summary>
    public void ClearErrors() => _validation.Clear();

    /// <summary>Maps returned command failures onto the shared form fields.</summary>
    public void ShowErrors(IReadOnlyList<FieldValidationError> errors) => _validation.ShowErrors(errors);

    /// <summary>Completes pending file reads before the page maps its command.</summary>
    public Task WaitForPendingFilesAsync() => _imageUpload?.WaitForPendingFilesAsync() ?? Task.CompletedTask;
}
