using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFeatured;
using TheShop.Application.Features.Homepage.Commands.UpdateFeatured;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetFeaturedForAdmin;
using TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>Permission-gated Featured editor with local drafts and bounded Section scrolling.</summary>
[Route(Routes.Admin.ManageFeatured)]
[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]
public partial class ManageFeatured : ComponentBase, IAsyncDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUser { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly CancellationTokenSource _lifetime = new();
    private FeaturedFormModel _model = new();
    private EditContext _editContext = default!;
    private ShopCommandValidation? _validation;
    private FeaturedTileEditor? _tile1Editor;
    private FeaturedTileEditor? _tile2Editor;
    private ElementReference _root;
    private IJSObjectReference? _module;
    private IJSObjectReference? _scroll;
    private Task? _scrollMount;
    private bool _hasFeatured;
    private bool _loaded;
    private bool _requiresReload;
    private bool _disposed;
    private string? _loadError;
    private string State => _loadError is not null ? "load-error" : !_loaded ? "loading"
        : _hasFeatured ? "edit" : CanMutate ? "add" : "absent";
    private string Heading => string.Format(_hasFeatured ? Strings.Featured_EditHeading : Strings.Featured_AddHeading,
        string.IsNullOrWhiteSpace(_model.Title) ? Strings.Homepage_Featured : _model.Title.Trim());
    private bool CanMutate => CurrentUser.HasPermission(PermissionCatalogue.Homepage.ViewCode)
        && CurrentUser.HasPermission(_hasFeatured ? PermissionCatalogue.Homepage.EditCode : PermissionCatalogue.Homepage.CreateCode);
    private static readonly IReadOnlyDictionary<string, string> FieldMap = new Dictionary<string, string>
    {
        ["Tile1.NewImage"] = "Tile1.Images",
        ["Tile2.NewImage"] = "Tile2.Images"
    };

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _editContext = new(_model);
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Add(Strings.Homepage_Heading, Routes.Admin.ManageHomepage)
            .Current(Strings.Homepage_Featured));
        await LoadAsync();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        await (_scrollMount ??= MountScrollAsync());
        if (_disposed) return;
        if (_scroll is not null) await _scroll.InvokeVoidAsync("refresh");
    }

    private async Task MountScrollAsync()
    {
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/featuredEditor.js");
        if (!_disposed) _scroll = await _module.InvokeAsync<IJSObjectReference>("mount", _root);
    }

    private async Task LoadAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Homepage.LoadFeatured)
            || BusyState.IsBusy(BusyKeys.Homepage.SaveFeatured)) return;
        await BusyState.RunAsync(BusyKeys.Homepage.LoadFeatured, async () =>
        {
            var result = await Mediator.Send(new GetFeaturedForAdminQuery(), _lifetime.Token);
            if (result.IsFailure)
            {
                _loadError = result.Error;
                return;
            }
            var section = result.Value;
            _hasFeatured = section is not null;
            _model = section is null ? new() : new()
            {
                Title = section.Title,
                Tile1 = TileModel(section.Tile1),
                Tile2 = TileModel(section.Tile2),
                IsVisible = section.IsVisible
            };
            _editContext = new(_model);
            _loadError = null;
            _requiresReload = false;
            _loaded = true;
        });
    }

    private static FeaturedTileFormModel TileModel(FeaturedTileDto tile) => new()
    {
        Label = tile.Label,
        Title = tile.Title,
        ButtonLabel = tile.ButtonLabel,
        ButtonLink = tile.ButtonLink,
        Images = [ShopUploadedImage.Existing(Guid.NewGuid(), tile.Image.PreviewUrl)]
    };

    private async Task<string?> ValidateImageAsync(ShopUploadedImage image, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ValidateFeaturedImageQuery(
            new(image.Bytes, image.FileName, image.ContentType)), cancellationToken);
        return result.IsSuccess ? null : Localizer[result.Error!].Value;
    }

    private void SetVisible(bool visible)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveFeatured)
            || BusyState.IsBusy(BusyKeys.Homepage.LoadFeatured)) return;
        _model.IsVisible = visible;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.IsVisible)));
    }

    private async Task SaveAsync()
    {
        if (!CanMutate || _requiresReload || BusyState.IsBusy(BusyKeys.Homepage.SaveFeatured)
            || BusyState.IsBusy(BusyKeys.Homepage.LoadFeatured)) return;
        _validation?.Clear();
        await BusyState.RunAsync(BusyKeys.Homepage.SaveFeatured, async () =>
        {
            if (_tile1Editor is not null) await _tile1Editor.WaitForPendingFilesAsync();
            if (_tile2Editor is not null) await _tile2Editor.WaitForPendingFilesAsync();
            Result result = _hasFeatured
                ? await Mediator.Send(new UpdateFeaturedCommand(_model.Title ?? string.Empty,
                    TileInput(_model.Tile1), TileInput(_model.Tile2), _model.IsVisible), _lifetime.Token)
                : await Mediator.Send(new CreateFeaturedCommand(_model.Title ?? string.Empty,
                    TileInput(_model.Tile1), TileInput(_model.Tile2), _model.IsVisible), _lifetime.Token);
            if (result.IsSuccess)
            {
                Notifications.Show(Strings.Featured_Saved, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageHomepage);
            }
            else if (result.ValidationErrors.Count > 0)
                _validation?.ShowErrors(result.ValidationErrors);
            else
            {
                _requiresReload = result.Error is FeaturedErrorKeys.SaveUnconfirmed
                    or FeaturedErrorKeys.AlreadyExists or FeaturedErrorKeys.NotFound;
                Notifications.Show(Localizer[result.Error!], ShopNotificationKind.Error);
            }
        });
    }

    private static FeaturedTileInput TileInput(FeaturedTileFormModel tile)
        => new(tile.Label, tile.Title ?? string.Empty, tile.ButtonLabel, tile.ButtonLink,
            tile.Images.FirstOrDefault() is { IsExisting: false } image
                ? new(image.Bytes, image.FileName, image.ContentType) : null, tile.Images.Count == 0);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _lifetime.CancelAsync();
        _lifetime.Dispose();
        try
        {
            if (_scrollMount is not null) await _scrollMount;
            if (_scroll is not null)
            {
                await _scroll.InvokeVoidAsync("dispose");
                await _scroll.DisposeAsync();
            }
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
