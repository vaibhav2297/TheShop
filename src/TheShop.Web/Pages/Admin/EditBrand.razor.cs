using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Brands.Commands.UpdateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetBrandById;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Edits an existing brand, id-keyed (plan §5 Decision 8). Dispatches
/// <see cref="GetBrandByIdQuery"/> (<c>brands.view</c>) on load and <see cref="UpdateBrandCommand"/>
/// on save. Gated at the route level on <c>PolicyNames.AdminArea</c> via
/// <c>Pages/Admin/_Imports.razor</c>, and additionally on <c>brands.edit</c> within the page
/// itself — the page-level gate, not the query, is what denies a view-only admin's direct link
/// (plan §5 Decision 11, AC-16).
/// </summary>
[Route(Routes.Admin.EditBrandPattern)]
public partial class EditBrand : ComponentBase
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

    private bool _originalHadLogo;
    private bool _hasExistingLogo;
    private string? _existingLogoUrl;
    private IReadOnlyList<ShopUploadedImage> _newLogoImages = [];

    private readonly Func<string, string?> _nameValidation = name =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length > MaxNameLength
            ? Strings.Brand_NameTooLong
            : null;

    private readonly Func<string, string?> _descriptionValidation = description =>
        !string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength
            ? Strings.Brand_DescriptionTooLong
            : null;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageBrands_Heading, Routes.Admin.ManageBrands)
            .Current(Strings.EditBrand_Heading));

        await BusyState.RunAsync(BusyKeys.Brands.EditBrand, async () =>
        {
            var result = await Mediator.Send(new GetBrandByIdQuery(Id));

            if (!result.IsSuccess)
            {
                _notFound = true;
                return;
            }

            var brand = result.Value;
            _name = brand.Name;
            _description = brand.Description;
            _isActive = brand.IsActive;
            _existingLogoUrl = brand.LogoUrl;
            _originalHadLogo = brand.LogoUrl is not null;
            _hasExistingLogo = _originalHadLogo;
        });
    }

    private void OnNewLogoImagesChanged(IReadOnlyList<ShopUploadedImage> images) => _newLogoImages = images;

    private void RemoveExistingLogo()
    {
        _hasExistingLogo = false;
        _existingLogoUrl = null;
    }

    private async Task SaveAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid)
            return;

        await BusyState.RunAsync(BusyKeys.Brands.EditBrand, async () =>
        {
            var newLogo = _newLogoImages.Count > 0
                ? new BrandLogoUpload(_newLogoImages[0].Bytes, _newLogoImages[0].FileName, _newLogoImages[0].ContentType)
                : null;

            // The user removed the existing logo and did not pick a replacement in this save.
            var removeLogo = _originalHadLogo && !_hasExistingLogo && newLogo is null;

            var result = await Mediator.Send(new UpdateBrandCommand(
                Id,
                _name.Trim(),
                string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                _isActive,
                newLogo,
                removeLogo));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.EditBrand_Success, Severity.Success);
                Nav.NavigateTo(Routes.Admin.ManageBrands);
            }
            else
            {
                Snackbar.Add(Localizer[result.Error ?? nameof(Strings.Brand_UpdateFailed)], Severity.Error);
            }
        });
    }
}
