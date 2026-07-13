using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Deserialization target for the <c>get_catalogue_filters()</c> RPC — the entire catalogue
/// filter sidebar (category, brand, flavour, nicotine options plus the price range) in one JSON
/// payload. Not a table model; mapped to <c>FilterGroupDto</c>s by
/// <see cref="Repositories.SupabaseProductRepository"/> via <see cref="ProductFilterDefinitions"/>.
/// </summary>
internal sealed class CatalogueFiltersRecord
{
    [JsonProperty("categories")]
    public IReadOnlyList<FilterLookupRecord> Categories { get; set; } = [];

    [JsonProperty("brands")]
    public IReadOnlyList<FilterLookupRecord> Brands { get; set; } = [];

    [JsonProperty("flavours")]
    public IReadOnlyList<string> Flavours { get; set; } = [];

    [JsonProperty("nicotine_strengths")]
    public IReadOnlyList<int> NicotineStrengths { get; set; } = [];

    [JsonProperty("price_min")]
    public decimal PriceMin { get; set; }

    [JsonProperty("price_max")]
    public decimal PriceMax { get; set; }
}

/// <summary>An <c>{ id, name }</c> lookup row (a category or brand) from <c>get_catalogue_filters()</c>.</summary>
internal sealed class FilterLookupRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
