using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Web.Components.Categories;
using TheShop.Application.Features.Categories.Commands.CreateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Creates a category through <see cref="CreateCategoryCommand"/> and returns to the
/// category list on success. Requires the categories.create permission.
/// </summary>
[Route(Routes.Admin.AddCategory)]
[AuthorizePermission("categories.create")]
public partial class AddCategory : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private CategoryForm _form = default!;
    private readonly CategoryFormModel _model = new();

    private IReadOnlyList<ShopUploadedImage> _categoryImages = [];

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageCategories_Heading, Routes.Admin.ManageCategories)
            .Current(Strings.Categories_CreateHeading));
    }

    private void OnCategoryImagesChanged(IReadOnlyList<ShopUploadedImage> images) => _categoryImages = images;

    private async Task SaveAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Categories.AddCategory)) return;
        _form.ClearErrors();

        await BusyState.RunAsync(BusyKeys.Categories.AddCategory, async () =>
        {
            await _form.WaitForPendingFilesAsync();

            // A row still showing a validation error (wrong type / too large) never leaves the client.
            var validCategoryImages = _categoryImages.Where(image => image.Error is null).ToList();
            var image = validCategoryImages.Count > 0
                ? new CategoryImageUpload(validCategoryImages[0].Bytes, validCategoryImages[0].FileName, validCategoryImages[0].ContentType)
                : null;

            var result = await Mediator.Send(new CreateCategoryCommand(
                _model.Name?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                image,
                _model.IsActive));

            if (result.IsSuccess)
            {
                Notifications.Show(Strings.Category_Created, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageCategories);
            }
            else if (result.ValidationErrors.Count > 0)
            {
                _form.ShowErrors(result.ValidationErrors);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Category_CreateFailed)], ShopNotificationKind.Error);
            }
        });
    }
}
