using FluentAssertions;
using Newtonsoft.Json;
using TheShop.Domain.Enums;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public class VariantFilterMapperTests
{
    private const string FacetsJson = """
        {"price_min":10,"price_max":80,"option_types":[
          {"key":"variant:colour","name":"Colour <merchant>","values":["Red"],"options":[{"value":"red","label":"Red"}]},
          {"key":"variant:empty","name":"Empty","options":[]},
          {"key":"brand","name":"Not dynamic","options":[{"value":"x","label":"X"}]}]}
        """;

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void FacetRecords_PublicAndAdmin_MapSameDynamicContractWithoutEmptyOrFixedGroups()
    {
        var publicRecord = JsonConvert.DeserializeObject<CatalogueFiltersRecord>(FacetsJson)!;
        var adminRecord = JsonConvert.DeserializeObject<AdminProductFiltersRecord>(FacetsJson)!;

        var groups = publicRecord.OptionTypes.ToVariantGroups();

        groups.Should().ContainSingle();
        groups[0].Key.Should().Be("variant:colour");
        groups[0].Label.Should().Be("Colour <merchant>");
        groups[0].Kind.Should().Be(FilterKind.MultiSelect);
        groups[0].Options.Single().Value.Should().Be("red");
        groups[0].Options.Single().Label.Should().Be("Red");
        adminRecord.ToDto().VariantGroups.Should().BeEquivalentTo(groups);
        adminRecord.ToDto().PriceRange.Should().BeEquivalentTo(new { Min = 10m, Max = 80m });
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void FacetRecords_MissingOptionalFields_DefaultToEmptyChoices()
    {
        JsonConvert.DeserializeObject<CatalogueFiltersRecord>("{}")!.OptionTypes.ToVariantGroups().Should().BeEmpty();
        JsonConvert.DeserializeObject<AdminProductFiltersRecord>("{}")!.ToDto().VariantGroups.Should().BeEmpty();
    }

    [Theory]
    [InlineData("gallery/primary.webp", "legacy.webp", "https://cdn/gallery/primary.webp")]
    [InlineData(null, "legacy.webp", "https://cdn/legacy.webp")]
    [Trait("Feature", "002_variant-filters")]
    public void CatalogueRpcRow_CompleteScalars_PreservesPriceReadModelAndImagePriority(string? primary, string? legacy, string expected)
    {
        var json = JsonConvert.SerializeObject(new
        {
            items = new[] { new { id = Guid.NewGuid(), name = "Product", sku = "PRODUCT", description = "<p>Description</p>",
                original_price = (decimal?)null, sale_price = (decimal?)null, currency = "CAD", is_published = true,
                brand_id = Guid.NewGuid(), brand_name = "Brand", category_id = Guid.NewGuid(), category_name = "Category",
                primary_image_key = primary, image_path = legacy, created_at = "2026-01-01T00:00:00Z",
                min_variant_price = 15m, has_sellable_variant = false } },
            total_count = 7,
        });
        var page = JsonConvert.DeserializeObject<CatalogueProductPageRecord>(json)!;

        var product = page.Items.Single().ToDomain(key => "https://cdn/" + key);

        page.TotalCount.Should().Be(7);
        product.ImageUrl.Should().Be(expected);
        product.HasVariants.Should().BeTrue();
        product.MinVariantPrice!.Amount.Should().Be(15);
        product.HasSellableVariant.Should().BeFalse();
        product.Brand.Name.Should().Be("Brand");
        product.Category.Name.Should().Be("Category");
    }
}
