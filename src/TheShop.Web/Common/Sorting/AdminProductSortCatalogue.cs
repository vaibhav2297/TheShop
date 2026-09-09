using TheShop.Domain.Enums;
using TheShop.Web.Resources;

namespace TheShop.Web.Common.Sorting;

/// <summary>
/// The manage-products list's sort configuration. Name A→Z leads because it is the order an empty
/// URL resolves to (FR-5). The two price orders sort on the lowest variant price — a distinct
/// concept from the storefront's <c>display_price</c> sort — so they carry their own label keys
/// even though they share the <c>price-asc</c>/<c>price-desc</c> URL slugs the admin RPC expects.
/// </summary>
public sealed class AdminProductSortCatalogue : SortCatalogue<AdminProductSortOption>
{
    /// <summary>The shared instance — the catalogue is immutable, so one suffices.</summary>
    public static readonly AdminProductSortCatalogue Instance = new();

    private AdminProductSortCatalogue() : base(
        AdminProductSortOption.NameAToZ,
        new(AdminProductSortOption.NameAToZ, SortSlugs.NameAsc, nameof(Strings.Sort_NameAZ)),
        new(AdminProductSortOption.NameZToA, SortSlugs.NameDesc, nameof(Strings.Sort_NameZA)),
        new(AdminProductSortOption.NewestFirst, SortSlugs.Newest, nameof(Strings.Sort_Newest)),
        new(AdminProductSortOption.OldestFirst, SortSlugs.Oldest, nameof(Strings.Sort_Oldest)),
        new(AdminProductSortOption.LowestVariantPriceAsc, SortSlugs.PriceAsc, nameof(Strings.Sort_LowestVariantPriceAsc)),
        new(AdminProductSortOption.LowestVariantPriceDesc, SortSlugs.PriceDesc, nameof(Strings.Sort_LowestVariantPriceDesc)))
    {
    }
}
