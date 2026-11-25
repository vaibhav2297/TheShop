using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

internal sealed class CatalogueProductRowRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
    [JsonProperty("sku")]
    public string Sku { get; set; } = string.Empty;
    [JsonProperty("original_price")]
    public decimal? OriginalPrice { get; set; }
    [JsonProperty("sale_price")]
    public decimal? SalePrice { get; set; }
    [JsonProperty("currency")]
    public string Currency { get; set; } = "CAD";
    [JsonProperty("is_published")]
    public bool IsPublished { get; set; }
    [JsonProperty("brand_id")]
    public Guid BrandId { get; set; }
    [JsonProperty("brand_name")]
    public string BrandName { get; set; } = string.Empty;
    [JsonProperty("category_id")]
    public Guid CategoryId { get; set; }
    [JsonProperty("category_name")]
    public string CategoryName { get; set; } = string.Empty;
    [JsonProperty("primary_image_key")]
    public string? PrimaryImageKey { get; set; }
    [JsonProperty("image_path")]
    public string? ImagePath { get; set; }
    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("min_variant_price")]
    public decimal? MinVariantPrice { get; set; }
    [JsonProperty("has_sellable_variant")]
    public bool HasSellableVariant { get; set; }
}
