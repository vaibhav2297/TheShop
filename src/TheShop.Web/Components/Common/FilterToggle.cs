namespace TheShop.Web.Components.Common;

/// <summary>
/// A single filter-option toggle raised by <see cref="ShopFilterPanel"/>: the group whose
/// option changed, the option value, and whether it is now selected. The panel emits this atomic
/// change rather than a recomputed selection list so the owning page can merge it into its own
/// authoritative state. That avoids a read-modify-write race in which two rapid toggles both build
/// off the same pre-change selection (a parameter that only refreshes after the URL round-trip) and
/// the second silently drops the first.
/// </summary>
/// <param name="GroupKey">The filter group key (e.g. category, brand).</param>
/// <param name="Value">The toggled option value.</param>
/// <param name="IsSelected">Whether the option is now selected.</param>
public sealed record FilterToggle(string GroupKey, string Value, bool IsSelected);
