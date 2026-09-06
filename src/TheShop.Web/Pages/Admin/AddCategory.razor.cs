using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Categories.Commands.CreateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Creates a new category (Figma node <c>2629:1874</c>). Dispatches
/// <see cref="CreateCategoryCommand"/>, seeding the status control Active by default
/// (plan §5 Decision 11, RULE-5); on success returns to <see cref="Routes.Admin.ManageCategories"/>
/// (Flow 2). Requires a signed-in user via <c>Pages/Admin/_Imports.razor</c>, and is gated on
/// <c>categories.create</c> within the page itself.
/// </summary>
[Route(Routes.Admin.AddCategory)]
[AuthorizePermission("categories.create")]
public partial class AddCategory : ComponentBase
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private MudForm _form = default!;
    private string _name = string.Empty;
    private string? _description;
    private bool _isActive = true;
    private bool _isFormValid;

    private IReadOnlyList<ShopUploadedImage> _categoryImages = [];

    private readonly Func<string, string?> _nameValidation = name =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length > MaxNameLength
            ? Strings.Category_NameTooLong
            : null;

    private readonly Func<string, string?> _descriptionValidation = description =>
        !string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength
            ? Strings.Category_DescriptionTooLong
            : null;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageCategories_Heading, Routes.Admin.ManageCategories)
            .Current(Strings.AddCategory_Heading));
    }

    private void OnCategoryImagesChanged(IReadOnlyList<ShopUploadedImage> images) => _categoryImages = images;

    private async Task SaveAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid) return;

        await BusyState.RunAsync(BusyKeys.Categories.AddCategory, async () =>
        {
            // A row still showing a validation error (wrong type / too large) never leaves the client.
            var validCategoryImages = _categoryImages.Where(image => image.Error is null).ToList();
            var image = validCategoryImages.Count > 0
                ? new CategoryImageUpload(validCategoryImages[0].Bytes, validCategoryImages[0].FileName, validCategoryImages[0].ContentType)
                : null;

            var result = await Mediator.Send(new CreateCategoryCommand(
                _name.Trim(),
                string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                image,
                _isActive));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.Category_Created, Severity.Success);
                Nav.NavigateTo(Routes.Admin.ManageCategories);
            }
            else
            {
                Snackbar.Add(Localizer[result.Error ?? nameof(Strings.Category_CreateFailed)], Severity.Error);
            }
        });
    }
}
