using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Deserialization target for the <c>get_catalogue_filters()</c> RPC — the catalogue filter
/// sidebar's category/brand lookups and price range, plus the generic per-product option types
/// published products carry (Decision 3 — replaces the retired flavour/nicotine facets; the
/// product-catalogue feature builds its dynamic filter controls from <see cref="OptionTypes"/>).
/// Not a table model; mapped to <c>FilterGroupDto</c>s by
/// <see cref="Repositories.SupabaseProductRepository"/> via <see cref="Filtering.ProductFilterDefinitions"/>.
/// </summary>
internal sealed class CatalogueFiltersRecord
{
    [JsonProperty("categories")]
    public IReadOnlyList<FilterLookupRecord> Categories { get; set; } = [];

    [JsonProperty("brands")]
    public IReadOnlyList<FilterLookupRecord> Brands { get; set; } = [];

    [JsonProperty("option_types")]
    public IReadOnlyList<OptionTypeLookupRecord> OptionTypes { get; set; } = [];

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

/// <summary>An <c>{ name, values[] }</c> option type row from <c>get_catalogue_filters()</c>.</summary>
internal sealed class OptionTypeLookupRecord
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("values")]
    public IReadOnlyList<string> Values { get; set; } = [];
}
