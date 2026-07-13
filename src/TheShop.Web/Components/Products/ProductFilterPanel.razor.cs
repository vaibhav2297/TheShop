using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using Timer = System.Timers.Timer;

namespace TheShop.Web.Components.Products;

/// <summary>
/// Renders one control per backend-driven <see cref="FilterGroupDto"/> — a checkbox list
/// for <c>MultiSelect</c> groups, a min/max range for the <c>Price</c> group. The panel
/// hard-codes no filter set: whatever <see cref="Groups"/> the catalogue query returns is
/// what renders. The page owns the current selection state; this component only raises
/// change callbacks.
/// </summary>
public partial class ProductFilterPanel : MudComponentBase, IDisposable
{
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    /// <summary>The backend-driven filter groups to render.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<FilterGroupDto> Groups { get; set; } = [];

    /// <summary>The currently applied multi-select filter values (drives the checkbox states).</summary>
    [Parameter]
    public IReadOnlyList<AppliedFilterDto> SelectedFilters { get; set; } = [];

    /// <summary>
    /// Raised with the single toggled option whenever the user checks or unchecks it. The panel
    /// emits the atomic change (not a recomputed selection list) so the owning page can merge it
    /// into its authoritative state — see <see cref="FilterToggle"/>.
    /// </summary>
    [Parameter]
    public EventCallback<FilterToggle> FilterToggled { get; set; }

    /// <summary>The currently applied minimum price.</summary>
    [Parameter]
    public decimal? PriceMin { get; set; }

    /// <summary>Raised when the minimum price changes.</summary>
    [Parameter]
    public EventCallback<decimal?> PriceMinChanged { get; set; }

    /// <summary>The currently applied maximum price.</summary>
    [Parameter]
    public decimal? PriceMax { get; set; }

    /// <summary>Raised when the maximum price changes.</summary>
    [Parameter]
    public EventCallback<decimal?> PriceMaxChanged { get; set; }

    /// <summary>
    /// Milliseconds of drag inactivity before a price-thumb change is pushed to
    /// <see cref="PriceMinChanged"/> / <see cref="PriceMaxChanged"/>. The thumb itself moves
    /// live; only the callback is deferred. Set to <c>0</c> (or less) to commit immediately.
    /// </summary>
    [Parameter]
    public double DebounceInterval { get; set; } = 300;

    /// <summary>Raised when the user clears every applied filter.</summary>
    [Parameter]
    public EventCallback OnClearFilters { get; set; }

    // The range slider always shows a concrete [low, high] pair, but the page's price filter
    // is nullable (null = "no bound on this side"). These fields hold the thumb positions the
    // component owns, so a live drag stays smooth instead of being pushed back by the parent's
    // re-render round-trip; they are re-synced from the parameters on every render below.
    private decimal _sliderMin;
    private decimal _sliderMax;

    // A single debounce shared by both thumbs: the thumb fields above update on every drag
    // tick, but moving either thumb just restarts this one timer, and the settled bounds are
    // pushed to the parent once the whole slider goes quiet for DebounceInterval.
    private Timer? _debounceTimer;

    /// <summary>The single range (price) group's bounds, if the backend returned one.</summary>
    private PriceRangeDto? PriceRange =>
        Groups.FirstOrDefault(g => g.Kind == FilterKind.Range)?.Range;

    // A narrowed price bound counts as an active filter just like a checked option, so the
    // Clear button surfaces (and the "clear everything" affordance stays truthful) even when
    // price is the only thing the user has changed.
    private bool HasActiveFilters =>
        SelectedFilters is { Count: > 0 } || PriceMin is not null || PriceMax is not null;

    /// <summary>The current thumb bounds formatted for display in the panel header.</summary>
    private string RangeHeaderText =>
        $"{CurrencyFormatter.Format(_sliderMin)} – {CurrencyFormatter.Format(_sliderMax)}";

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (PriceRange is not { } range)
            return;

        // Filter -> thumb: a null bound maps to that end of the full range. This also handles
        // Clear (both null) and any external state change, snapping the thumbs back cleanly.
        _sliderMin = PriceMin ?? range.Min;
        _sliderMax = PriceMax ?? range.Max;
    }

    // Both thumbs feed the same debounce window; the normalization to null happens at commit.
    private Task OnSliderMinChangedAsync(decimal value)
    {
        _sliderMin = value;
        return ScheduleCommitAsync();
    }

    private Task OnSliderMaxChangedAsync(decimal value)
    {
        _sliderMax = value;
        return ScheduleCommitAsync();
    }

    // Restarts the shared debounce window; a non-positive interval bypasses it and commits now.
    private Task ScheduleCommitAsync()
    {
        if (DebounceInterval <= 0)
            return CommitAsync();

        var timer = _debounceTimer ??= CreateDebounceTimer();
        timer.Stop();
        timer.Interval = DebounceInterval;
        timer.Start();
        return Task.CompletedTask;
    }

    private Timer CreateDebounceTimer()
    {
        var timer = new Timer { AutoReset = false };
        // Elapsed fires off the UI thread; marshal back before touching state or callbacks.
        timer.Elapsed += (_, _) => InvokeAsync(CommitAsync);
        return timer;
    }

    // Pushes the settled bounds to the parent. Each thumb resting on its boundary maps to null
    // ("no bound on this side"); a side is only raised when it differs from the parent's current
    // value, so a paused drag re-fetches at most once and never for a thumb that didn't move.
    private async Task CommitAsync()
    {
        if (PriceRange is not { } range)
            return;

        var min = _sliderMin <= range.Min ? (decimal?)null : _sliderMin;
        var max = _sliderMax >= range.Max ? (decimal?)null : _sliderMax;

        if (min != PriceMin)
            await PriceMinChanged.InvokeAsync(min);

        if (max != PriceMax)
            await PriceMaxChanged.InvokeAsync(max);
    }

    private bool IsSelected(string groupKey, string value) =>
        SelectedFilters.FirstOrDefault(f => f.Key == groupKey)?.Values.Contains(value) ?? false;

    private int CountSelected(string groupKey) =>
        SelectedFilters.FirstOrDefault(f => f.Key == groupKey)?.Values.Count ?? 0;

    // Emit only the atomic toggle; the page merges it into its authoritative selection. Computing
    // the updated set here would race — SelectedFilters lags a full URL round-trip behind rapid
    // clicks, so a second toggle would rebuild off a stale set and drop the first.
    private Task OnOptionToggledAsync(string groupKey, string value, bool isChecked) =>
        FilterToggled.InvokeAsync(new FilterToggle(groupKey, value, isChecked));

    private Task OnClearFiltersAsync() => OnClearFilters.InvokeAsync();

    /// <inheritdoc/>
    public void Dispose()
    {
        _debounceTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
