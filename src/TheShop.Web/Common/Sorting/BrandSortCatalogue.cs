using TheShop.Domain.Enums;
using TheShop.Web.Resources;

namespace TheShop.Web.Common.Sorting;

/// <summary>
/// The manage-brands list's sort configuration. Name A→Z leads because it is the order an empty URL
/// resolves to; Newest is offered last, reading as the "what did we just add" shortcut rather than
/// the primary ordering.
/// </summary>
public sealed class BrandSortCatalogue : SortCatalogue<BrandSortOption>
{
    /// <summary>The shared instance — the catalogue is immutable, so one suffices.</summary>
    public static readonly BrandSortCatalogue Instance = new();

    private BrandSortCatalogue() : base(
        BrandSortOption.NameAToZ,
        new(BrandSortOption.NameAToZ, SortSlugs.NameAsc, nameof(Strings.Sort_NameAZ)),
        new(BrandSortOption.NameZToA, SortSlugs.NameDesc, nameof(Strings.Sort_NameZA)),
        new(BrandSortOption.NewestFirst, SortSlugs.Newest, nameof(Strings.Sort_Newest)))
    {
    }
}
