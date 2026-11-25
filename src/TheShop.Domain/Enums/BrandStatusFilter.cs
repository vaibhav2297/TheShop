namespace TheShop.Domain.Enums;

/// <summary>
/// Status filter offered on the manage-brands admin page. The filter is optional throughout:
/// a <see langword="null"/> <c>BrandStatusFilter?</c> means no status narrowing — every brand —
/// so "all" is modelled as the absence of a filter rather than as a member of this enum.
/// </summary>
public enum BrandStatusFilter
{
    Active = 1,
    Inactive = 2,
}
