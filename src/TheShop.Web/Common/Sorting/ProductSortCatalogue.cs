using TheShop.Domain.Enums;
using TheShop.Web.Resources;

namespace TheShop.Web.Common.Sorting;

/// <summary>
/// The product catalogue's sort configuration. Newest leads and is the default: a storefront visitor
/// arriving without a sort preference should meet the freshest stock first.
/// </summary>
public sealed class ProductSortCatalogue : SortCatalogue<ProductSortOption>
{
    /// <summary>The shared instance — the catalogue is immutable, so one suffices.</summary>
    public static readonly ProductSortCatalogue Instance = new();

    private ProductSortCatalogue() : base(
        ProductSortOption.NewestFirst,
        new(ProductSortOption.NewestFirst, SortSlugs.Newest, nameof(Strings.Sort_Newest)),
        new(ProductSortOption.PriceLowToHigh, SortSlugs.PriceAsc, nameof(Strings.Sort_PriceLowHigh)),
        new(ProductSortOption.PriceHighToLow, SortSlugs.PriceDesc, nameof(Strings.Sort_PriceHighLow)),
        new(ProductSortOption.NameAToZ, SortSlugs.NameAsc, nameof(Strings.Sort_NameAZ)),
        new(ProductSortOption.NameZToA, SortSlugs.NameDesc, nameof(Strings.Sort_NameZA)))
    {
    }
}
