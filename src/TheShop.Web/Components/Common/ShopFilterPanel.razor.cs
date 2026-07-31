using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Common.Filtering;
using TheShop.Domain.Enums;
using TheShop.Web.Resources;
using Timer = System.Timers.Timer;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Renders one control per backend-driven <see cref="FilterGroupDto"/> — a checkbox list for
/// <see cref="FilterKind.MultiSelect"/> groups, a single-choice list for
/// <see cref="FilterKind.SingleSelect"/> groups, a min/max range for <see cref="FilterKind.Range"/>.
/// The panel hard-codes no filter set: whatever <see cref="Groups"/> the caller supplies is what
/// renders. The page owns the current selection state; this component only raises change
/// callbacks. Feature-agnostic — used by both the product catalogue and manage-brands (plan §5
/// Decision 4).
/// </summary>
public partial class ShopFilterPanel : MudComponentBase, IDisposable
{
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    /// <summary>The backend-driven filter groups to render.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<FilterGroupDto> Groups { get; set; } = [];

    /// <summary>The currently applied multi-select filter values (drives the checkbox states).</summary>
    [Parameter]
    public IReadOnlyList<AppliedFilterDto> SelectedFilters { get; set; } = [];

    /// <summary>
    /// Raised with the single toggled option whenever the user checks or unchecks a
    /// <see cref="FilterKind.MultiSelect"/> option. The panel emits the atomic change (not a
    /// recomputed selection list) so the owning page can merge it into its authoritative state —
    /// see <see cref="FilterToggle"/>.
    /// </summary>
    [Parameter]
    public EventCallback<FilterToggle> FilterToggled { get; set; }

    /// <summary>
    /// Raised with the group key and newly chosen value whenever the user picks a different
    /// option in a <see cref="FilterKind.SingleSelect"/> group (plan §5 Decision 5). A
    /// <see langword="null"/> value means the user cleared the group by unchecking the selected
    /// option — a single-select group offers no "any" option of its own, because selecting
    /// nothing already says exactly that.
    /// </summary>
    [Parameter]
    public EventCallback<(string GroupKey, string? Value)> SingleSelectChanged { get; set; }

    /// <summary>
    /// The currently applied bounds per <see cref="FilterKind.Range"/> group, keyed by group key.
    /// A group absent from this list is unbounded on both sides.
    /// </summary>
    [Parameter]
    public IReadOnlyList<RangeSelection> SelectedRanges { get; set; } = [];

    /// <summary>
    /// Raised with a range group's settled bounds whenever the user finishes moving either of its
    /// thumbs. Both bounds travel together in one <see cref="RangeSelection"/> so a drag that
    /// changes each end costs the owning page a single state update — see <see cref="RangeSelection"/>.
    /// </summary>
    [Parameter]
    public EventCallback<RangeSelection> RangeChanged { get; set; }

    /// <summary>
    /// Formats a range-group bound for display in the panel header and slider labels, given the
    /// group key and the value. The panel renders no filter set of its own, so it cannot assume
    /// currency — the caller supplies the formatting, and receives the group key so a page with
    /// several range groups can format each in its own units.
    /// </summary>
    [Parameter]
    public Func<string, decimal, string> RangeFormatter { get; set; } = FormatDefault;

    /// <summary>
    /// Milliseconds of drag inactivity before a range-thumb change is pushed to
    /// <see cref="RangeChanged"/>. The thumb itself moves live; only the callback is deferred.
    /// Set to <c>0</c> (or less) to commit immediately.
    /// </summary>
    [Parameter]
    public double DebounceInterval { get; set; } = 300;

    /// <summary>Raised when the user clears every applied filter.</summary>
    [Parameter]
    public EventCallback OnClearFilters { get; set; }

    /// <summary>
    /// The component-owned thumb positions per range group. They remain concrete while an applied
    /// bound may be <see langword="null"/> to represent an unbounded side.
    /// </summary>
    private readonly Dictionary<string, (decimal Min, decimal Max)> _thumbs =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The range groups whose thumbs have moved but whose bounds are not committed yet. Held as a
    /// set so a drag that crosses groups before the window elapses commits every group it touched.
    /// </summary>
    private readonly HashSet<string> _pendingGroups = new(StringComparer.Ordinal);

    /// <summary>
    /// The debounce timer shared by every range thumb. Moving any thumb restarts the timer so the
    /// settled bounds are pushed to the parent only after the sliders have gone quiet.
    /// </summary>
    private Timer? _debounceTimer;

    /// <summary>
    /// Gets whether any option is selected or any range group is narrowed, indicating that the
    /// clear-filters action should be available.
    /// </summary>
    private bool HasActiveFilters =>
        SelectedFilters is { Count: > 0 }
        || SelectedRanges.Any(r => r.Min is not null || r.Max is not null);

    /// <summary>Gets a range group's current thumb positions.</summary>
    private (decimal Min, decimal Max) Thumbs(string groupKey) =>
        _thumbs.TryGetValue(groupKey, out var thumbs) ? thumbs : default;

    /// <summary>Gets a range group's current thumb positions formatted for the panel header.</summary>
    private string RangeHeaderText(string groupKey)
    {
        var (min, max) = Thumbs(groupKey);
        return $"{RangeFormatter(groupKey, min)} – {RangeFormatter(groupKey, max)}";
    }

    /// <summary>Gets a range group's applied bounds, both <see langword="null"/> when unbounded.</summary>
    private (decimal? Min, decimal? Max) Applied(string groupKey) =>
        SelectedRanges.FirstOrDefault(r => r.GroupKey == groupKey) is { } applied
            ? (applied.Min, applied.Max)
            : (null, null);

    private static string FormatDefault(string groupKey, decimal value) => value.ToString("0.##");

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        _thumbs.Clear();

        foreach (var group in Groups.Where(g => g.Kind == FilterKind.Range && g.Range is not null))
        {
            var bounds = group.Range!;
            var (min, max) = Applied(group.Key);

            // Filter -> thumb: a null bound maps to that end of the full range. This also handles
            // Clear (both null) and any external state change, snapping the thumbs back cleanly.
            _thumbs[group.Key] = (min ?? bounds.Min, max ?? bounds.Max);
        }
    }

    /// <summary>
    /// Updates a group's lower-thumb position and schedules its settled bounds to be committed.
    /// </summary>
    private Task OnRangeMinChangedAsync(string groupKey, decimal value)
    {
        var (_, max) = Thumbs(groupKey);
        _thumbs[groupKey] = (value, max);
        return ScheduleCommitAsync(groupKey);
    }

    /// <summary>
    /// Updates a group's upper-thumb position and schedules its settled bounds to be committed.
    /// </summary>
    private Task OnRangeMaxChangedAsync(string groupKey, decimal value)
    {
        var (min, _) = Thumbs(groupKey);
        _thumbs[groupKey] = (min, value);
        return ScheduleCommitAsync(groupKey);
    }

    /// <summary>
    /// Restarts the shared debounce window, or commits immediately when
    /// <see cref="DebounceInterval"/> is not positive.
    /// </summary>
    private Task ScheduleCommitAsync(string groupKey)
    {
        _pendingGroups.Add(groupKey);

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

    /// <summary>
    /// Pushes each pending group's changed, settled bounds to the parent, mapping a thumb at its
    /// range boundary to <see langword="null"/> to represent an unbounded side.
    /// </summary>
    private async Task CommitAsync()
    {
        var pending = _pendingGroups.ToList();
        _pendingGroups.Clear();

        foreach (var groupKey in pending)
        {
            if (Groups.FirstOrDefault(g => g.Key == groupKey)?.Range is not { } bounds)
                continue;

            var (thumbMin, thumbMax) = Thumbs(groupKey);
            var min = thumbMin <= bounds.Min ? (decimal?)null : thumbMin;
            var max = thumbMax >= bounds.Max ? (decimal?)null : thumbMax;

            if ((min, max) == Applied(groupKey))
                continue;

            await RangeChanged.InvokeAsync(new RangeSelection(groupKey, min, max));
        }
    }

    private bool IsSelected(string groupKey, string value) =>
        SelectedFilters.FirstOrDefault(f => f.Key == groupKey)?.Values.Contains(value) ?? false;

    private int CountSelected(string groupKey) =>
        SelectedFilters.FirstOrDefault(f => f.Key == groupKey)?.Values.Count ?? 0;

    /// <summary>
    /// Gets the selected value for a single-select group, or <see langword="null"/> when the group
    /// is unfiltered. No option is selected by default: an unfiltered group renders with every
    /// option unchecked, which is what "no narrowing" looks like.
    /// </summary>
    private string? SelectedSingleValue(string groupKey) =>
        SelectedFilters.FirstOrDefault(f => f.Key == groupKey)?.Values.FirstOrDefault();

    /// <summary>
    /// Emits an atomic option toggle for the owning page to merge into its authoritative
    /// selection state.
    /// </summary>
    private Task OnOptionToggledAsync(string groupKey, string value, bool isChecked) =>
        FilterToggled.InvokeAsync(new FilterToggle(groupKey, value, isChecked));

    private Task OnSingleSelectChangedAsync(string groupKey, string? value) =>
        SingleSelectChanged.InvokeAsync((groupKey, value));

    private Task OnClearFiltersAsync() => OnClearFilters.InvokeAsync();

    /// <inheritdoc/>
    public void Dispose()
    {
        _debounceTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
