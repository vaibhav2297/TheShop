using System.Globalization;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the manage-products schema deltas (.specs/manage-product/plan.md §10):
/// the <c>idx_products_created_at</c> index, the <c>admin_products_page(...)</c> /
/// <c>get_admin_product_filters()</c> / <c>delete_products(uuid[])</c> <c>SECURITY DEFINER</c> RPCs
/// that answer RULE-9/10/11 questions no client-visible RLS policy or PostgREST filter can (plan §5
/// Decision 1), and the <c>products_admin_delete</c> policy. Exercised in production by
/// <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseProductRepository"/>.
///
/// Spins up a real Postgres container (Testcontainers), reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (the same mechanism <see cref="ManageBrandsSchemaTests"/> uses),
/// then layers a minimal <c>brands</c>/<c>categories</c>/<c>products</c>/<c>product_variants</c>/
/// <c>product_images</c> set on top — just enough for the three RPCs to answer against. FR-12's
/// reference-blocked deletion (RULE-3/RULE-4) cannot be proven at this level today — no table
/// references <c>products</c> yet (plan §11 accepted risk) — so that path is proven at handler and
/// component level instead (<c>DeleteProductsHandlerTests</c>, <c>ManageProductsTests</c>).
/// <see href=".specs/manage-product/spec.md"/>
/// <see href=".specs/manage-product/plan.md"/>
/// </summary>
public sealed class ManageProductsSchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("shop_test")
        .WithUsername("shop")
        .WithPassword("shop")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(conn);
        await RbacTestSchema.ApplyRbacCoreAsync(conn);
        await RbacTestSchema.CreateRlsTestRolesAsync(conn);
        await ApplyManageProductsSchemaAsync(conn);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    // =========================================================================
    // idx_products_created_at — serves the Newest/Oldest sorts (plan §4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Schema_IdxProductsCreatedAt_Exists()
    {
        await using var conn = await OpenAsync();

        var count = await ScalarAsync(conn, "SELECT COUNT(*) FROM pg_indexes WHERE indexname = 'idx_products_created_at'");

        Convert.ToInt32(count).Should().Be(1);
    }

    // =========================================================================
    // products_admin_delete RLS (admin enforcement Layer 4 behind the RPC)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteProductRow_WhenCallerHoldsProductsDeletePermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var id = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await AuthorizeAsAsync(conn, "products.delete");

        var act = () => DeleteProductRowAsync(conn, id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteProductRow_WhenCallerLacksProductsDeletePermission_TheDeleteAffectsNoRows()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var id = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var affected = await DeleteProductRowAsync(conn, id);

        affected.Should().Be(0);
    }

    // =========================================================================
    // admin_products_page — access boundary (AC-18)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WhenCallerLacksProductsViewPermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallAdminProductsPageAsync(conn);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // admin_products_page — both statuses, name ascending default, total count (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithElevenProducts_ReturnsFirstTenNameAscendingWithTheFullTotal()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        for (var i = 1; i <= 11; i++)
            await InsertProductAsync(conn, brandId, categoryId, $"Product {i:D2}", isPublished: i % 2 == 0);
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, sort: "name-asc", limit: 10, offset: 0);

        rows.Should().HaveCount(10);
        rows.Select(r => r.Name).Should().BeInAscendingOrder();
        rows[0].TotalCount.Should().Be(11);
        rows.Should().Contain(r => !r.IsPublished, "both statuses are eligible (AC-1)");
        rows.Should().Contain(r => r.IsPublished);
    }

    // =========================================================================
    // admin_products_page — search trims and ignores case (AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_SearchWithSurroundingSpacesAndMixedCase_MatchesContainingNames()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        await InsertProductAsync(conn, brandId, categoryId, "Cool Mint Ice");
        await InsertProductAsync(conn, brandId, categoryId, "Berry Blast");
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, search: " MINT ");

        rows.Should().ContainSingle(r => r.Name == "Cool Mint Ice");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithBlankSearch_AppliesNoNarrowing()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        await InsertProductAsync(conn, brandId, categoryId, "Cool Mint Ice");
        await InsertProductAsync(conn, brandId, categoryId, "Berry Blast");
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, search: "   ");

        rows.Should().HaveCount(2);
    }

    // =========================================================================
    // admin_products_page — status/brand/category combine (AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithStatusAndBrandAndCategoryFilters_ReturnsOnlyProductsSatisfyingAll()
    {
        await using var conn = await OpenAsync();
        var (brandA, categoryA) = await SeedBrandAndCategoryAsync(conn, "Elf Bar", "Disposables");
        var (brandB, categoryB) = await SeedBrandAndCategoryAsync(conn, "Lost Mary", "Pods");
        var matchId = await InsertProductAsync(conn, brandA, categoryA, "Elf Bar Active", isPublished: true);
        await InsertProductAsync(conn, brandA, categoryA, "Elf Bar Inactive", isPublished: false);
        await InsertProductAsync(conn, brandB, categoryB, "Lost Mary Active", isPublished: true);
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(
            conn, status: "active", brandIds: [brandA], categoryIds: [categoryA]);

        rows.Should().ContainSingle(r => r.Id == matchId);
    }

    // =========================================================================
    // admin_products_page — price bounds match an actual variant price, inclusive (AC-26)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithPriceFilterBetweenTwoVariantPrices_ExcludesTheProduct()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await InsertVariantAsync(conn, productId, 10m, null);
        await InsertVariantAsync(conn, productId, 30m, null);
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, priceMin: 15m, priceMax: 25m);

        rows.Should().BeEmpty("neither actual variant price (10 or 30) falls inside 15-25");
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithPriceFilterMatchingAVariantPriceExactly_IncludesTheProductInclusively(
        decimal min, decimal max)
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await InsertVariantAsync(conn, productId, 10m, null);
        await InsertVariantAsync(conn, productId, 30m, null);
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, priceMin: min, priceMax: max);

        rows.Should().ContainSingle(r => r.Id == productId, "RULE-10 bounds are inclusive of the endpoints");
    }

    // =========================================================================
    // admin_products_page — filtering never narrows the displayed range or count (AC-27, AC-29)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithPriceFilterMatchingAMiddleVariant_KeepsTheFullRangeAndCount()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await InsertVariantAsync(conn, productId, 10m, null);
        await InsertVariantAsync(conn, productId, 20m, null);
        await InsertVariantAsync(conn, productId, 30m, null);
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, priceMin: 15m, priceMax: 25m);

        var row = rows.Should().ContainSingle(r => r.Id == productId).Which;
        row.MinPrice.Should().Be(10m);
        row.MaxPrice.Should().Be(30m);
        row.VariantCount.Should().Be(3);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_ADeletedVariantContributesNeitherPriceNorCount()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await InsertVariantAsync(conn, productId, 20m, null);
        await InsertVariantAsync(conn, productId, 10m, null);
        var deletedVariantId = await InsertVariantAsync(conn, productId, 100m, null);
        await DeleteVariantRowAsync(conn, deletedVariantId);
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, priceMin: 30m, priceMax: 30m);

        rows.Should().BeEmpty("the deleted CAD 100 variant no longer has a row to contribute a price");

        var unfiltered = await CallAdminProductsPageAsync(conn);
        var row = unfiltered.Should().ContainSingle(r => r.Id == productId).Which;
        row.MinPrice.Should().Be(10m);
        row.MaxPrice.Should().Be(20m);
        row.VariantCount.Should().Be(2);
    }

    // =========================================================================
    // admin_products_page — a sale price is the variant's current selling price (AC-24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WhenAVariantHasASalePrice_UsesTheSalePriceForTheRange()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await InsertVariantAsync(conn, productId, 10m, null);
        await InsertVariantAsync(conn, productId, 30m, salePrice: 25m);
        await AuthorizeAsAsync(conn, "products.view");

        var row = (await CallAdminProductsPageAsync(conn)).Single(r => r.Id == productId);

        row.MinPrice.Should().Be(10m);
        row.MaxPrice.Should().Be(25m);
    }

    // =========================================================================
    // admin_products_page — a zero-variant product prices itself (plan §5 Decision 13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithNoVariants_UsesItsOwnEffectivePriceForBothEnds()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000", originalPrice: 24.99m);
        await AuthorizeAsAsync(conn, "products.view");

        var row = (await CallAdminProductsPageAsync(conn)).Single(r => r.Id == productId);

        row.MinPrice.Should().Be(24.99m);
        row.MaxPrice.Should().Be(24.99m);
        row.VariantCount.Should().Be(0);
    }

    // =========================================================================
    // admin_products_page — sort orders (AC-5, AC-28)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_SortedNameDescending_ReturnsReverseAlphabeticalOrder()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        await InsertProductAsync(conn, brandId, categoryId, "Alpha");
        await InsertProductAsync(conn, brandId, categoryId, "Beta");
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, sort: "name-desc");

        rows.Select(r => r.Name).Should().ContainInOrder("Beta", "Alpha");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_SortedOldestFirst_ReturnsCreationOrderAscending()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var olderId = await InsertProductAsync(conn, brandId, categoryId, "Older", createdAt: DateTimeOffset.UtcNow.AddDays(-2));
        var newerId = await InsertProductAsync(conn, brandId, categoryId, "Newer", createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, sort: "oldest");

        rows.Select(r => r.Id).Should().ContainInOrder(olderId, newerId);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_SortedByLowestVariantPriceUnderAFilter_OrdersOnTheUnfilteredLowestPrice()
    {
        // AC-28: product A (10, 100) and product B (20, 30) under a 25-100 filter — ascending
        // places A before B (A's lowest price, 10, is lower than B's, 20), never the filtered range.
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var productA = await InsertProductAsync(conn, brandId, categoryId, "Product A");
        await InsertVariantAsync(conn, productA, 10m, null);
        await InsertVariantAsync(conn, productA, 100m, null);
        var productB = await InsertProductAsync(conn, brandId, categoryId, "Product B");
        await InsertVariantAsync(conn, productB, 20m, null);
        await InsertVariantAsync(conn, productB, 30m, null);
        await AuthorizeAsAsync(conn, "products.view");

        var ascending = await CallAdminProductsPageAsync(conn, priceMin: 25m, priceMax: 100m, sort: "price-asc");
        var descending = await CallAdminProductsPageAsync(conn, priceMin: 25m, priceMax: 100m, sort: "price-desc");

        ascending.Select(r => r.Id).Should().ContainInOrder(productA, productB);
        descending.Select(r => r.Id).Should().ContainInOrder(productB, productA);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductsPage_WithAnUnrecognizedSort_FallsBackToNameAscending()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        await InsertProductAsync(conn, brandId, categoryId, "Beta");
        await InsertProductAsync(conn, brandId, categoryId, "Alpha");
        await AuthorizeAsAsync(conn, "products.view");

        var rows = await CallAdminProductsPageAsync(conn, sort: "unknown-slug");

        rows.Select(r => r.Name).Should().ContainInOrder("Alpha", "Beta");
    }

    // =========================================================================
    // get_admin_product_filters() — access boundary and option sourcing (plan §5 Decision 7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductFilters_WhenCallerLacksProductsViewPermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallAdminProductFiltersAsync(conn);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductFilters_ReturnsOnlyBrandsAndCategoriesThatOwnAProduct()
    {
        await using var conn = await OpenAsync();
        var (ownedBrand, ownedCategory) = await SeedBrandAndCategoryAsync(conn, "Elf Bar", "Disposables");
        var (idleBrand, idleCategory) = await SeedBrandAndCategoryAsync(conn, "Idle Brand", "Idle Category");
        await InsertProductAsync(conn, ownedBrand, ownedCategory, "Elf Bar BC5000");
        await AuthorizeAsAsync(conn, "products.view");

        var (brands, categories, _, _) = await CallAdminProductFiltersAsync(conn);

        brands.Should().Contain("Elf Bar").And.NotContain("Idle Brand");
        categories.Should().Contain("Disposables").And.NotContain("Idle Category");
        _ = idleBrand;
        _ = idleCategory;
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task AdminProductFilters_PriceBoundsSpanVariantAndZeroVariantProductPrices()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var withVariants = await InsertProductAsync(conn, brandId, categoryId, "Has Variants");
        await InsertVariantAsync(conn, withVariants, 5m, null);
        await InsertVariantAsync(conn, withVariants, 40m, null);
        await InsertProductAsync(conn, brandId, categoryId, "No Variants", originalPrice: 60m);
        await AuthorizeAsAsync(conn, "products.view");

        var (_, _, priceMin, priceMax) = await CallAdminProductFiltersAsync(conn);

        priceMin.Should().Be(5m);
        priceMax.Should().Be(60m);
    }

    // =========================================================================
    // delete_products(uuid[]) — deletable product, image key disposal, access boundary
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteProducts_WhenTheProductHasNoReferences_DeletesItAndReportsDeletedTrue()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var id = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await AuthorizeAsAsync(conn, "products.delete");

        var rows = await CallDeleteProductsAsync(conn, [id]);

        rows.Should().ContainSingle(r => r.Id == id && r.Deleted);
        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM products WHERE id = '{id}'");
        Convert.ToInt32(count).Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteProducts_ReturnsTheDeletedProductsImageKeysForDisposal()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var id = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await InsertImageAsync(conn, id, "products/abc/1.webp", isPrimary: true);
        await InsertImageAsync(conn, id, "products/abc/2.webp", isPrimary: false);
        await AuthorizeAsAsync(conn, "products.delete");

        var rows = await CallDeleteProductsAsync(conn, [id]);

        rows.Single(r => r.Id == id).ImageKeys.Should()
            .BeEquivalentTo(["products/abc/1.webp", "products/abc/2.webp"]);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteProducts_ForMultipleIds_DeletesEachOneInTheSameCall()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var firstId = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        var secondId = await InsertProductAsync(conn, brandId, categoryId, "Lost Mary OS5000");
        await AuthorizeAsAsync(conn, "products.delete");

        var rows = await CallDeleteProductsAsync(conn, [firstId, secondId]);

        rows.Should().OnlyContain(r => r.Deleted);
        var count = await ScalarAsync(conn, "SELECT COUNT(*) FROM products");
        Convert.ToInt32(count).Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteProducts_WhenCallerLacksProductsDeletePermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync(conn);
        var id = await InsertProductAsync(conn, brandId, categoryId, "Elf Bar BC5000");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallDeleteProductsAsync(conn, [id]);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    /// <summary>Grants the caller a fresh Admin-rooted session limited to one permission (or none).</summary>
    private static async Task AuthorizeAsAsync(NpgsqlConnection conn, string? permissionCode)
    {
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        if (permissionCode is not null)
        {
            await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        }
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE authenticated";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<(Guid BrandId, Guid CategoryId)> SeedBrandAndCategoryAsync(
        NpgsqlConnection conn, string brandName = "Elf Bar", string categoryName = "Disposables")
    {
        await using var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = $"INSERT INTO brands (name) VALUES ('{brandName.Replace("'", "''")}') RETURNING id";
        var brandId = (Guid)(await brandCmd.ExecuteScalarAsync())!;

        await using var categoryCmd = conn.CreateCommand();
        categoryCmd.CommandText = $"INSERT INTO categories (name) VALUES ('{categoryName.Replace("'", "''")}') RETURNING id";
        var categoryId = (Guid)(await categoryCmd.ExecuteScalarAsync())!;

        return (brandId, categoryId);
    }

    private static async Task<Guid> InsertProductAsync(
        NpgsqlConnection conn, Guid brandId, Guid categoryId, string name,
        bool isPublished = true, decimal? originalPrice = 24.99m, decimal? salePrice = null,
        DateTimeOffset? createdAt = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO products (name, sku, brand_id, category_id, is_published, original_price, sale_price, created_at)
            VALUES (
                '{name.Replace("'", "''")}', '{Guid.NewGuid()}', '{brandId}', '{categoryId}',
                {(isPublished ? "true" : "false")}, {SqlDecimal(originalPrice)}, {SqlDecimal(salePrice)},
                '{(createdAt ?? DateTimeOffset.UtcNow):O}')
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertVariantAsync(
        NpgsqlConnection conn, Guid productId, decimal? originalPrice, decimal? salePrice)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO product_variants (product_id, original_price, sale_price)
            VALUES ('{productId}', {SqlDecimal(originalPrice)}, {SqlDecimal(salePrice)})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task DeleteVariantRowAsync(NpgsqlConnection conn, Guid variantId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM product_variants WHERE id = '{variantId}'";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertImageAsync(NpgsqlConnection conn, Guid productId, string objectKey, bool isPrimary)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO product_images (product_id, object_key, is_primary)
            VALUES ('{productId}', '{objectKey}', {(isPrimary ? "true" : "false")})
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> DeleteProductRowAsync(NpgsqlConnection conn, Guid id)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM products WHERE id = '{id}'";
        return await cmd.ExecuteNonQueryAsync();
    }

    private static string SqlText(string? value) => value is null ? "NULL" : $"'{value.Replace("'", "''")}'";

    private static string SqlDecimal(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";

    private static string SqlUuidArray(IEnumerable<Guid>? ids) =>
        ids is null ? "NULL" : $"ARRAY[{string.Join(",", ids.Select(id => $"'{id}'"))}]::uuid[]";

    private sealed record AdminProductPageRow(
        Guid Id, string Name, int VariantCount, decimal? MinPrice, decimal? MaxPrice, bool IsPublished, long TotalCount);

    private static async Task<List<AdminProductPageRow>> CallAdminProductsPageAsync(
        NpgsqlConnection conn,
        string? search = null,
        string? status = null,
        IEnumerable<Guid>? brandIds = null,
        IEnumerable<Guid>? categoryIds = null,
        decimal? priceMin = null,
        decimal? priceMax = null,
        string sort = "name-asc",
        int limit = 10,
        int offset = 0)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, name, variant_count, min_price, max_price, is_published, total_count
            FROM public.admin_products_page(
                {SqlText(search)}, {SqlText(status)}, {SqlUuidArray(brandIds)}, {SqlUuidArray(categoryIds)},
                {SqlDecimal(priceMin)}, {SqlDecimal(priceMax)}, {SqlText(sort)}, {limit}, {offset})
            """;

        var rows = new List<AdminProductPageRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new AdminProductPageRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.GetBoolean(5),
                reader.GetInt64(6)));
        }
        return rows;
    }

    private static async Task<(List<string> Brands, List<string> Categories, decimal PriceMin, decimal PriceMax)>
        CallAdminProductFiltersAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT public.get_admin_product_filters()";
        var json = (string)(await cmd.ExecuteScalarAsync())!;
        var doc = System.Text.Json.JsonDocument.Parse(json);

        var brands = doc.RootElement.GetProperty("brands").EnumerateArray()
            .Select(b => b.GetProperty("name").GetString()!).ToList();
        var categories = doc.RootElement.GetProperty("categories").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()!).ToList();
        var priceMin = doc.RootElement.GetProperty("price_min").GetDecimal();
        var priceMax = doc.RootElement.GetProperty("price_max").GetDecimal();

        return (brands, categories, priceMin, priceMax);
    }

    private sealed record DeleteProductsRow(Guid Id, string Name, long ReferenceCount, bool Deleted, List<string> ImageKeys);

    private static async Task<List<DeleteProductsRow>> CallDeleteProductsAsync(NpgsqlConnection conn, IEnumerable<Guid> ids)
    {
        var idArray = string.Join(",", ids.Select(id => $"'{id}'"));
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, name, reference_count, deleted, image_keys FROM public.delete_products(ARRAY[{idArray}]::uuid[])";

        var rows = new List<DeleteProductsRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var imageKeys = reader.IsDBNull(4)
                ? []
                : ((string[])reader.GetValue(4)).ToList();
            rows.Add(new DeleteProductsRow(
                reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2), reader.GetBoolean(3), imageKeys));
        }
        return rows;
    }

    // =========================================================================
    // Schema setup — the manage-product deltas layered on RbacTestSchema's RBAC core (plan §10):
    // a minimal brands/categories/products/product_variants/product_images set, the
    // products_admin_delete policy, idx_products_created_at, and the three RPCs verbatim from
    // supabase/migrations/0028_manage_products.sql.
    // =========================================================================

    private static async Task ApplyManageProductsSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE brands (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name TEXT NOT NULL
            );

            CREATE TABLE categories (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name TEXT NOT NULL
            );

            CREATE TABLE products (
                id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name           TEXT NOT NULL,
                sku            TEXT NOT NULL,
                brand_id       UUID NOT NULL REFERENCES brands(id),
                category_id    UUID NOT NULL REFERENCES categories(id),
                currency       TEXT NOT NULL DEFAULT 'CAD',
                is_published   BOOLEAN NOT NULL DEFAULT TRUE,
                original_price NUMERIC,
                sale_price     NUMERIC,
                created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at     TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE product_variants (
                id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                product_id     UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                original_price NUMERIC,
                sale_price     NUMERIC
            );

            CREATE TABLE product_images (
                id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                product_id UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                object_key TEXT NOT NULL,
                is_primary BOOLEAN NOT NULL DEFAULT FALSE
            );

            CREATE INDEX idx_products_created_at ON products (created_at DESC);

            ALTER TABLE products ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "products_admin_delete" ON products
                FOR DELETE USING ((SELECT public.authorize('products.delete')));

            -- brands.*/categories.*/products.* permissions and the Admin grant are seeded by
            -- RbacTestSchema (mirroring migrations 0007 + 0015).

            -- Verbatim from supabase/migrations/0028_manage_products.sql (plan §10)

            CREATE OR REPLACE FUNCTION public.admin_products_page(
                p_search       TEXT,
                p_status       TEXT,
                p_brand_ids    UUID[],
                p_category_ids UUID[],
                p_price_min    NUMERIC,
                p_price_max    NUMERIC,
                p_sort         TEXT,
                p_limit        INT,
                p_offset       INT)
            RETURNS TABLE (
                id                UUID,
                name              TEXT,
                sku               TEXT,
                brand_id          UUID,
                brand_name        TEXT,
                category_id       UUID,
                category_name     TEXT,
                currency          TEXT,
                is_published      BOOLEAN,
                primary_image_key TEXT,
                variant_count     INT,
                min_price         NUMERIC,
                max_price         NUMERIC,
                total_count       BIGINT)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            DECLARE
                v_search TEXT := NULLIF(btrim(COALESCE(p_search, '')), '');
            BEGIN
                IF NOT public.authorize('products.view') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                RETURN QUERY
                WITH variant_price AS (
                    SELECT v.product_id, COALESCE(v.sale_price, v.original_price) AS price
                    FROM product_variants v
                ),
                rollup AS (
                    SELECT p.id AS product_id,
                           (SELECT count(*)::int FROM product_variants v WHERE v.product_id = p.id) AS variant_count,
                           COALESCE((SELECT min(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                                    COALESCE(p.sale_price, p.original_price)) AS min_price,
                           COALESCE((SELECT max(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                                    COALESCE(p.sale_price, p.original_price)) AS max_price
                    FROM products p
                ),
                matched AS (
                    SELECT p.id, p.name, p.sku, p.brand_id, b.name AS brand_name,
                           p.category_id, c.name AS category_name, p.currency, p.is_published,
                           (SELECT i.object_key FROM product_images i
                             WHERE i.product_id = p.id AND i.is_primary LIMIT 1) AS primary_image_key,
                           r.variant_count, r.min_price, r.max_price, p.created_at,
                           count(*) OVER () AS total_count
                    FROM products p
                    JOIN brands b     ON b.id = p.brand_id
                    JOIN categories c ON c.id = p.category_id
                    JOIN rollup r     ON r.product_id = p.id
                    WHERE (v_search IS NULL OR p.name ILIKE '%' || v_search || '%')
                      AND (p_status IS NULL OR p.is_published = (p_status = 'active'))
                      AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
                      AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
                      AND (
                            (p_price_min IS NULL AND p_price_max IS NULL)
                            OR EXISTS (
                                SELECT 1 FROM variant_price vp
                                WHERE vp.product_id = p.id
                                  AND vp.price IS NOT NULL
                                  AND (p_price_min IS NULL OR vp.price >= p_price_min)
                                  AND (p_price_max IS NULL OR vp.price <= p_price_max))
                            OR (r.variant_count = 0
                                AND COALESCE(p.sale_price, p.original_price) IS NOT NULL
                                AND (p_price_min IS NULL OR COALESCE(p.sale_price, p.original_price) >= p_price_min)
                                AND (p_price_max IS NULL OR COALESCE(p.sale_price, p.original_price) <= p_price_max))
                          )
                )
                SELECT m.id, m.name, m.sku, m.brand_id, m.brand_name, m.category_id, m.category_name,
                       m.currency, m.is_published, m.primary_image_key,
                       m.variant_count, m.min_price, m.max_price, m.total_count
                FROM matched m
                ORDER BY
                    CASE WHEN p_sort = 'name-desc'  THEN m.name END DESC,
                    CASE WHEN p_sort = 'newest'     THEN m.created_at END DESC,
                    CASE WHEN p_sort = 'oldest'     THEN m.created_at END ASC,
                    CASE WHEN p_sort = 'price-desc' THEN m.min_price END DESC,
                    CASE WHEN p_sort = 'price-asc'  THEN m.min_price END ASC,
                    CASE WHEN p_sort NOT IN ('name-desc','newest','oldest','price-asc','price-desc')
                         THEN m.name END ASC,
                    m.id
                LIMIT p_limit OFFSET p_offset;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.get_admin_product_filters()
            RETURNS JSONB
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            DECLARE
                v_result JSONB;
            BEGIN
                IF NOT public.authorize('products.view') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                SELECT jsonb_build_object(
                    'brands', COALESCE((
                        SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name) ORDER BY b.name)
                        FROM brands b WHERE EXISTS (SELECT 1 FROM products p WHERE p.brand_id = b.id)), '[]'::jsonb),
                    'categories', COALESCE((
                        SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name) ORDER BY c.name)
                        FROM categories c WHERE EXISTS (SELECT 1 FROM products p WHERE p.category_id = c.id)), '[]'::jsonb),
                    'price_min', COALESCE((SELECT min(price) FROM (
                        SELECT COALESCE(v.sale_price, v.original_price) AS price FROM product_variants v
                        UNION ALL
                        SELECT COALESCE(p.sale_price, p.original_price) FROM products p
                         WHERE NOT EXISTS (SELECT 1 FROM product_variants v2 WHERE v2.product_id = p.id)) prices), 0),
                    'price_max', COALESCE((SELECT max(price) FROM (
                        SELECT COALESCE(v.sale_price, v.original_price) AS price FROM product_variants v
                        UNION ALL
                        SELECT COALESCE(p.sale_price, p.original_price) FROM products p
                         WHERE NOT EXISTS (SELECT 1 FROM product_variants v2 WHERE v2.product_id = p.id)) prices), 0)
                ) INTO v_result;

                RETURN v_result;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.delete_products(product_ids UUID[])
            RETURNS TABLE (id UUID, name TEXT, reference_count BIGINT, deleted BOOLEAN, image_keys TEXT[])
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            BEGIN
                IF NOT public.authorize('products.delete') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                RETURN QUERY
                WITH candidates AS (
                    SELECT p.id, p.name,
                           0::bigint AS reference_count,
                           COALESCE((
                               SELECT array_agg(i.object_key) FROM product_images i
                               WHERE i.product_id = p.id
                                 AND NOT EXISTS (
                                     SELECT 1 FROM product_images i2
                                     WHERE i2.object_key = i.object_key AND i2.product_id <> p.id)
                           ), ARRAY[]::text[]) AS image_keys
                    FROM products p
                    WHERE p.id = ANY(product_ids)
                ),
                removed AS (
                    DELETE FROM products
                    WHERE products.id IN (SELECT c.id FROM candidates c WHERE c.reference_count = 0)
                    RETURNING products.id
                )
                SELECT c.id, c.name, c.reference_count,
                       EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id),
                       CASE WHEN EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id)
                            THEN c.image_keys
                            ELSE ARRAY[]::text[]
                       END
                FROM candidates c;
            END;
            $$;

            REVOKE ALL ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT) FROM PUBLIC, anon;
            REVOKE ALL ON FUNCTION public.get_admin_product_filters() FROM PUBLIC, anon;
            REVOKE ALL ON FUNCTION public.delete_products(UUID[])     FROM PUBLIC, anon;
            GRANT EXECUTE ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT) TO authenticated;
            GRANT EXECUTE ON FUNCTION public.get_admin_product_filters() TO authenticated;
            GRANT EXECUTE ON FUNCTION public.delete_products(UUID[])     TO authenticated;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: AdminProductsPage_WithElevenProducts_ReturnsFirstTenNameAscendingWithTheFullTotal
// AC-3: AdminProductsPage_SearchWithSurroundingSpacesAndMixedCase_MatchesContainingNames,
//        AdminProductsPage_WithBlankSearch_AppliesNoNarrowing
// AC-4: AdminProductsPage_WithStatusAndBrandAndCategoryFilters_ReturnsOnlyProductsSatisfyingAll,
//        AdminProductFilters_ReturnsOnlyBrandsAndCategoriesThatOwnAProduct
// AC-5, AC-28: AdminProductsPage_SortedNameDescending_ReturnsReverseAlphabeticalOrder,
//        AdminProductsPage_SortedOldestFirst_ReturnsCreationOrderAscending,
//        AdminProductsPage_SortedByLowestVariantPriceUnderAFilter_OrdersOnTheUnfilteredLowestPrice,
//        AdminProductsPage_WithAnUnrecognizedSort_FallsBackToNameAscending
// AC-13: DeleteProducts_WhenTheProductHasNoReferences_DeletesItAndReportsDeletedTrue,
//        DeleteProducts_ReturnsTheDeletedProductsImageKeysForDisposal
// AC-16 (multiple in one call): DeleteProducts_ForMultipleIds_DeletesEachOneInTheSameCall
// AC-18: AdminProductsPage_WhenCallerLacksProductsViewPermission_ThrowsAccessDeniedException,
//        AdminProductFilters_WhenCallerLacksProductsViewPermission_ThrowsAccessDeniedException,
//        DeleteProducts_WhenCallerLacksProductsDeletePermission_ThrowsAccessDeniedException,
//        DeleteProductRow_WhenCallerLacksProductsDeletePermission_TheDeleteAffectsNoRows
// AC-24: AdminProductsPage_WhenAVariantHasASalePrice_UsesTheSalePriceForTheRange
// AC-25 (zero-variant collapses to one amount): AdminProductsPage_WithNoVariants_UsesItsOwnEffectivePriceForBothEnds
// AC-26: AdminProductsPage_WithPriceFilterBetweenTwoVariantPrices_ExcludesTheProduct,
//        AdminProductsPage_WithPriceFilterMatchingAVariantPriceExactly_IncludesTheProductInclusively
// AC-27: AdminProductsPage_WithPriceFilterMatchingAMiddleVariant_KeepsTheFullRangeAndCount
// AC-29: AdminProductsPage_ADeletedVariantContributesNeitherPriceNorCount
// (RULE-8 image disposal set, plan §5 Decision 7 price bounds): AdminProductFilters_PriceBoundsSpanVariantAndZeroVariantProductPrices
// (RULE-8 storage backstop): DeleteProductRow_WhenCallerHoldsProductsDeletePermission_Succeeds
// (idx_products_created_at, serves Newest/Oldest): Schema_IdxProductsCreatedAt_Exists
