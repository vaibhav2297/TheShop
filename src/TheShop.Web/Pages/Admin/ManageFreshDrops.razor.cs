using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;
using TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsForAdmin;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Staff editor for the single Fresh Drops configuration.
/// </summary>
[Route(Routes.Admin.ManageFreshDrops)]
[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]
public partial class ManageFreshDrops : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUser { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    private readonly CancellationTokenSource _lifetime = new();
    private FreshDropsFormModel _model = new();
    private EditContext _editContext = default!;
    private ShopCommandValidation? _validation;
    private bool _hasDrops;
    private bool _loaded;
    private string? _loadError;
    private bool _dialogOpen;
    private string? _productError;
    private readonly Dictionary<Guid, FreshDropsProductDto> _cards = [];
    private List<FreshDropsProductDto> _automatic = [];
    private Guid? _draggedId;

    private bool CanMutate => CurrentUser.HasPermission(
        _hasDrops ? PermissionCatalogue.Homepage.EditCode : PermissionCatalogue.Homepage.CreateCode);
    private string Heading => string.Format(_hasDrops ? Strings.FreshDrops_EditHeading : Strings.FreshDrops_AddHeading,
        string.IsNullOrWhiteSpace(_model.Title) ? Strings.Homepage_FreshDrops : _model.Title.Trim());
    private string State => _loadError is not null ? "load-error" : !_loaded ? "loading" : _hasDrops ? "edit" : "add";
    private bool IsManual => _model.SelectionMode == FreshDropsSelectionMode.Manual;
    private IReadOnlyList<Guid> SelectedIds => _model.ManualProductIds;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _editContext = new(_model);
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Add(Strings.Homepage_Heading, Routes.Admin.ManageHomepage)
            .Current(Strings.Homepage_FreshDrops));
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Homepage.LoadFreshDrops)) return;
        await BusyState.RunAsync(BusyKeys.Homepage.LoadFreshDrops, async () =>
        {
            var result = await Mediator.Send(new GetFreshDropsForAdminQuery(), _lifetime.Token);
            if (result.IsFailure)
            {
                _loadError = result.Error;
                return;
            }
            var drops = result.Value;
            _hasDrops = drops is not null;
            _model = drops is null ? new() : new()
            {
                Title = drops.Title,
                ButtonLabel = drops.ButtonLabel,
                ButtonLink = drops.ButtonLink,
                SelectionMode = drops.SelectionMode,
                ManualProductIds = [.. drops.ManualProductIds],
                IsVisible = drops.IsVisible
            };
            _editContext = new(_model);
            _cards.Clear();
            _loadError = null;
            _loaded = true;
            await LoadDisplayedProductsAsync();
        });
    }

    private async Task LoadDisplayedProductsAsync()
    {
        if (IsManual)
        {
            if (_model.ManualProductIds.Count == 0) return;
            var selected = await Mediator.Send(new GetFreshDropsProductsQuery(_model.ManualProductIds, Limit: 3), _lifetime.Token);
            if (selected.IsSuccess && selected.Value is not null)
                foreach (var card in selected.Value) _cards[card.Id] = card;
            else _productError = selected.Error;
        }
        else
        {
            var newest = await Mediator.Send(new GetFreshDropsProductsQuery(Limit: 3), _lifetime.Token);
            if (newest.IsSuccess) _automatic = [.. newest.Value ?? []];
            else _productError = newest.Error;
        }
    }

    private async Task SetModeAsync(FreshDropsSelectionMode mode)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveFreshDrops)) return;
        _model.SelectionMode = mode;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.SelectionMode)));
        await LoadDisplayedProductsAsync();
    }

    private void SetVisible(bool visible)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveFreshDrops)) return;
        _model.IsVisible = visible;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.IsVisible)));
    }

    private Task OpenDialogAsync()
    {
        if (!CanMutate || !IsManual || _model.ManualProductIds.Count >= 3) return Task.CompletedTask;
        _dialogOpen = true;
        return Task.CompletedTask;
    }

    private Task DialogCompletedAsync(FreshDropsProductDialogResult? selection)
    {
        if (selection is not null)
        {
            _model.ManualProductIds = [.. selection.ProductIds];
            foreach (var card in selection.Cards) _cards[card.Id] = card;
            _editContext.NotifyFieldChanged(new(_model, nameof(_model.ManualProductIds)));
        }
        _dialogOpen = false;
        return Task.CompletedTask;
    }

    private void Remove(Guid id)
    {
        _model.ManualProductIds.Remove(id);
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.ManualProductIds)));
    }

    private void Move(Guid id, int delta)
    {
        var from = _model.ManualProductIds.IndexOf(id);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _model.ManualProductIds.Count) return;
        (_model.ManualProductIds[from], _model.ManualProductIds[to]) =
            (_model.ManualProductIds[to], _model.ManualProductIds[from]);
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.ManualProductIds)));
    }

    private void StartDrag(Guid id) => _draggedId = id;
    private void DropOn(Guid target)
    {
        if (_draggedId is { } source && source != target)
        {
            var from = _model.ManualProductIds.IndexOf(source);
            var to = _model.ManualProductIds.IndexOf(target);
            if (from >= 0 && to >= 0)
            {
                _model.ManualProductIds.RemoveAt(from);
                _model.ManualProductIds.Insert(to, source);
                _editContext.NotifyFieldChanged(new(_model, nameof(_model.ManualProductIds)));
            }
        }
        _draggedId = null;
    }

    private async Task SaveAsync()
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveFreshDrops)) return;
        _validation?.Clear();
        await BusyState.RunAsync(BusyKeys.Homepage.SaveFreshDrops, async () =>
        {
            Result result = _hasDrops
                ? await Mediator.Send(new UpdateFreshDropsCommand(_model.Title, _model.ButtonLabel,
                    _model.ButtonLink, _model.SelectionMode, _model.ManualProductIds, _model.IsVisible), _lifetime.Token)
                : await Mediator.Send(new CreateFreshDropsCommand(_model.Title, _model.ButtonLabel,
                    _model.ButtonLink, _model.SelectionMode, _model.ManualProductIds, _model.IsVisible), _lifetime.Token);
            if (result.IsSuccess)
            {
                Notifications.Show(Strings.FreshDrops_Saved, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageHomepage);
            }
            else if (result.ValidationErrors.Count > 0) _validation?.ShowErrors(result.ValidationErrors);
            else Notifications.Show(Localizer[result.Error!], ShopNotificationKind.Error);
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
