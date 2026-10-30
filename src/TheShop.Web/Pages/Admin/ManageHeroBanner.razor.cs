using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Commands.CreateHeroBanner;
using TheShop.Application.Features.Homepage.Commands.UpdateHeroBanner;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHeroBannerForAdmin;
using TheShop.Application.Features.Homepage.Queries.ValidateHeroBannerImage;
using TheShop.Application.Features.Homepage;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>Admin-only singleton editor; drafts and preview bytes stay local until a successful save.</summary>
[Route(Routes.Admin.ManageHeroBanner)]
[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]
public partial class ManageHeroBanner : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUser { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    private readonly CancellationTokenSource _lifetime = new();
    private HeroBannerFormModel _model = new();
    private EditContext _editContext = default!;
    private ShopCommandValidation? _validation;
    private ShopImageUpload? _desktopUpload;
    private ShopImageUpload? _mobileUpload;
    private bool _hasBanner;
    private bool _loaded;
    private bool _requiresReload;
    private string? _loadError;
    private string State => _loadError is not null ? "load-error" : !_loaded ? "loading"
        : _hasBanner ? "edit" : CanMutate ? "add" : "absent";
    private string Heading => _hasBanner ? Strings.HeroBanner_EditHeading : Strings.HeroBanner_AddHeading;
    private bool CanMutate => CurrentUser.HasPermission(
        _hasBanner ? PermissionCatalogue.Homepage.EditCode : PermissionCatalogue.Homepage.CreateCode);
    private static readonly IReadOnlyDictionary<string, string> FieldMap = new Dictionary<string, string>
    {
        [nameof(UpdateHeroBannerCommand.NewDesktopImage)] = nameof(HeroBannerFormModel.DesktopImage),
        [nameof(UpdateHeroBannerCommand.NewMobileImage)] = nameof(HeroBannerFormModel.MobileImage)
    };

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _editContext = new(_model);
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Add(Strings.Homepage_Heading, Routes.Admin.ManageHomepage)
            .Current(Strings.Homepage_HeroBanner));
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Homepage.LoadHeroBanner)) return;
        await BusyState.RunAsync(BusyKeys.Homepage.LoadHeroBanner, async () =>
        {
            var result = await Mediator.Send(new GetHeroBannerForAdminQuery(), _lifetime.Token);
            if (result.IsFailure)
            {
                _loadError = result.Error;
                return;
            }
            var banner = result.Value;
            _hasBanner = banner is not null;
            _model = banner is null ? new() : new()
            {
                Heading = banner.Heading,
                Description = banner.Description,
                ButtonLabel = banner.ButtonLabel,
                ButtonLink = banner.ButtonLink,
                IsVisible = banner.IsVisible,
                DesktopImage = [ShopUploadedImage.Existing(Guid.NewGuid(), banner.DesktopImage.PreviewUrl)],
                MobileImage = banner.MobileImage is { } mobile
                    ? [ShopUploadedImage.Existing(Guid.NewGuid(), mobile.PreviewUrl)] : []
            };
            _editContext = new(_model);
            _loadError = null;
            _requiresReload = false;
            _loaded = true;
        });
    }

    private Task<string?> ValidateDesktopAsync(ShopUploadedImage image, CancellationToken ct) =>
        ValidateImageAsync(image, HeroBannerImageRole.Desktop, ct);
    private Task<string?> ValidateMobileAsync(ShopUploadedImage image, CancellationToken ct) =>
        ValidateImageAsync(image, HeroBannerImageRole.Mobile, ct);

    private async Task<string?> ValidateImageAsync(ShopUploadedImage image, HeroBannerImageRole role, CancellationToken ct)
    {
        var result = await Mediator.Send(new ValidateHeroBannerImageQuery(role,
            new(image.Bytes, image.FileName, image.ContentType)), ct);
        return result.IsSuccess ? null : Localizer[result.Error!].Value;
    }

    private void DesktopChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        _model.DesktopImage = images;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.DesktopImage)));
    }
    private void MobileChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        _model.MobileImage = images;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.MobileImage)));
    }
    private void SetVisible(bool visible)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveHeroBanner)) return;
        _model.IsVisible = visible;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.IsVisible)));
    }

    private async Task SaveAsync()
    {
        if (!CanMutate || _requiresReload || BusyState.IsBusy(BusyKeys.Homepage.SaveHeroBanner)) return;
        _validation?.Clear();
        await BusyState.RunAsync(BusyKeys.Homepage.SaveHeroBanner, async () =>
        {
            if (_desktopUpload is not null) await _desktopUpload.WaitForPendingFilesAsync();
            if (_mobileUpload is not null) await _mobileUpload.WaitForPendingFilesAsync();
            Result result = _hasBanner
                ? await Mediator.Send(new UpdateHeroBannerCommand(_model.Heading ?? string.Empty,
                    _model.Description, _model.ButtonLabel, _model.ButtonLink,
                    NewImage(_model.DesktopImage), NewImage(_model.MobileImage),
                    _model.DesktopImage.Count == 0, _model.MobileImage.Count == 0, _model.IsVisible), _lifetime.Token)
                : await Mediator.Send(new CreateHeroBannerCommand(_model.Heading ?? string.Empty,
                    _model.Description, _model.ButtonLabel, _model.ButtonLink,
                    NewImage(_model.DesktopImage), NewImage(_model.MobileImage), _model.IsVisible), _lifetime.Token);
            if (result.IsSuccess)
            {
                Notifications.Show(Strings.HeroBanner_Saved, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageHomepage);
            }
            else if (result.ValidationErrors.Count > 0)
                _validation?.ShowErrors(result.ValidationErrors);
            else
            {
                _requiresReload = result.Error is HeroBannerErrorKeys.SaveUnconfirmed or HeroBannerErrorKeys.AlreadyExists or HeroBannerErrorKeys.NotFound;
                Notifications.Show(Localizer[result.Error!], ShopNotificationKind.Error);
            }
        });
    }

    private static ImageUpload? NewImage(IReadOnlyList<ShopUploadedImage> images) =>
        images.FirstOrDefault() is { IsExisting: false } image
            ? new(image.Bytes, image.FileName, image.ContentType) : null;

    /// <inheritdoc/>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

