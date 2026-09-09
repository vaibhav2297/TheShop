namespace TheShop.Domain.Enums;

/// <summary>
/// Status filter offered on the manage-products admin page. The filter is optional throughout:
/// a <see langword="null"/> <c>ProductStatusFilter?</c> means no status narrowing — every product —
/// so "all" is modelled as the absence of a filter rather than as a member of this enum.
/// </summary>
public enum ProductStatusFilter
{
    Active = 1,
    Inactive = 2,
}
