using System.Text.Json;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class VariantFilterSchemaTests(VariantFilterDatabaseFixture database)
    : IClassFixture<VariantFilterDatabaseFixture>, IAsyncLifetime
{
    private NpgsqlConnection _conn = default!;
    private NpgsqlTransaction _transaction = default!;
    private Guid _brand;
    private Guid _category;
    private Task ExecuteAsync(string sql) => VariantFilterDatabaseFixture.ExecuteAsync(_conn, sql);

    public async ValueTask InitializeAsync()
    {
        _conn = await database.OpenAsync();
        _transaction = await _conn.BeginTransactionAsync();
        _brand = await IdAsync("INSERT INTO brands(name) VALUES ('Brand') RETURNING id");
        _category = await IdAsync("INSERT INTO categories(name) VALUES ('Category') RETURNING id");
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Facets_NormalizedNames_MergeCaseAndSpacesButKeepDifferentSpellings()
    {
        var first = await ProductAsync("First");
        var second = await ProductAsync("Second");
        await VariantAsync(first, true, 10, ("Colour", "Red"));
        await VariantAsync(second, true, 20, (" colour ", " RED "), ("Color", "Blue"));
        await ExecuteAsync("SET LOCAL ROLE anon");

        var groups = await JsonAsync("SELECT public.variant_filter_options(false)");

        groups.GetArrayLength().Should().Be(2);
        var colour = groups.EnumerateArray().Single(g => g.GetProperty("key").GetString() == "variant:colour");
        colour.GetProperty("name").GetString().Should().Be("Colour");
        colour.GetProperty("options").GetArrayLength().Should().Be(1);
        colour.GetProperty("options")[0].GetProperty("value").GetString().Should().Be("red");
        colour.GetProperty("options")[0].GetProperty("label").GetString().Should().Be("RED");
        groups[0].GetProperty("key").GetString().Should().Be("variant:color");
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Facets_UnassignedValuesAndLaterPages_UsesAllActualAssignmentsOnly()
    {
        for (var i = 0; i < 13; i++) await ProductAsync($"Product {i:D2}");
        var later = await ProductAsync("ZZ later");
        await VariantAsync(later, true, 20, ("Size", "L"));
        await ExecuteAsync($"INSERT INTO product_option_values(option_type_id,value,position) SELECT id,'Unused',9 FROM product_option_types WHERE product_id='{later}'");
        await ExecuteAsync("SET LOCAL ROLE anon");
        var page = await CatalogueAsync("{}", limit: 1);
        page.GetProperty("total_count").GetInt32().Should().Be(14);
        var groups = await JsonAsync("SELECT public.variant_filter_options(false)");
        groups.GetArrayLength().Should().Be(1);
        groups[0].GetProperty("values").EnumerateArray().Select(v => v.GetString()).Should().Equal("L");
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Facets_SavedActivationAndDeletion_RefreshesChoicesAndRemovesEmptyGroups()
    {
        var product = await ProductAsync("Product");
        var variant = await VariantAsync(product, true, 10, ("New type", "New value"));
        (await JsonAsync("SELECT public.variant_filter_options(false)")).GetArrayLength().Should().Be(1);
        await ExecuteAsync($"UPDATE product_variants SET is_available=false WHERE id='{variant}'");
        (await JsonAsync("SELECT public.variant_filter_options(false)")).GetArrayLength().Should().Be(0);
        await AuthorizeAsync(true);
        (await JsonAsync("SELECT public.variant_filter_options(true)")).GetArrayLength().Should().Be(1);
        await ExecuteAsync("RESET ROLE");
        await ExecuteAsync($"DELETE FROM product_variants WHERE id='{variant}'");
        await AuthorizeAsync(true);
        (await JsonAsync("SELECT public.variant_filter_options(true)")).GetArrayLength().Should().Be(0);
        (await AdminAsync("{\"variant:new type\":[\"new value\"]}")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Catalogue_MultipleValuesAndGroups_RequiresOneVariantToMatchEveryGroup()
    {
        var match = await ProductAsync("Red Large");
        var split = await ProductAsync("Split");
        var blue = await ProductAsync("Blue Large");
        await VariantAsync(match, true, 10, ("Colour", "Red"), ("Size", "L"));
        await VariantAsync(split, true, 10, ("Colour", "Red"), ("Size", "S"));
        await VariantAsync(split, true, 20, ("Colour", "Blue"), ("Size", "L"));
        await VariantAsync(blue, true, 10, ("Colour", "Blue"), ("Size", "L"));
        await ExecuteAsync("SET LOCAL ROLE anon");

        var andPage = await CatalogueAsync("{\"variant:colour\":[\"red\"],\"variant:size\":[\"l\"]}");
        Names(andPage).Should().Equal("Red Large");
        var orPage = await CatalogueAsync("{\"variant:colour\":[\"red\",\"blue\"],\"variant:size\":[\"l\"]}");
        Names(orPage).Should().BeEquivalentTo(["Red Large", "Split", "Blue Large"]);
        (await CatalogueAsync("{\"variant:colour\":[\"green\"]}")).GetProperty("total_count").GetInt32().Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Catalogue_DuplicateMatchesAndNoVariants_CountsProductsOnceBeforePagination()
    {
        var match = await ProductAsync("Matches");
        await ProductAsync("Plain");
        await VariantAsync(match, true, 10, ("Colour", "Red"));
        await VariantAsync(match, true, 20, ("Colour", "Red"));
        await ExecuteAsync("SET LOCAL ROLE anon");

        var filtered = await CatalogueAsync("{\"variant:colour\":[\"red\"]}", limit: 1);
        filtered.GetProperty("total_count").GetInt32().Should().Be(1);
        Names(filtered).Should().Equal("Matches");
        var outsidePage = await CatalogueAsync("{\"variant:colour\":[\"red\"]}", offset: 9);
        outsidePage.GetProperty("total_count").GetInt32().Should().Be(1);
        Names(outsidePage).Should().BeEmpty();
        (await CatalogueAsync("{}")).GetProperty("total_count").GetInt32().Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "002_variant-filters")]
    public async Task PublicScope_InactiveAndUnpublished_RemainsRestrictedEvenForStaff(bool staff)
    {
        var a = await ProductAsync("A");
        var b = await ProductAsync("B");
        var hidden = await ProductAsync("Unpublished", published: false);
        await VariantAsync(a, true, 10, ("Colour", "Red"));
        await VariantAsync(a, false, 10, ("Colour", "Blue"));
        await VariantAsync(a, false, 10, ("Colour", "Inactive exclusive"));
        await VariantAsync(b, true, 10, ("Colour", "Blue"));
        await VariantAsync(hidden, true, 10, ("Secret", "Exclusive"));
        await AuthorizeAsync(staff);

        Names(await CatalogueAsync("{\"variant:colour\":[\"blue\"]}")).Should().Equal("B");
        var publicChoices = (await JsonAsync("SELECT public.variant_filter_options(false)")).ToString();
        publicChoices.Should().NotContain("Exclusive").And.NotContain("Inactive exclusive");
        if (staff)
        {
            var admin = await AdminAsync("{\"variant:colour\":[\"blue\"]}");
            admin.EnumerateArray().Select(i => i.GetProperty("name").GetString()).Should().Equal("A", "B");
            (await JsonAsync("SELECT public.get_admin_product_filters()")).ToString().Should().Contain("Exclusive").And.Contain("Inactive exclusive");
        }
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Catalogue_ExistingCriteriaAndPrices_FiltersBeforePagingWithoutChangingDisplayPrice()
    {
        var one = await ProductAsync("One", price: 30);
        var two = await ProductAsync("Two", price: 20);
        var excluded = await ProductAsync("Excluded", price: 5);
        foreach (var id in new[] { one, two, excluded }) await VariantAsync(id, true, 99, ("Colour", "Red"));
        await ExecuteAsync("SET LOCAL ROLE anon");

        var page = await CatalogueAsync("{\"variant:colour\":[\"red\"]}", limit: 1, min: 20, max: 30, sort: "price-asc");
        Names(page).Should().Equal("Two");
        page.GetProperty("total_count").GetInt32().Should().Be(2);
        page.GetProperty("items")[0].GetProperty("display_price").GetDecimal().Should().Be(20);
        Names(await CatalogueAsync("{\"variant:colour\":[\"red\"]}", limit: 1, offset: 1, min: 20, max: 30, sort: "price-asc")).Should().Equal("One");
        (await CatalogueAsync("{\"variant:colour\":[\"red\"]}", otherBrand: true)).GetProperty("total_count").GetInt32().Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Admin_ExistingCriteriaAndRollups_PreservesFullRangeAndLegacyCall()
    {
        var product = await ProductAsync("Needle", published: false);
        await VariantAsync(product, false, 10, ("Colour", "Blue"));
        await VariantAsync(product, true, 40, ("Colour", "Red"));
        await AuthorizeAsync(true);

        var rows = await JsonAsync($$"""
            SELECT coalesce(jsonb_agg(r),'[]') FROM public.admin_products_page(' needle ','inactive',
                ARRAY['{{_brand}}']::uuid[],ARRAY['{{_category}}']::uuid[],40,40,'price-asc',1,0,
                '{"variant:colour":["blue"]}'::jsonb) r
            """);
        rows.GetArrayLength().Should().Be(1);
        rows[0].GetProperty("variant_count").GetInt32().Should().Be(2);
        rows[0].GetProperty("min_price").GetDecimal().Should().Be(10);
        rows[0].GetProperty("max_price").GetDecimal().Should().Be(40);
        rows[0].GetProperty("total_count").GetInt32().Should().Be(1);
        (await JsonAsync("SELECT count(*)::text FROM public.admin_products_page(NULL,NULL,NULL,NULL,NULL,NULL,'name-asc',10,0)")).GetInt32().Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task DeletedProduct_RemovesChoicesAndMatchesFromBothScopes()
    {
        var product = await ProductAsync("Removed");
        await VariantAsync(product, true, 10, ("Colour", "Green"));
        await ExecuteAsync($"DELETE FROM products WHERE id='{product}'");
        (await JsonAsync("SELECT public.variant_filter_options(false)")).GetArrayLength().Should().Be(0);
        await AuthorizeAsync(true);
        (await JsonAsync("SELECT public.variant_filter_options(true)")).GetArrayLength().Should().Be(0);
        (await AdminAsync("{\"variant:colour\":[\"green\"]}")).GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("anon", "SELECT public.get_admin_product_filters()")]
    [InlineData("anon", "SELECT * FROM public.admin_products_page(NULL,NULL,NULL,NULL,NULL,NULL,'name-asc',10,0)")]
    [InlineData("authenticated", "SELECT public.variant_filter_options(true)")]
    [InlineData("authenticated", "SELECT public.product_matches_variant_filters(gen_random_uuid(),'{}',true)")]
    [InlineData("authenticated", "SELECT public.get_admin_product_filters()")]
    [InlineData("authenticated", "SELECT * FROM public.admin_products_page(NULL,NULL,NULL,NULL,NULL,NULL,'name-asc',10,0)")]
    [Trait("Feature", "002_variant-filters")]
    public async Task AdminScope_UnauthorizedCaller_DeniesAccess(string role, string sql)
    {
        await ExecuteAsync($"SET LOCAL ROLE {role}");
        var act = () => ExecuteAsync(sql);
        await act.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == "42501");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"variant:colour\":\"red\"}")]
    [Trait("Feature", "002_variant-filters")]
    public async Task Matcher_InvalidJsonShape_RejectsInput(string filters)
    {
        await using var cmd = new NpgsqlCommand("SELECT public.product_matches_variant_filters(gen_random_uuid(),$1::jsonb,false)", _conn);
        cmd.Parameters.AddWithValue(filters);
        var act = () => cmd.ExecuteScalarAsync();
        await act.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == "22023");
    }

    private async Task<Guid> ProductAsync(string name, bool published = true, decimal price = 25)
    {
        await using var cmd = new NpgsqlCommand("INSERT INTO products(name,brand_id,category_id,is_published,original_price) VALUES ($1,$2,$3,$4,$5) RETURNING id", _conn);
        cmd.Parameters.AddWithValue(name); cmd.Parameters.AddWithValue(_brand); cmd.Parameters.AddWithValue(_category);
        cmd.Parameters.AddWithValue(published); cmd.Parameters.AddWithValue(price);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<Guid> VariantAsync(Guid product, bool active, decimal price, params (string Type, string Value)[] options)
    {
        var variant = await IdAsync($"INSERT INTO product_variants(product_id,sku,position,is_available,original_price) VALUES ('{product}','SKU',0,{active.ToString().ToLowerInvariant()},{price}) RETURNING id");
        foreach (var option in options)
        {
            await using var cmd = new NpgsqlCommand("""
                WITH t AS (INSERT INTO product_option_types(product_id,name,position) VALUES ($1,$2,0)
                  ON CONFLICT (product_id,lower(btrim(name))) DO UPDATE SET name=product_option_types.name RETURNING id),
                o AS (INSERT INTO product_option_values(option_type_id,value,position) SELECT id,$3,0 FROM t
                  ON CONFLICT (option_type_id,lower(btrim(value))) DO UPDATE SET value=product_option_values.value RETURNING id)
                INSERT INTO product_variant_option_values(variant_id,option_value_id) SELECT $4,id FROM o
                """, _conn);
            cmd.Parameters.AddWithValue(product); cmd.Parameters.AddWithValue(option.Type);
            cmd.Parameters.AddWithValue(option.Value); cmd.Parameters.AddWithValue(variant);
            await cmd.ExecuteNonQueryAsync();
        }
        return variant;
    }

    private async Task AuthorizeAsync(bool staff)
    {
        await ExecuteAsync("RESET ROLE");
        await RbacTestSchema.ClearJwtClaimsAsync(_conn);
        var user = await RbacTestSchema.InsertAuthUserAsync(_conn);
        if (staff) await RbacTestSchema.AssignRoleByNameAsync(_conn, user, "Admin");
        await RbacTestSchema.SetJwtClaimsAsync(_conn, user, Guid.Empty);
        await ExecuteAsync("SET LOCAL ROLE authenticated");
    }

    private async Task<Guid> IdAsync(string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, _conn);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<JsonElement> JsonAsync(string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, _conn);
        using var document = JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync())!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CatalogueAsync(string filters, int limit = 12, int offset = 0,
        int? min = null, int? max = null, string sort = "name-asc", bool otherBrand = false) =>
        JsonAsync($"""
            SELECT public.catalogue_products_page(ARRAY['{(otherBrand ? Guid.Empty : _brand)}']::uuid[],
                ARRAY['{_category}']::uuid[],{min?.ToString() ?? "NULL"},{max?.ToString() ?? "NULL"},'{sort}',{limit},{offset},'{filters}'::jsonb)
            """);

    private Task<JsonElement> AdminAsync(string filters) => JsonAsync($"""
        SELECT coalesce(jsonb_agg(r),'[]') FROM public.admin_products_page(NULL,NULL,NULL,NULL,NULL,NULL,'name-asc',10,0,'{filters}'::jsonb) r
        """);

    private static IEnumerable<string?> Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString());
}
