using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.Commands.UpdateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetBrandById;
using TheShop.Web.Auth;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Common;
using TheShop.Web.Components.Brands;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Edits an existing brand, id-keyed (plan §5 Decision 8). Dispatches
/// <see cref="GetBrandByIdQuery"/> (<c>brands.view</c>) on load and <see cref="UpdateBrandCommand"/>
/// on save. Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>brands.edit</c> within the page
/// itself — the page-level gate, not the query, is what denies a view-only admin's direct link
/// (plan §5 Decision 11, AC-16).
/// </summary>
[Route(Routes.Admin.EditBrandPattern)]
[AuthorizePermission("brands.edit")]
public partial class EditBrand : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private BrandForm _form = default!;
    private readonly BrandFormModel _model = new();
    private bool _notFound;
    private bool _loaded;

    private bool _originalHadLogo;
    private bool _hasExistingLogo;
    private string? _existingLogoUrl;
    private IReadOnlyList<ShopUploadedImage> _newLogoImages = [];

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
            Breadcrumbs.Set(BreadcrumbTrail.Admin()
                .Add(Strings.ManageBrands_Heading, Routes.Admin.ManageBrands)
                .Current(brand.Name));
            _loaded = true;
            _model.Name = brand.Name;
            _model.Description = brand.Description;
            _model.IsActive = brand.IsActive;
            _existingLogoUrl = brand.LogoUrl;
            _originalHadLogo = brand.LogoUrl is not null;
            _hasExistingLogo = _originalHadLogo;
        });
    }

    private IReadOnlyList<ShopUploadedImage> DisplayedLogoImages =>
        _hasExistingLogo && _newLogoImages.Count == 0 && _existingLogoUrl is not null
            ? [ShopUploadedImage.Existing(Id, _existingLogoUrl)] : _newLogoImages;

    private void OnNewLogoImagesChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        var removingRejectedReplacement = _hasExistingLogo && _newLogoImages.Count > 0 && images.Count == 0;
        _newLogoImages = images.Where(image => !image.IsExisting).ToList();
        if (!removingRejectedReplacement && !images.Any(image => image.IsExisting)
            && (images.Count == 0 || images.Any(image => image.Error is null)))
            RemoveExistingLogo();
    }

    private void RemoveExistingLogo()
    {
        _hasExistingLogo = false;
        _existingLogoUrl = null;
    }

    private async Task SaveAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Brands.EditBrand)) return;
        _form.ClearErrors();

        await BusyState.RunAsync(BusyKeys.Brands.EditBrand, async () =>
        {
            await _form.WaitForPendingFilesAsync();
            // A row still showing a validation error (wrong type / too large) never leaves the client.
            var validNewLogoImages = _newLogoImages.Where(image => image.Error is null).ToList();
            var newLogo = validNewLogoImages.Count > 0
                ? new ImageUpload(validNewLogoImages[0].Bytes, validNewLogoImages[0].FileName, validNewLogoImages[0].ContentType)
                : null;

            // The user removed the existing logo and did not pick a replacement in this save.
            var removeLogo = _originalHadLogo && !_hasExistingLogo && newLogo is null;

            var result = await Mediator.Send(new UpdateBrandCommand(
                Id,
                _model.Name?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                _model.IsActive,
                newLogo,
                removeLogo));

            if (result.IsSuccess)
            {
                Notifications.Show(Strings.EditBrand_Success, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageBrands);
            }
            else if (result.ValidationErrors.Count > 0)
            {
                _form.ShowErrors(result.ValidationErrors);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Brand_UpdateFailed)], ShopNotificationKind.Error);
            }
        });
    }
}
