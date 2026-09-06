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

    public const string NameRequired = "Product_NameRequired";
    public const string NameTooLong = "Product_NameTooLong";
    public const string DescriptionTooLong = "Product_DescriptionTooLong";
    public const string SkuRequired = "Product_SkuRequired";
    public const string SkuDuplicatedInRequest = "Product_SkuDuplicatedInRequest";
    public const string SkuAlreadyExists = "Product_SkuAlreadyExists";
    public const string NameAlreadyExists = "Product_NameAlreadyExists";
    public const string CategoryRequired = "Product_CategoryRequired";
    public const string BrandRequired = "Product_BrandRequired";
    public const string PriceRequired = "Product_PriceRequired";
    public const string PriceInvalid = "Product_PriceInvalid";
    public const string SalePriceTooHigh = "Product_SalePriceTooHigh";
    public const string ImageInvalidType = "Product_ImageInvalidType";
    public const string ImageTooLarge = "Product_ImageTooLarge";
    public const string PrimaryImageRequired = "Product_PrimaryImageRequired";
    public const string OptionNameRequired = "Product_OptionNameRequired";
    public const string OptionValueRequired = "Product_OptionValueRequired";
    public const string OptionNameDuplicated = "Product_OptionNameDuplicated";
    public const string OptionValueDuplicated = "Product_OptionValueDuplicated";
    public const string VariantImageNotInGallery = "Product_VariantImageNotInGallery";
    public const string NotPublishable = "Product_NotPublishable";
    public const string ModifiedElsewhere = "Product_ModifiedElsewhere";
    public const string NotFound = "Product_NotFound";
    public const string CreateFailed = "Product_CreateFailed";
    public const string UpdateFailed = "Product_UpdateFailed";
    public const string PageInvalid = "Product_PageInvalid";
}
