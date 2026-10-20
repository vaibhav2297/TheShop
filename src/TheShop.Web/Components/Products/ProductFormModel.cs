namespace TheShop.Web.Components.Products;

/// <summary>Editable product fields; request rules remain in the Application validators.</summary>
public sealed class ProductFormModel
{
    /// <summary>Product name used to suggest its SKU.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Generated stock identifier.</summary>
    public string Sku { get; set; } = string.Empty;
    /// <summary>Rich-text description.</summary>
    public string? Description { get; set; }
    /// <summary>Selected category.</summary>
    public Guid? CategoryId { get; set; }
    /// <summary>Selected brand.</summary>
    public Guid? BrandId { get; set; }
    /// <summary>Price used when the product has no variants.</summary>
    public decimal? OriginalPrice { get; set; }
    /// <summary>Optional sale price used when the product has no variants.</summary>
    public decimal? SalePrice { get; set; }
    /// <summary>Requested publication state.</summary>
    public bool IsPublished { get; set; }
}
