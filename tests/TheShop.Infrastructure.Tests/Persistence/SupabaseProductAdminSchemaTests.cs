using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the create-product schema deltas (plan §10, migrations 0022-0024): the
/// new <c>product_images</c> / <c>product_option_types</c> / <c>product_option_values</c> /
/// <c>product_variants</c> / <c>product_variant_option_values</c> / <c>product_sku_registry</c>
/// tables, the store-wide SKU namespace (RULE-8), the gallery's exactly-one-primary index
/// (RULE-7), a removed pinned image unpinning its variants via <c>ON DELETE SET NULL</c>
/// (RULE-13), the variant price/stock CHECK constraints (RULE-4, RULE-5, RULE-6), and RLS
/// denial for a caller without the relevant <c>products.*</c> permission (RULE-20, plan §10).
///
/// Spins up a real Postgres container (Testcontainers) and reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (the same mechanism <see cref="SupabaseCategoryRepositorySchemaTests"/>
/// uses), then layers the plan §10 product/variant schema on top. This class covers only the
/// pieces create-product adds or changes; <see cref="SupabaseProductRepositorySchemaTests"/>
/// covers the product-catalogue feature's original schema.
/// <see href=".specs/create-product/spec.md"/>
/// <see href=".specs/create-product/plan.md"/>
/// </summary>
public sealed class SupabaseProductAdminSchemaTests : IAsyncLifetime
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
        await ApplyProductAdminSchemaAsync(conn);
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
    // Gallery — exactly one primary image whenever any image exists (RULE-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProductImage_WithASecondPrimaryForTheSameProduct_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        await InsertImageAsync(conn, productId, "products/a.png", isPrimary: true);

        var act = () => InsertImageAsync(conn, productId, "products/b.png", isPrimary: true);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProductImage_WithOnlyOnePrimary_Succeeds()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        await InsertImageAsync(conn, productId, "products/a.png", isPrimary: true);

        var act = () => InsertImageAsync(conn, productId, "products/b.png", isPrimary: false);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProductImage_ADifferentProductsPrimary_DoesNotCollide()
    {
        // The unique index is scoped per product_id, so two different products may each have
        // their own primary image.
        await using var conn = await OpenAsync();
        var firstProductId = await SeedProductAsync(conn);
        var secondProductId = await SeedProductAsync(conn);
        await InsertImageAsync(conn, firstProductId, "products/a.png", isPrimary: true);

        var act = () => InsertImageAsync(conn, secondProductId, "products/b.png", isPrimary: true);

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // Removing a pinned image unpins its variant (RULE-13, ON DELETE SET NULL)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task DeleteProductImage_WhenAVariantIsPinnedToIt_SetsTheVariantsImageIdToNull()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        var imageId = await InsertImageAsync(conn, productId, "products/a.png", isPrimary: true);
        var variantId = await InsertVariantAsync(conn, productId, sku: "SKU-MANGO", imageId: imageId);

        await DeleteImageAsync(conn, imageId);

        var pinned = await ScalarAsync(conn, $"SELECT image_id FROM product_variants WHERE id = '{variantId}'");
        pinned.Should().Be(DBNull.Value, "removing the pinned image must leave the variant unpinned, never blocked");
    }

    // =========================================================================
    // Store-wide SKU namespace — one registry spanning products and variants (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertSkuRegistryRow_WithASkuAlreadyUsedByAnotherProduct_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        var firstProductId = await SeedProductAsync(conn, sku: "ELF-BC5000");
        var secondProductId = await SeedProductAsync(conn, sku: "ELF-BC6000");
        await RegisterSkuAsync(conn, "elf-bc5000", firstProductId);

        var act = () => RegisterSkuAsync(conn, "elf-bc5000", secondProductId);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertSkuRegistryRow_WithAVariantSkuMatchingAnotherProductsSku_ThrowsUniqueViolation()
    {
        // The namespace spans both tables (Decision 6): a variant SKU cannot collide with any
        // other product's or variant's SKU either.
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn, sku: "ELF-BC5000");
        var otherProductId = await SeedProductAsync(conn, sku: "ELF-BC6000");
        await RegisterSkuAsync(conn, "elf-bc5000", productId);
        var variantId = await InsertVariantAsync(conn, otherProductId, sku: "ELF-BC5000");

        var act = () => RegisterSkuAsync(conn, "elf-bc5000", otherProductId, variantId);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertSkuRegistryRow_DeletingTheOwningProduct_CascadesTheRegistryRow()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn, sku: "ELF-BC5000");
        await RegisterSkuAsync(conn, "elf-bc5000", productId);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DELETE FROM products WHERE id = '{productId}'";
            await cmd.ExecuteNonQueryAsync();
        }

        var count = await ScalarAsync(conn, "SELECT COUNT(*) FROM product_sku_registry WHERE sku_normalized = 'elf-bc5000'");
        Convert.ToInt32(count).Should().Be(0, "the registry is self-cleaning via ON DELETE CASCADE");
    }

    // =========================================================================
    // Variant CHECK constraints (RULE-5, RULE-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertVariant_WithNegativeStock_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => InsertVariantAsync(conn, productId, sku: "SKU-MANGO", stockQuantity: -1);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertVariant_WithSalePriceEqualToOriginalPrice_ThrowsPostgresException()
    {
        // RULE-5 is strict: equal is not lower.
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => InsertVariantAsync(conn, productId, sku: "SKU-MANGO", originalPrice: 24.99m, salePrice: 24.99m);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertVariant_WithNoPriceOrStockYet_Succeeds()
    {
        // RULE-15: an Unpublished draft's variant may be saved incomplete.
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => InsertVariantAsync(conn, productId, sku: "SKU-MANGO");

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // products — sku required at every save (RULE-8, RULE-15), draft-tolerant pricing
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProduct_WhenSkuIsNull_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO products (name, sku, category_id, brand_id)
                VALUES ('Elf Bar BC5000', NULL, '{categoryId}', '{brandId}')
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23502");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProduct_WithNoPriceAtAll_Succeeds()
    {
        // RULE-15: original_price is nullable so a draft may be saved with nothing priced yet.
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO products (name, sku, category_id, brand_id)
                VALUES ('Elf Bar BC5000', 'ELF-BC5000', '{categoryId}', '{brandId}')
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProduct_WithADuplicateNormalizedName_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        var (categoryId, brandId) = await SeedCategoryAndBrandAsync(conn);
        await SeedProductAsync(conn, name: "Elf Bar BC5000", sku: "ELF-BC5000", categoryId: categoryId, brandId: brandId);

        var act = () => SeedProductAsync(conn, name: "  elf bar bc5000  ", sku: "ELF-BC6000", categoryId: categoryId, brandId: brandId);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    // =========================================================================
    // RLS — admin read/write require the relevant products.* permission (RULE-20)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task SelectProducts_AsAuthenticatedUserWithoutProductsView_SeesNoUnpublishedRows()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn, isPublished: false);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await using var roleConn = await OpenAsAuthenticatedRoleAsync();
        await RbacTestSchema.SetJwtClaimsAsync(roleConn, userId, sessionId);
        var count = await ScalarAsync(roleConn, $"SELECT COUNT(*) FROM products WHERE id = '{productId}' AND is_published = false");

        Convert.ToInt32(count).Should().Be(0, "an unpublished product is admin-only (RULE-17), and this caller holds no products.view");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task SelectProducts_AsStaffWithProductsView_SeesUnpublishedRows()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn, isPublished: false);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");

        await using var roleConn = await OpenAsAuthenticatedRoleAsync();
        await RbacTestSchema.SetJwtClaimsAsync(roleConn, userId, sessionId);
        var count = await ScalarAsync(roleConn, $"SELECT COUNT(*) FROM products WHERE id = '{productId}'");

        Convert.ToInt32(count).Should().Be(1, "a staff member holding products.view must see unpublished products (RULE-17)");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task InsertProductImage_AsAuthenticatedUserWithoutProductsCreateOrEdit_IsDeniedByRls()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await using var roleConn = await OpenAsAuthenticatedRoleAsync();
        await RbacTestSchema.SetJwtClaimsAsync(roleConn, userId, sessionId);

        var act = () => InsertImageAsync(roleConn, productId, "products/a.png", isPrimary: true);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "42501", "a caller holding neither products.create nor products.edit must be refused by RLS");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<NpgsqlConnection> OpenAsAuthenticatedRoleAsync()
    {
        var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE authenticated";
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<(Guid CategoryId, Guid BrandId)> SeedCategoryAndBrandAsync(NpgsqlConnection conn)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var categoryCmd = conn.CreateCommand();
        categoryCmd.CommandText = $"INSERT INTO categories (name) VALUES ('Disposables {suffix}') RETURNING id";
        var categoryId = (Guid)(await categoryCmd.ExecuteScalarAsync())!;

        await using var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = $"INSERT INTO brands (name, slug) VALUES ('Elf Bar {suffix}', 'elf-bar-{suffix}') RETURNING id";
        var brandId = (Guid)(await brandCmd.ExecuteScalarAsync())!;

        return (categoryId, brandId);
    }

    private async Task<Guid> SeedProductAsync(
        NpgsqlConnection conn,
        string? name = null,
        string? sku = null,
        bool isPublished = false,
        Guid? categoryId = null,
        Guid? brandId = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Guid resolvedCategoryId;
        Guid resolvedBrandId;
        if (categoryId is Guid c && brandId is Guid b)
        {
            resolvedCategoryId = c;
            resolvedBrandId = b;
        }
        else
        {
            (resolvedCategoryId, resolvedBrandId) = await SeedCategoryAndBrandAsync(conn);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO products (name, sku, category_id, brand_id, is_published)
            VALUES ('{(name ?? $"Elf Bar {suffix}").Replace("'", "''")}', '{sku ?? $"ELF-{suffix}"}',
                    '{resolvedCategoryId}', '{resolvedBrandId}', {(isPublished ? "true" : "false")})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertImageAsync(NpgsqlConnection conn, Guid productId, string objectKey, bool isPrimary)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO product_images (product_id, object_key, position, is_primary)
            VALUES ('{productId}', '{objectKey}', 0, {(isPrimary ? "true" : "false")})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task DeleteImageAsync(NpgsqlConnection conn, Guid imageId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM product_images WHERE id = '{imageId}'";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertVariantAsync(
        NpgsqlConnection conn,
        Guid productId,
        string sku,
        decimal? originalPrice = null,
        decimal? salePrice = null,
        int? stockQuantity = null,
        Guid? imageId = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO product_variants (product_id, sku, original_price, sale_price, stock_quantity, image_id, position)
            VALUES ('{productId}', '{sku}',
                    {(originalPrice is null ? "NULL" : originalPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))},
                    {(salePrice is null ? "NULL" : salePrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))},
                    {(stockQuantity is null ? "NULL" : stockQuantity.Value.ToString())},
                    {(imageId is null ? "NULL" : $"'{imageId}'")}, 0)
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task RegisterSkuAsync(NpgsqlConnection conn, string skuNormalized, Guid productId, Guid? variantId = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO product_sku_registry (sku_normalized, product_id, variant_id)
            VALUES ('{skuNormalized}', '{productId}', {(variantId is null ? "NULL" : $"'{variantId}'")})
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================================
    // Schema setup — mirrors the plan §10 shape (migrations 0022-0024), scoped to what this
    // class exercises. Uses the RBAC core's authorize()/authenticated role for RLS.
    // =========================================================================

    private static async Task ApplyProductAdminSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE categories (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name TEXT NOT NULL
            );

            CREATE TABLE brands (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name TEXT NOT NULL,
                slug TEXT NOT NULL UNIQUE
            );

            CREATE TABLE products (
                id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name                  TEXT NOT NULL,
                description           TEXT NOT NULL DEFAULT '',
                sku                   TEXT NOT NULL,
                original_price        NUMERIC(10,2) CHECK (original_price IS NULL OR original_price > 0),
                sale_price            NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price > 0),
                category_id           UUID NOT NULL REFERENCES categories(id),
                brand_id              UUID NOT NULL REFERENCES brands(id),
                is_published          BOOLEAN NOT NULL DEFAULT FALSE,
                min_variant_price     NUMERIC(10,2),
                has_sellable_variant  BOOLEAN NOT NULL DEFAULT FALSE,
                updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
                created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT product_sale_below_original CHECK (sale_price IS NULL OR original_price IS NULL OR sale_price < original_price)
            );
            CREATE UNIQUE INDEX ux_products_name_normalized ON products (lower(btrim(name)));

            CREATE TABLE product_images (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                object_key  TEXT NOT NULL,
                position    INTEGER NOT NULL CHECK (position >= 0),
                is_primary  BOOLEAN NOT NULL DEFAULT FALSE,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX ux_product_images_primary ON product_images(product_id) WHERE is_primary;
            CREATE INDEX idx_product_images_product ON product_images(product_id, position);

            CREATE TABLE product_option_types (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                name        TEXT NOT NULL CHECK (btrim(name) <> ''),
                position    INTEGER NOT NULL CHECK (position >= 0)
            );
            CREATE UNIQUE INDEX ux_product_option_types_name ON product_option_types(product_id, lower(btrim(name)));

            CREATE TABLE product_option_values (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                option_type_id  UUID NOT NULL REFERENCES product_option_types(id) ON DELETE CASCADE,
                value           TEXT NOT NULL CHECK (btrim(value) <> ''),
                position        INTEGER NOT NULL CHECK (position >= 0)
            );
            CREATE UNIQUE INDEX ux_product_option_values_value ON product_option_values(option_type_id, lower(btrim(value)));

            CREATE TABLE product_variants (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                product_id      UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                sku             TEXT NOT NULL CHECK (btrim(sku) <> ''),
                original_price  NUMERIC(10,2) CHECK (original_price IS NULL OR original_price > 0),
                sale_price      NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price > 0),
                stock_quantity  INTEGER CHECK (stock_quantity IS NULL OR stock_quantity >= 0),
                is_available    BOOLEAN NOT NULL DEFAULT TRUE,
                image_id        UUID REFERENCES product_images(id) ON DELETE SET NULL,
                position        INTEGER NOT NULL CHECK (position >= 0),
                CONSTRAINT variant_sale_below_original
                    CHECK (sale_price IS NULL OR original_price IS NULL OR sale_price < original_price)
            );
            CREATE INDEX idx_product_variants_product ON product_variants(product_id, position);

            CREATE TABLE product_variant_option_values (
                variant_id      UUID NOT NULL REFERENCES product_variants(id) ON DELETE CASCADE,
                option_value_id UUID NOT NULL REFERENCES product_option_values(id) ON DELETE CASCADE,
                PRIMARY KEY (variant_id, option_value_id)
            );

            CREATE TABLE product_sku_registry (
                sku_normalized  TEXT PRIMARY KEY,
                product_id      UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                variant_id      UUID REFERENCES product_variants(id) ON DELETE CASCADE
            );
            CREATE INDEX idx_product_sku_registry_product ON product_sku_registry(product_id);

            ALTER TABLE products                      ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_images                 ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_option_types           ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_option_values          ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_variants                ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_variant_option_values   ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_sku_registry            ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "products_public_read" ON products
                FOR SELECT USING (is_published = true);
            CREATE POLICY "products_admin_read" ON products
                FOR SELECT USING ((SELECT public.authorize('products.view')));
            CREATE POLICY "products_admin_insert" ON products
                FOR INSERT WITH CHECK ((SELECT public.authorize('products.create')));
            CREATE POLICY "products_admin_update" ON products
                FOR UPDATE USING ((SELECT public.authorize('products.edit')))
                WITH CHECK ((SELECT public.authorize('products.edit')));

            CREATE POLICY "product_images_public_read" ON product_images
                FOR SELECT USING (
                    EXISTS (SELECT 1 FROM products p WHERE p.id = product_id AND p.is_published)
                    OR (SELECT public.authorize('products.view')));
            CREATE POLICY "product_images_admin_insert" ON product_images
                FOR INSERT WITH CHECK ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')));
            CREATE POLICY "product_images_admin_update" ON product_images
                FOR UPDATE USING ((SELECT public.authorize('products.edit')))
                WITH CHECK ((SELECT public.authorize('products.edit')));
            CREATE POLICY "product_images_admin_delete" ON product_images
                FOR DELETE USING ((SELECT public.authorize('products.edit')) OR (SELECT public.authorize('products.create')));

            CREATE POLICY "product_variants_public_read" ON product_variants
                FOR SELECT USING (
                    EXISTS (SELECT 1 FROM products p WHERE p.id = product_id AND p.is_published)
                    OR (SELECT public.authorize('products.view')));
            CREATE POLICY "product_variants_admin_write" ON product_variants
                FOR ALL USING ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')))
                WITH CHECK ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')));

            CREATE POLICY "product_option_types_admin_all" ON product_option_types
                FOR ALL USING ((SELECT public.authorize('products.view')))
                WITH CHECK ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')));
            CREATE POLICY "product_option_values_admin_all" ON product_option_values
                FOR ALL USING ((SELECT public.authorize('products.view')))
                WITH CHECK ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')));
            CREATE POLICY "product_variant_option_values_admin_all" ON product_variant_option_values
                FOR ALL USING ((SELECT public.authorize('products.view')))
                WITH CHECK ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')));
            CREATE POLICY "product_sku_registry_admin_all" ON product_sku_registry
                FOR ALL USING ((SELECT public.authorize('products.view')))
                WITH CHECK ((SELECT public.authorize('products.create')) OR (SELECT public.authorize('products.edit')));
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7 (exactly one primary image): InsertProductImage_WithASecondPrimaryForTheSameProduct_ThrowsUniqueViolation,
//        InsertProductImage_WithOnlyOnePrimary_Succeeds, InsertProductImage_ADifferentProductsPrimary_DoesNotCollide
// AC-19 (a draft may be saved with no price at all): InsertProduct_WithNoPriceAtAll_Succeeds,
//        InsertVariant_WithNoPriceOrStockYet_Succeeds
// AC-22 (duplicate product name refused): InsertProduct_WithADuplicateNormalizedName_ThrowsUniqueViolation
// AC-25 (sale price must be strictly lower than price): InsertVariant_WithSalePriceEqualToOriginalPrice_ThrowsPostgresException
// AC-26 (stock must be a whole number ≥ 0): InsertVariant_WithNegativeStock_ThrowsPostgresException
// AC-27 (SKU is unique across the whole catalogue, product and variant alike): InsertSkuRegistryRow_WithASkuAlreadyUsedByAnotherProduct_ThrowsUniqueViolation,
//        InsertSkuRegistryRow_WithAVariantSkuMatchingAnotherProductsSku_ThrowsUniqueViolation, InsertSkuRegistryRow_DeletingTheOwningProduct_CascadesTheRegistryRow
// AC-30 (unpublished products stay admin-visible, RLS-gated): SelectProducts_AsAuthenticatedUserWithoutProductsView_SeesNoUnpublishedRows,
//        SelectProducts_AsStaffWithProductsView_SeesUnpublishedRows
// AC-33 (no products.view → denied, RLS as the real boundary): SelectProducts_AsAuthenticatedUserWithoutProductsView_SeesNoUnpublishedRows,
//        InsertProductImage_AsAuthenticatedUserWithoutProductsCreateOrEdit_IsDeniedByRls
// RULE-8 (every product/variant SKU is required, in one store-wide namespace): InsertProduct_WhenSkuIsNull_ThrowsPostgresException
// RULE-13 (removing a pinned image leaves the variant unpinned): DeleteProductImage_WhenAVariantIsPinnedToIt_SetsTheVariantsImageIdToNull
