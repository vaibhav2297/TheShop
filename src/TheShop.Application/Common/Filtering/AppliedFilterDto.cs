namespace TheShop.Application.Common.Filtering;

/// <summary>
/// A filter group's selected values, sent back on the page query. <see cref="Key"/> must be
/// one of the feature's known selectable keys.
/// </summary>
public sealed record AppliedFilterDto(string Key, IReadOnlyList<string> Values);
