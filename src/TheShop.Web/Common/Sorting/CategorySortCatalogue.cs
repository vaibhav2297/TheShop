using TheShop.Domain.Enums;
using TheShop.Web.Resources;

namespace TheShop.Web.Common.Sorting;

/// <summary>
/// The manage-categories list's sort configuration. Name A→Z leads because it is the order an
/// empty URL resolves to; Newest/Oldest are offered after, reading as "what did we just add" /
/// "what's been here longest" shortcuts rather than the primary ordering.
/// </summary>
public sealed class CategorySortCatalogue : SortCatalogue<CategorySortOption>
{
    /// <summary>The shared instance — the catalogue is immutable, so one suffices.</summary>
    public static readonly CategorySortCatalogue Instance = new();

    private CategorySortCatalogue() : base(
        CategorySortOption.NameAToZ,
        new(CategorySortOption.NameAToZ, SortSlugs.NameAsc, nameof(Strings.Sort_NameAZ)),
        new(CategorySortOption.NameZToA, SortSlugs.NameDesc, nameof(Strings.Sort_NameZA)),
        new(CategorySortOption.NewestFirst, SortSlugs.Newest, nameof(Strings.Sort_Newest)),
        new(CategorySortOption.OldestFirst, SortSlugs.Oldest, nameof(Strings.Sort_Oldest)))
    {
    }
}
