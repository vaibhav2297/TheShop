using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Web.Components.Categories;
using TheShop.Application.Features.Categories.Commands.UpdateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetCategoryById;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Loads the category addressed by the route and saves edits through
/// <see cref="UpdateCategoryCommand"/>. Requires the categories.edit permission.
/// </summary>
[Route(Routes.Admin.EditCategoryPattern)]
[AuthorizePermission("categories.edit")]
public partial class EditCategory : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    /// <summary>The category identifier supplied by the edit route.</summary>
    [Parameter] public Guid Id { get; set; }

    private CategoryForm _form = default!;
    private readonly CategoryFormModel _model = new();
    private bool _notFound;
    private bool _loaded;

    private bool _originalHadImage;
    private bool _hasExistingImage;
    private string? _existingImageUrl;
    private IReadOnlyList<ShopUploadedImage> _newCategoryImages = [];

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageCategories_Heading, Routes.Admin.ManageCategories)
            .Current(Strings.EditCategory_Heading));

        await BusyState.RunAsync(BusyKeys.Categories.EditCategory, async () =>
        {
            var result = await Mediator.Send(new GetCategoryByIdQuery(Id));

            if (!result.IsSuccess)
            {
                _notFound = true;
                return;
            }

            var category = result.Value;
            _loaded = true;
            _model.Name = category.Name;
            _model.Description = category.Description;
            _model.IsActive = category.IsActive;
            _existingImageUrl = category.ImageUrl;
            _originalHadImage = category.ImageUrl is not null;
            _hasExistingImage = _originalHadImage;
        });
    }

    private IReadOnlyList<ShopUploadedImage> DisplayedImages =>
        _hasExistingImage && _newCategoryImages.Count == 0 && _existingImageUrl is not null
            ? [ShopUploadedImage.Existing(Id, _existingImageUrl)] : _newCategoryImages;

    private void OnNewCategoryImagesChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        var removingRejectedReplacement = _hasExistingImage && _newCategoryImages.Count > 0 && images.Count == 0;
        _newCategoryImages = images.Where(image => !image.IsExisting).ToList();
        if (!removingRejectedReplacement && !images.Any(image => image.IsExisting)
            && (images.Count == 0 || images.Any(image => image.Error is null)))
            RemoveExistingImage();
    }

    private void RemoveExistingImage()
    {
        _hasExistingImage = false;
        _existingImageUrl = null;
    }

    private async Task SaveAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Categories.EditCategory)) return;
        _form.ClearErrors();

        await BusyState.RunAsync(BusyKeys.Categories.EditCategory, async () =>
        {
            await _form.WaitForPendingFilesAsync();
            // A row still showing a validation error (wrong type / too large) never leaves the client.
            var validNewCategoryImages = _newCategoryImages.Where(image => image.Error is null).ToList();
            var newImage = validNewCategoryImages.Count > 0
                ? new CategoryImageUpload(validNewCategoryImages[0].Bytes, validNewCategoryImages[0].FileName, validNewCategoryImages[0].ContentType)
                : null;

            // The user removed the existing image and did not pick a replacement in this save.
            var removeImage = _originalHadImage && !_hasExistingImage && newImage is null;

            var result = await Mediator.Send(new UpdateCategoryCommand(
                Id,
                _model.Name?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                _model.IsActive,
                newImage,
                removeImage));

            if (result.IsSuccess)
            {
                Notifications.Show(Strings.EditCategory_Success, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageCategories);
            }
            else if (result.ValidationErrors.Count > 0)
            {
                _form.ShowErrors(result.ValidationErrors);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Category_UpdateFailed)], ShopNotificationKind.Error);
            }
        });
    }
}
