using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.Commands.CreateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Web.Auth;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Common;
using TheShop.Web.Components.Brands;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Creates a new brand (Figma node <c>2465:1128</c>). Dispatches
/// <see cref="CreateBrandCommand"/>; on success returns to
/// <see cref="Routes.Admin.ManageBrands"/> (Flow 1). Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on
/// <c>brands.create</c> within the page itself.
/// </summary>
[Route(Routes.Admin.AddBrand)]
[AuthorizePermission("brands.create")]
public partial class AddBrand : ComponentBase
{

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private BrandForm _form = default!;
    private readonly BrandFormModel _model = new();

    private IReadOnlyList<ShopUploadedImage> _logoImages = [];

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageBrands_Heading, Routes.Admin.ManageBrands)
            .Current(Strings.AddBrand_Heading));
    }

    private void OnLogoImagesChanged(IReadOnlyList<ShopUploadedImage> images) => _logoImages = images;

    private async Task SaveAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Brands.AddBrand)) return;
        _form.ClearErrors();

        await BusyState.RunAsync(BusyKeys.Brands.AddBrand, async () =>
        {
            await _form.WaitForPendingFilesAsync();

            // A row still showing a validation error (wrong type / too large) never leaves the client.
            var validLogoImages = _logoImages.Where(image => image.Error is null).ToList();
            var logo = validLogoImages.Count > 0
                ? new ImageUpload(validLogoImages[0].Bytes, validLogoImages[0].FileName, validLogoImages[0].ContentType)
                : null;

            var result = await Mediator.Send(new CreateBrandCommand(
                _model.Name?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                logo,
                _model.IsActive));

            if (result.IsSuccess)
            {
                Notifications.Show(Strings.Brand_Created, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageBrands);
            }
            else if (result.ValidationErrors.Count > 0)
            {
                _form.ShowErrors(result.ValidationErrors);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Brand_CreateFailed)], ShopNotificationKind.Error);
            }
        });
    }
}
