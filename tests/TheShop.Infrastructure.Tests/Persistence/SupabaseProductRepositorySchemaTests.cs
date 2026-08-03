using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the <c>categories</c> / <c>brands</c> / <c>products</c> table schema
/// and Row-Level Security policies used by
/// <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseProductRepository"/>.
///
/// These tests spin up a real Postgres container (Testcontainers) and apply exactly the
/// stable, plan-sourced parts of the migration specified in the plan (§10 Database Schema
/// &amp; RLS Policies) to verify the storage-level contracts the Application layer depends on:
/// CHECK constraints, foreign keys, unique indexes, and the "only published products are
/// publicly visible" RLS policy (spec constraint: "The catalogue shows only products meant to
/// be publicly visible", AC-1).
///
/// Why raw SQL and not the Supabase SDK:
///   The Supabase SDK wraps PostgREST, which requires the full Supabase stack (Auth,
///   PostgREST process, etc.). A plain Postgres container cannot run PostgREST, so we
///   exercise the SQL contracts directly via Npgsql. Record ↔ domain mapping (including
///   image URL resolution) is covered separately in <see cref="ProductMapperTests"/>.
///
/// Scope note: the plan's schema (§10) lists an `image_url` column, but the shipped schema
/// (migrations 0004/0005) replaced it with `image_path` — a Storage object key resolved to a
/// public URL by the mapper. That column is intentionally not exercised at the schema level
/// here since the plan and the shipped schema disagree on its shape; see the coverage summary.
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public sealed class SupabaseProductRepositorySchemaTests : IAsyncLifetime
{
    // =========================================================================
    // Container + lifecycle
    // =========================================================================

    private const string PublicRole = "storefront_role";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("shop_test")
        .WithUsername("shop")
        .WithPassword("shop")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await ApplySchemaAsync();
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    // =========================================================================
    // products — NOT NULL constraints
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenNameIsNull_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO products (name, original_price, category_id, brand_id)
                VALUES (NULL, 24.99, '{categoryId}', '{brandId}')
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23502"); // not_null_violation
    }

    // =========================================================================
    // products — CHECK constraints (plan §10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenOriginalPriceIsNegative_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, brandId, originalPrice: -1m);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23514"); // check_violation
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenSalePriceIsNegative_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, brandId, originalPrice: 24.99m, salePrice: -1m);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenSalePriceExceedsOriginalPrice_ThrowsPostgresException()
    {
        // Storage-level backstop for the ProductPricing domain invariant (AC-2).
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, brandId, originalPrice: 19.99m, salePrice: 24.99m);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenSalePriceEqualsOriginalPrice_Succeeds()
    {
        // Boundary: sale price equal to original is allowed by the CHECK constraint.
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, brandId, originalPrice: 24.99m, salePrice: 24.99m);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenStockQuantityIsNegative_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, brandId, stockQuantity: -1);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenNicotineStrengthIsNegative_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, brandId, nicotineStrengthMg: -1);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23514");
    }

    // =========================================================================
    // products — effective_price generated column (migration 0006)
    // The storefront filters/sorts on the price the customer pays: sale price
    // when discounted, else original. effective_price = COALESCE(sale, original).
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task EffectivePrice_WhenProductHasNoSalePrice_EqualsOriginalPrice()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(conn, categoryId, brandId, originalPrice: 24.99m, salePrice: null);

        var effective = await ScalarAsync(conn, $"SELECT effective_price FROM products WHERE id = '{id}'");

        ((decimal)effective!).Should().Be(24.99m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task EffectivePrice_WhenProductIsDiscounted_EqualsSalePrice()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(conn, categoryId, brandId, originalPrice: 24.99m, salePrice: 19.99m);

        var effective = await ScalarAsync(conn, $"SELECT effective_price FROM products WHERE id = '{id}'");

        ((decimal)effective!).Should().Be(19.99m);
    }

    // =========================================================================
    // products — foreign keys
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenCategoryIdDoesNotExist_ThrowsForeignKeyViolation()
    {
        await using var conn = await OpenAsync();
        var (_, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, Guid.NewGuid(), brandId);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23503"); // foreign_key_violation
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WhenBrandIdDoesNotExist_ThrowsForeignKeyViolation()
    {
        await using var conn = await OpenAsync();
        var (categoryId, _) = await SeedCategoryAndBrandAsync(conn);

        var act = () => InsertProductAsync(conn, categoryId, Guid.NewGuid());

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23503");
    }

    // =========================================================================
    // products — defaults
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WithoutExplicitIsPublished_DefaultsToTrue()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(conn, categoryId, brandId, isPublished: null);

        var isPublished = await ScalarAsync(conn, $"SELECT is_published FROM products WHERE id = '{id}'");
        ((bool)isPublished!).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WithoutExplicitStockQuantity_DefaultsToZero()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(conn, categoryId, brandId, stockQuantity: null);

        var stock = await ScalarAsync(conn, $"SELECT stock_quantity FROM products WHERE id = '{id}'");
        ((int)stock!).Should().Be(0);
    }

    // =========================================================================
    // Happy-path round-trip: insert and select back (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task InsertProduct_WithValidData_RowCanBeSelectedBack()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(
            conn, categoryId, brandId, name: "Elf Bar BC5000",
            originalPrice: 24.99m, salePrice: 19.99m, stockQuantity: 40, nicotineStrengthMg: 50);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT name, original_price, sale_price, stock_quantity, nicotine_strength_mg
            FROM products WHERE id = '{id}'
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        var read = await reader.ReadAsync();

        read.Should().BeTrue("the row must exist after a successful insert");
        reader.GetString(0).Should().Be("Elf Bar BC5000");
        reader.GetDecimal(1).Should().Be(24.99m);
        reader.GetDecimal(2).Should().Be(19.99m);
        reader.GetInt32(3).Should().Be(40);
        reader.GetInt32(4).Should().Be(50);
    }

    // =========================================================================
    // RLS: only published products are visible to the public storefront role (AC-1)
    // Plan §10: CREATE POLICY "products_public_read" ON products FOR SELECT USING (is_published = true)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task SelectAsStorefrontRole_WhenProductIsPublished_IsVisible()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(conn, categoryId, brandId, isPublished: true);

        await using var roleConn = await OpenAsStorefrontRoleAsync();
        var count = await ScalarAsync(roleConn, $"SELECT COUNT(*) FROM products WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(1, "a published product must be visible to the public storefront role");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task SelectAsStorefrontRole_WhenProductIsUnpublished_IsHidden()
    {
        // Spec constraint: "The catalogue shows only products meant to be publicly visible
        // (no hidden, draft, or unpublished products)."
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        var id = await InsertProductAsync(conn, categoryId, brandId, isPublished: false);

        await using var roleConn = await OpenAsStorefrontRoleAsync();
        var count = await ScalarAsync(roleConn, $"SELECT COUNT(*) FROM products WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(0, "an unpublished product must not be visible to the public storefront role");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task SelectAsStorefrontRole_CategoriesAndBrandsAreAlwaysVisible()
    {
        // Plan §10: categories/brands are public reference data (USING (true)).
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn, "Disposables");
        await InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        await using var roleConn = await OpenAsStorefrontRoleAsync();
        var categoryCount = await ScalarAsync(roleConn, "SELECT COUNT(*) FROM categories");
        var brandCount = await ScalarAsync(roleConn, "SELECT COUNT(*) FROM brands");

        Convert.ToInt32(categoryCount).Should().BeGreaterThan(0);
        Convert.ToInt32(brandCount).Should().BeGreaterThan(0);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    // Simulates an anon/authenticated storefront read: a non-owner, non-superuser role subject
    // to RLS, obtained via SET ROLE on a fresh connection (the owning "shop" login can always
    // switch role, but RLS is evaluated against the switched-to role, not the login role).
    private async Task<NpgsqlConnection> OpenAsStorefrontRoleAsync()
    {
        var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET ROLE {PublicRole}";
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task InsertCategoryAsync(NpgsqlConnection conn, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO categories (name) VALUES ('{name}')";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertBrandAsync(NpgsqlConnection conn, string name, string slug)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO brands (name, slug) VALUES ('{name}', '{slug}')";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<(Guid CategoryId, Guid BrandId)> SeedCategoryAndBrandAsync(NpgsqlConnection conn)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var categoryCmd = conn.CreateCommand();
        categoryCmd.CommandText = $"""
            INSERT INTO categories (name) VALUES ('Disposables {suffix}')
            RETURNING id
            """;
        var categoryId = (Guid)(await categoryCmd.ExecuteScalarAsync())!;

        await using var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = $"""
            INSERT INTO brands (name, slug) VALUES ('Elf Bar {suffix}', 'elf-bar-{suffix}')
            RETURNING id
            """;
        var brandId = (Guid)(await brandCmd.ExecuteScalarAsync())!;

        return (categoryId, brandId);
    }

    private static async Task<Guid> InsertProductAsync(
        NpgsqlConnection conn,
        Guid categoryId,
        Guid brandId,
        string name = "Elf Bar BC5000",
        decimal originalPrice = 24.99m,
        decimal? salePrice = null,
        int? stockQuantity = 10,
        int? nicotineStrengthMg = null,
        bool? isPublished = null)
    {
        var columns = new List<string> { "name", "original_price", "category_id", "brand_id" };
        var values = new List<string> { $"'{name}'", originalPrice.ToString(System.Globalization.CultureInfo.InvariantCulture), $"'{categoryId}'", $"'{brandId}'" };

        if (salePrice is not null)
        {
            columns.Add("sale_price");
            values.Add(salePrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (stockQuantity is not null)
        {
            columns.Add("stock_quantity");
            values.Add(stockQuantity.Value.ToString());
        }

        if (nicotineStrengthMg is not null)
        {
            columns.Add("nicotine_strength_mg");
            values.Add(nicotineStrengthMg.Value.ToString());
        }

        if (isPublished is not null)
        {
            columns.Add("is_published");
            values.Add(isPublished.Value ? "true" : "false");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO products ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    // =========================================================================
    // Schema setup — mirrors the stable, plan-sourced parts of migration
    // 0002_create_product_catalogue.sql (plan §10). The `image_url`/`image_path` column is
    // intentionally omitted (see class remarks).
    // =========================================================================

    private async Task ApplySchemaAsync()
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS categories (
                id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name    TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS brands (
                id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name    TEXT NOT NULL,
                slug    TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS products (
                id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name                    TEXT NOT NULL,
                description             TEXT NOT NULL DEFAULT '',
                original_price          NUMERIC(10,2) NOT NULL CHECK (original_price >= 0),
                sale_price              NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price >= 0),
                currency                TEXT NOT NULL DEFAULT 'CAD',
                category_id             UUID NOT NULL REFERENCES categories(id),
                brand_id                UUID NOT NULL REFERENCES brands(id),
                flavour                 TEXT,
                nicotine_strength_mg    INTEGER CHECK (nicotine_strength_mg IS NULL OR nicotine_strength_mg >= 0),
                stock_quantity          INTEGER NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
                is_published            BOOLEAN NOT NULL DEFAULT TRUE,
                created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
                effective_price         NUMERIC(10,2) GENERATED ALWAYS AS (COALESCE(sale_price, original_price)) STORED,
                CONSTRAINT sale_not_above_original CHECK (sale_price IS NULL OR sale_price <= original_price)
            );

            ALTER TABLE categories ENABLE ROW LEVEL SECURITY;
            ALTER TABLE brands     ENABLE ROW LEVEL SECURITY;
            ALTER TABLE products   ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "categories_public_read" ON categories FOR SELECT USING (true);
            CREATE POLICY "brands_public_read"     ON brands     FOR SELECT USING (true);
            CREATE POLICY "products_public_read"   ON products   FOR SELECT USING (is_published = true);

            CREATE ROLE {PublicRole} NOLOGIN;
            GRANT USAGE ON SCHEMA public TO {PublicRole};
            GRANT SELECT ON categories, brands, products TO {PublicRole};
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: InsertProduct_WithValidData_RowCanBeSelectedBack,
//        SelectAsStorefrontRole_WhenProductIsPublished_IsVisible,
//        SelectAsStorefrontRole_WhenProductIsUnpublished_IsHidden
//        (storage-level backstop for "the catalogue shows only publicly visible products")
// AC-2: InsertProduct_WhenSalePriceExceedsOriginalPrice_ThrowsPostgresException,
//        InsertProduct_WhenSalePriceEqualsOriginalPrice_Succeeds
//        (storage-level backstop for the ProductPricing domain invariant)
