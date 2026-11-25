namespace TheShop.Web.Components.Common;

/// <summary>
/// The applied bounds of one <see cref="TheShop.Domain.Enums.FilterKind.Range"/> filter group,
/// keyed by group so <see cref="ShopFilterPanel"/> stays feature-agnostic — it neither names nor
/// assumes any particular range (price, rating, weight). The same shape carries both directions:
/// the panel reads the currently applied bounds from it and raises it again when the user settles
/// a thumb, emitting both bounds together so the owning page performs one state update per change
/// rather than one per thumb.
/// </summary>
/// <param name="GroupKey">The range filter group key (e.g. price).</param>
/// <param name="Min">The lower bound, or <see langword="null"/> for no lower bound.</param>
/// <param name="Max">The upper bound, or <see langword="null"/> for no upper bound.</param>
public sealed record RangeSelection(string GroupKey, decimal? Min, decimal? Max);
