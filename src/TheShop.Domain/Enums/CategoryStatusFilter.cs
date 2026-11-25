namespace TheShop.Domain.Enums;

/// <summary>
/// Status filter offered on the manage-categories admin page. The filter is optional throughout:
/// a <see langword="null"/> <c>CategoryStatusFilter?</c> means no status narrowing — every
/// category — so "all" is modelled as the absence of a filter rather than as a member of this enum.
/// </summary>
public enum CategoryStatusFilter
{
    Active = 1,
    Inactive = 2,
}
