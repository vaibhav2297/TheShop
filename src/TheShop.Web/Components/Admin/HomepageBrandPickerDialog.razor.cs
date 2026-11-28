using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Admin;

/// <summary>The ordered active brand selection returned by the picker.</summary>
public sealed record HomepageBrandPickerResult(IReadOnlyList<Guid> BrandIds, IReadOnlyList<BrandDto> Cards);

/// <summary>Dialog-local active choices, search, and staged selection.</summary>
public partial class HomepageBrandPickerDialog : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    [Parameter, EditorRequired] public IReadOnlyList<Guid> InitialSelectedIds { get; set; } = [];
    [Parameter, EditorRequired] public EventCallback<HomepageBrandPickerResult?> OnCompleted { get; set; }

    private readonly CancellationTokenSource _lifetime = new();
    private List<Guid> _selectedIds = [];
    private List<BrandDto> _choices = [];
    private string _search = string.Empty;
    private string? _choiceError;
    private bool _choicesLoaded;
    private bool _completed;

    private IEnumerable<BrandDto> FilteredChoices => _choices.Where(choice =>
        choice.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync() => await LoadChoicesAsync();

    private async Task LoadChoicesAsync()
    {
        if (_completed) return;
        _choiceError = null;
        await BusyState.RunAsync(BusyKeys.Homepage.LoadBrandChoices, async () =>
        {
            var result = await Mediator.Send(new GetHomepageBrandChoicesQuery(), _lifetime.Token);
            if (_completed) return;
            if (result.IsFailure) { _choiceError = result.Error; return; }
            _choices = [.. result.Value.Where(choice => choice.IsActive)];
            var available = _choices.Select(choice => choice.Id).ToHashSet();
            _selectedIds = [.. InitialSelectedIds.Where(available.Contains)];
            _choicesLoaded = true;
        });
    }

    private void ToggleChoice(Guid id)
    {
        if (!_choicesLoaded || !_choices.Any(choice => choice.Id == id)) return;
        if (!_selectedIds.Remove(id) && _selectedIds.Count < 6) _selectedIds.Add(id);
    }

    private Task ConfirmAsync()
    {
        if (!_choicesLoaded) return Task.CompletedTask;
        var cards = _selectedIds.Select(id => _choices.Single(choice => choice.Id == id)).ToList();
        return CompleteAsync(new HomepageBrandPickerResult([.. _selectedIds], cards));
    }

    private Task CancelAsync() => CompleteAsync(null);

    private Task CompleteAsync(HomepageBrandPickerResult? result)
    {
        if (_completed) return Task.CompletedTask;
        _completed = true;
        return OnCompleted.InvokeAsync(result);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
