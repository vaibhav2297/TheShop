using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
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
/// Edits an existing category, id-keyed (plan §5 Decision 8). Dispatches
/// <see cref="GetCategoryByIdQuery"/> (<c>categories.view</c>) on load and
/// <see cref="UpdateCategoryCommand"/> on save. Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>categories.edit</c> within the page
/// itself — the page-level gate, not the query, is what denies a view-only admin's direct link
/// (plan §5 Decision 9, AC-20).
/// </summary>
[Route(Routes.Admin.EditCategoryPattern)]
[AuthorizePermission("categories.edit")]
public partial class EditCategory : ComponentBase
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private MudForm _form = default!;
    private string _name = string.Empty;
    private string? _description;
    private bool _isActive = true;
    private bool _isFormValid;
    private bool _notFound;

    private bool _originalHadImage;
    private bool _hasExistingImage;
    private string? _existingImageUrl;
    private IReadOnlyList<ShopUploadedImage> _newCategoryImages = [];

    private readonly Func<string, string?> _nameValidation = name =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length > MaxNameLength
            ? Strings.Category_NameTooLong
            : null;

    private readonly Func<string, string?> _descriptionValidation = description =>
        !string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength
            ? Strings.Category_DescriptionTooLong
            : null;

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
            _name = category.Name;
            _description = category.Description;
            _isActive = category.IsActive;
            _existingImageUrl = category.ImageUrl;
            _originalHadImage = category.ImageUrl is not null;
            _hasExistingImage = _originalHadImage;
        });
    }

    private void OnNewCategoryImagesChanged(IReadOnlyList<ShopUploadedImage> images) => _newCategoryImages = images;

    private void RemoveExistingImage()
    {
        _hasExistingImage = false;
        _existingImageUrl = null;
    }

    private async Task SaveAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid)
            return;

        await BusyState.RunAsync(BusyKeys.Categories.EditCategory, async () =>
        {
            var newImage = _newCategoryImages.Count > 0
                ? new CategoryImageUpload(_newCategoryImages[0].Bytes, _newCategoryImages[0].FileName, _newCategoryImages[0].ContentType)
                : null;

            // The user removed the existing image and did not pick a replacement in this save.
            var removeImage = _originalHadImage && !_hasExistingImage && newImage is null;

            var result = await Mediator.Send(new UpdateCategoryCommand(
                Id,
                _name.Trim(),
                string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                _isActive,
                newImage,
                removeImage));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.EditCategory_Success, Severity.Success);
                Nav.NavigateTo(Routes.Admin.ManageCategories);
            }
            else
            {
                Snackbar.Add(Localizer[result.Error ?? nameof(Strings.Category_UpdateFailed)], Severity.Error);
            }
        });
    }
}
