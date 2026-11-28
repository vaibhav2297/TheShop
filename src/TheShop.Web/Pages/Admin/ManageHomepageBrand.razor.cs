using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageBrand;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandForAdmin;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>Staff editor for the singleton homepage brand section.</summary>
[Route(Routes.Admin.ManageHomepageBrand)]
[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]
public partial class ManageHomepageBrand : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUser { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    private readonly CancellationTokenSource _lifetime = new();
    private HomepageBrandFormModel _model = new();
    private EditContext _editContext = default!;
    private ShopCommandValidation? _validation;
    private bool _hasSection;
    private bool _loaded;
    private string? _loadError;
    private readonly Dictionary<Guid, BrandDto> _cards = [];
    private bool _pickerOpen;

    private bool CanMutate => CurrentUser.HasPermission(
        _hasSection ? PermissionCatalogue.Homepage.EditCode : PermissionCatalogue.Homepage.CreateCode);
    private string Heading => string.Format(_hasSection ? Strings.HomepageBrand_EditHeading : Strings.HomepageBrand_AddHeading,
        string.IsNullOrWhiteSpace(_model.Title) ? Strings.HomepageBrand_DefaultTitle : _model.Title.Trim());
    private string State => _loadError is not null ? "load-error" : !_loaded ? "loading" : _hasSection ? "edit" : "add";
    private IReadOnlyList<Guid> SelectedIds => _model.BrandIds;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _editContext = new(_model);
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Add(Strings.Homepage_Heading, Routes.Admin.ManageHomepage)
            .Current(Strings.HomepageBrand_DefaultTitle));
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Homepage.LoadBrandSection)) return;
        await BusyState.RunAsync(BusyKeys.Homepage.LoadBrandSection, async () =>
        {
            var result = await Mediator.Send(new GetHomepageBrandForAdminQuery(), _lifetime.Token);
            if (result.IsFailure) { _loadError = result.Error; return; }
            var section = result.Value;
            _hasSection = section is not null;
            _model = section is null ? new() : new()
            {
                Title = section.Title,
                ButtonLabel = section.ButtonLabel,
                ButtonLink = section.ButtonLink,
                BrandIds = [.. section.BrandIds],
                IsVisible = section.IsVisible
            };
            _editContext = new(_model);
            _cards.Clear();
            if (section is not null)
                foreach (var card in section.Brands) _cards[card.Id] = card;
            _loadError = null;
            _loaded = true;
        });
    }

    private void SetVisible(bool visible)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveBrandSection)) return;
        _model.IsVisible = visible;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.IsVisible)));
    }

    private Task OpenPickerAsync()
    {
        if (!CanMutate || _model.BrandIds.Count >= 6 || BusyState.IsBusy(BusyKeys.Homepage.SaveBrandSection))
            return Task.CompletedTask;
        _pickerOpen = true;
        return Task.CompletedTask;
    }

    private Task OnPickerCompletedAsync(HomepageBrandPickerResult? result)
    {
        _pickerOpen = false;
        if (result is not null && CanMutate && !BusyState.IsBusy(BusyKeys.Homepage.SaveBrandSection))
        {
            _model.BrandIds = [.. result.BrandIds];
            foreach (var card in result.Cards) _cards[card.Id] = card;
            _editContext.NotifyFieldChanged(new(_model, nameof(_model.BrandIds)));
        }
        return Task.CompletedTask;
    }

    private void Remove(Guid id)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveBrandSection)) return;
        _model.BrandIds.Remove(id);
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.BrandIds)));
    }

    private void Move(Guid id, int delta)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveBrandSection)) return;
        var from = _model.BrandIds.IndexOf(id);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _model.BrandIds.Count) return;
        (_model.BrandIds[from], _model.BrandIds[to]) = (_model.BrandIds[to], _model.BrandIds[from]);
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.BrandIds)));
    }

    private async Task SaveAsync()
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveBrandSection)) return;
        _validation?.Clear();
        await BusyState.RunAsync(BusyKeys.Homepage.SaveBrandSection, async () =>
        {
            Result result = _hasSection
                ? await Mediator.Send(new UpdateHomepageBrandCommand(_model.Title, _model.ButtonLabel,
                    _model.ButtonLink, _model.BrandIds, _model.IsVisible), _lifetime.Token)
                : await Mediator.Send(new CreateHomepageBrandCommand(_model.Title, _model.ButtonLabel,
                    _model.ButtonLink, _model.BrandIds, _model.IsVisible), _lifetime.Token);
            if (result.IsSuccess)
            {
                Notifications.Show(Strings.HomepageBrand_Saved, ShopNotificationKind.Success);
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
