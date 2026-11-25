using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryForAdmin;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>Staff editor for the singleton homepage category section.</summary>
[Route(Routes.Admin.ManageHomepageCategory)]
[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]
public partial class ManageHomepageCategory : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUser { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    private readonly CancellationTokenSource _lifetime = new();
    private HomepageCategoryFormModel _model = new();
    private EditContext _editContext = default!;
    private ShopCommandValidation? _validation;
    private bool _hasSection;
    private bool _loaded;
    private bool _pickerOpen;
    private string? _loadError;
    private Guid? _draggedId;
    private List<Guid> _pickerIds = [];
    private List<CategoryDto> _choices = [];
    private readonly Dictionary<Guid, CategoryDto> _cards = [];

    private bool CanMutate => CurrentUser.HasPermission(
        _hasSection ? PermissionCatalogue.Homepage.EditCode : PermissionCatalogue.Homepage.CreateCode);
    private string Heading => string.Format(_hasSection ? Strings.HomepageCategory_EditHeading : Strings.HomepageCategory_AddHeading,
        string.IsNullOrWhiteSpace(_model.Title) ? Strings.HomepageCategory_DefaultTitle : _model.Title.Trim());
    private string State => _loadError is not null ? "load-error" : !_loaded ? "loading" : _hasSection ? "edit" : "add";
    private IReadOnlyList<Guid> SelectedIds => _model.CategoryIds;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _editContext = new(_model);
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Add(Strings.Homepage_Heading, Routes.Admin.ManageHomepage)
            .Current(Strings.HomepageCategory_DefaultTitle));
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Homepage.LoadCategorySection)) return;
        await BusyState.RunAsync(BusyKeys.Homepage.LoadCategorySection, async () =>
        {
            var result = await Mediator.Send(new GetHomepageCategoryForAdminQuery(), _lifetime.Token);
            if (result.IsFailure) { _loadError = result.Error; return; }
            var choices = await Mediator.Send(new GetHomepageCategoryChoicesQuery(), _lifetime.Token);
            if (choices.IsFailure) { _loadError = choices.Error; return; }

            var section = result.Value;
            _hasSection = section is not null;
            _model = section is null ? new() : new()
            {
                Title = section.Title,
                ButtonLabel = section.ButtonLabel,
                ButtonLink = section.ButtonLink,
                CategoryIds = [.. section.CategoryIds],
                IsVisible = section.IsVisible
            };
            _editContext = new(_model);
            _choices = [.. choices.Value];
            _cards.Clear();
            foreach (var card in _choices) _cards[card.Id] = card;
            if (section is not null)
                foreach (var card in section.Categories) _cards[card.Id] = card;
            _loadError = null;
            _loaded = true;
        });
    }

    private void SetVisible(bool visible)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveCategorySection)) return;
        _model.IsVisible = visible;
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.IsVisible)));
    }

    private Task OpenPickerAsync()
    {
        if (!CanMutate || _model.CategoryIds.Count >= 3) return Task.CompletedTask;
        _pickerIds = [.. _model.CategoryIds];
        _pickerOpen = true;
        return Task.CompletedTask;
    }

    private void TogglePickerChoice(Guid id)
    {
        if (!_pickerIds.Remove(id) && _pickerIds.Count < 3) _pickerIds.Add(id);
    }

    private Task ConfirmPickerAsync()
    {
        _model.CategoryIds = [.. _pickerIds];
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.CategoryIds)));
        _pickerOpen = false;
        return Task.CompletedTask;
    }

    private Task CancelPickerAsync()
    {
        _pickerOpen = false;
        return Task.CompletedTask;
    }

    private void Remove(Guid id)
    {
        if (!CanMutate) return;
        _model.CategoryIds.Remove(id);
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.CategoryIds)));
    }

    private void Move(Guid id, int delta)
    {
        if (!CanMutate) return;
        var from = _model.CategoryIds.IndexOf(id);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _model.CategoryIds.Count) return;
        (_model.CategoryIds[from], _model.CategoryIds[to]) = (_model.CategoryIds[to], _model.CategoryIds[from]);
        _editContext.NotifyFieldChanged(new(_model, nameof(_model.CategoryIds)));
    }

    private void StartDrag(Guid id) => _draggedId =
        CanMutate && !BusyState.IsBusy(BusyKeys.Homepage.SaveCategorySection) ? id : null;
    private void DropOn(Guid target)
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveCategorySection)) return;
        if (_draggedId is { } source && source != target)
        {
            var from = _model.CategoryIds.IndexOf(source);
            var to = _model.CategoryIds.IndexOf(target);
            if (from >= 0 && to >= 0)
            {
                _model.CategoryIds.RemoveAt(from);
                _model.CategoryIds.Insert(to, source);
                _editContext.NotifyFieldChanged(new(_model, nameof(_model.CategoryIds)));
            }
        }
        _draggedId = null;
    }

    private async Task SaveAsync()
    {
        if (!CanMutate || BusyState.IsBusy(BusyKeys.Homepage.SaveCategorySection)) return;
        _validation?.Clear();
        await BusyState.RunAsync(BusyKeys.Homepage.SaveCategorySection, async () =>
        {
            Result result = _hasSection
                ? await Mediator.Send(new UpdateHomepageCategoryCommand(_model.Title, _model.ButtonLabel,
                    _model.ButtonLink, _model.CategoryIds, _model.IsVisible), _lifetime.Token)
                : await Mediator.Send(new CreateHomepageCategoryCommand(_model.Title, _model.ButtonLabel,
                    _model.ButtonLink, _model.CategoryIds, _model.IsVisible), _lifetime.Token);
            if (result.IsSuccess)
            {
                Notifications.Show(Strings.HomepageCategory_Saved, ShopNotificationKind.Success);
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
