namespace TheShop.Application.Features.Products;

/// <summary>
/// String constants matching the resx keys in <c>Strings.resx</c> for product-catalogue
/// errors. Kept here because the Application layer cannot reference the Web project's typed
/// <c>Strings</c> accessor. Keys are surfaced to the UI verbatim and resolved via
/// <c>Localizer[result.Error]</c>.
/// </summary>
public static class ProductErrorKeys
{
    public const string CataloguePageInvalid = "Catalogue_Page_Invalid";
    public const string CataloguePageSizeInvalid = "Catalogue_PageSize_Invalid";
    public const string CataloguePriceRangeInvalid = "Catalogue_PriceRange_Invalid";
    public const string CatalogueSortInvalid = "Catalogue_Sort_Invalid";
    public const string CatalogueFilterInvalid = "Catalogue_Filter_Invalid";
}
