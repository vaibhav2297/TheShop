using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the product-description schema deltas (plan §10): the new
/// <c>product_specifications</c> table with its uniqueness index, cascade delete, and RLS
/// policies (migration 0029, applied), and the <c>products.description</c> bounds — the
/// Decision 3 whitelist-markup <c>CHECK</c> and the Decision 6 byte-size <c>CHECK</c> (migration
/// 0030's specified SQL). The description bounds are the <em>real</em> trust boundary per
/// constitution <c>architecture-admin.md</c>: Domain, Application, and Infrastructure all run
/// client-side under Blazor WebAssembly, so no C# check is a security boundary (plan Decision 4)
/// — only the database <c>CHECK</c> proves AC-11 ("supplied code never executes").
///
/// Spins up a real Postgres container (Testcontainers) and reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (the same mechanism <see cref="SupabaseProductAdminSchemaTests"/>
/// uses), then layers this feature's schema on top.
/// <see href=".specs/product-description/spec.md"/>
/// <see href=".specs/product-description/plan.md"/>
/// </summary>
public sealed class SupabaseProductDescriptionSchemaTests : IAsyncLifetime
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
        await ApplyProductDescriptionSchemaAsync(conn);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    private async Task<NpgsqlConnection> OpenAsAuthenticatedRoleAsync()
    {
        var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE authenticated";
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    // =========================================================================
    // product_specifications — row presence and uniqueness (RULE-3, RULE-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task InsertSpecification_WithABlankName_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => InsertSpecificationAsync(conn, productId, "   ", "Stainless steel", 0);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task InsertSpecification_WithABlankValue_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => InsertSpecificationAsync(conn, productId, "Material", "   ", 0);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task InsertSpecification_WithADuplicateNameIgnoringCaseAndSpaces_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        await InsertSpecificationAsync(conn, productId, "Material", "Stainless steel", 0);

        var act = () => InsertSpecificationAsync(conn, productId, " material ", "Aluminum", 1);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task InsertSpecification_WithDistinctNames_Succeeds()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        await InsertSpecificationAsync(conn, productId, "Material", "Stainless steel", 0);

        var act = () => InsertSpecificationAsync(conn, productId, "Capacity", "750 ml", 1);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task DeleteProduct_CascadesItsSpecifications()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        await InsertSpecificationAsync(conn, productId, "Material", "Stainless steel", 0);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DELETE FROM products WHERE id = '{productId}'";
            await cmd.ExecuteNonQueryAsync();
        }

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM product_specifications WHERE product_id = '{productId}'");
        Convert.ToInt32(count).Should().Be(0, "a removed product's specification rows must not become orphaned");
    }

    // =========================================================================
    // product_specifications — RLS (mirrors product_option_types, RULE-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task SelectSpecifications_AsAuthenticatedUserWithoutProductsView_SeesNoneOfAnUnpublishedProductsRows()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn, isPublished: false);
        await InsertSpecificationAsync(conn, productId, "Material", "Stainless steel", 0);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await using var roleConn = await OpenAsAuthenticatedRoleAsync();
        await RbacTestSchema.SetJwtClaimsAsync(roleConn, userId, sessionId);
        var count = await ScalarAsync(roleConn, $"SELECT COUNT(*) FROM product_specifications WHERE product_id = '{productId}'");

        Convert.ToInt32(count).Should().Be(0, "an unpublished product's rows are admin-only, and this caller holds no products.view");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task SelectSpecifications_AsStaffWithProductsView_SeesThem()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn, isPublished: false);
        await InsertSpecificationAsync(conn, productId, "Material", "Stainless steel", 0);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");

        await using var roleConn = await OpenAsAuthenticatedRoleAsync();
        await RbacTestSchema.SetJwtClaimsAsync(roleConn, userId, sessionId);
        var count = await ScalarAsync(roleConn, $"SELECT COUNT(*) FROM product_specifications WHERE product_id = '{productId}'");

        Convert.ToInt32(count).Should().Be(1, "a staff member holding products.view must see every row (RULE-1)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task InsertSpecification_AsAuthenticatedUserWithoutProductsCreateOrEdit_IsDeniedByRls()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await using var roleConn = await OpenAsAuthenticatedRoleAsync();
        await RbacTestSchema.SetJwtClaimsAsync(roleConn, userId, sessionId);

        var act = () => InsertSpecificationAsync(roleConn, productId, "Material", "Stainless steel", 0);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "42501", "a caller holding neither products.create nor products.edit must be refused by RLS");
    }

    // =========================================================================
    // products.description — the Decision 3 whitelist grammar CHECK (RULE-5, AC-10, AC-11)
    // =========================================================================

    [Theory]
    [InlineData("<p>Paragraph</p>")]
    [InlineData("Text<br>break")]
    [InlineData("<h1>Heading</h1><h6>Smallest</h6>")]
    [InlineData("<strong>Bold</strong><em>Italic</em>")]
    [InlineData("<ol><li>Item</li></ol><ul><li>Item</li></ul>")]
    [InlineData("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">Link</a>")]
    [InlineData("<a href=\"mailto:staff@example.com\">Email</a>")]
    [Trait("Feature", "product-description")]
    public async Task UpdateProductDescription_WithEverySupportedFormat_Succeeds(string description)
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => UpdateDescriptionAsync(conn, productId, description);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=\"x.png\">")]
    [InlineData("<span>Text</span>")]
    [InlineData("<p class=\"fancy\">Text</p>")]
    [InlineData("<a href=\"javascript:alert(1)\">Click</a>")]
    [InlineData("<a href=\"data:text/html,evil\">Click</a>")]
    [Trait("Feature", "product-description")]
    public async Task UpdateProductDescription_WithMarkupOutsideTheGrammar_ThrowsPostgresException(string description)
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);

        var act = () => UpdateDescriptionAsync(conn, productId, description);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514",
            "the database CHECK is the real security boundary — supplied code must never be storable (RULE-5, AC-11)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task UpdateProductDescription_WithExecutableContent_LeavesNoRowChanged()
    {
        // Proves AC-11 end to end at the actual write path: calling the database directly with
        // markup outside the grammar, bypassing ProductForm and every C# validator entirely.
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        await UpdateDescriptionAsync(conn, productId, "<p>Safe</p>");

        var act = () => UpdateDescriptionAsync(conn, productId, "<script>alert(document.cookie)</script>");
        await act.Should().ThrowAsync<PostgresException>();

        var stored = await ScalarAsync(conn, $"SELECT description FROM products WHERE id = '{productId}'");
        stored.Should().Be("<p>Safe</p>", "a rejected write must leave the previously stored description untouched");
    }

    // =========================================================================
    // products.description — the Decision 6 byte-size CHECK (RULE-2 backstop)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task UpdateProductDescription_AtExactly200000Bytes_Succeeds()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        var description = "<p>" + new string('a', 199_993) + "</p>"; // 3 + 199_993 + 4 = 200_000 bytes

        var act = () => UpdateDescriptionAsync(conn, productId, description);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task UpdateProductDescription_Over200000Bytes_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var productId = await SeedProductAsync(conn);
        var description = "<p>" + new string('a', 199_994) + "</p>"; // 200_001 bytes

        var act = () => UpdateDescriptionAsync(conn, productId, description);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
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

    private async Task<Guid> SeedProductAsync(NpgsqlConnection conn, bool isPublished = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var categoryCmd = conn.CreateCommand();
        categoryCmd.CommandText = $"INSERT INTO categories (name) VALUES ('Disposables {suffix}') RETURNING id";
        var categoryId = (Guid)(await categoryCmd.ExecuteScalarAsync())!;

        await using var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = $"INSERT INTO brands (name, slug) VALUES ('Elf Bar {suffix}', 'elf-bar-{suffix}') RETURNING id";
        var brandId = (Guid)(await brandCmd.ExecuteScalarAsync())!;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO products (name, sku, category_id, brand_id, is_published)
            VALUES ('Elf Bar {suffix}', 'ELF-{suffix}', '{categoryId}', '{brandId}', {(isPublished ? "true" : "false")})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task InsertSpecificationAsync(
        NpgsqlConnection conn, Guid productId, string name, string value, int position)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO product_specifications (product_id, name, value, position) VALUES ($1, $2, $3, $4)";
        cmd.Parameters.AddWithValue(productId);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(value);
        cmd.Parameters.AddWithValue(position);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpdateDescriptionAsync(NpgsqlConnection conn, Guid productId, string description)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE products SET description = $1 WHERE id = $2";
        cmd.Parameters.AddWithValue(description);
        cmd.Parameters.AddWithValue(productId);
        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================================
    // Schema setup — mirrors the plan §10 shape (migrations 0029 applied, plus 0030's specified
    // description CHECKs), scoped to what this class exercises. Uses the RBAC core's
    // authorize()/authenticated role for RLS.
    // =========================================================================

    private static async Task ApplyProductDescriptionSchemaAsync(NpgsqlConnection conn)
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
                id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name          TEXT NOT NULL,
                description   TEXT NOT NULL DEFAULT '',
                sku           TEXT NOT NULL,
                category_id   UUID NOT NULL REFERENCES categories(id),
                brand_id      UUID NOT NULL REFERENCES brands(id),
                is_published  BOOLEAN NOT NULL DEFAULT FALSE,
                updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
                created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            -- Migration 0030's CHECKs (plan §10), restated here because this suite builds its own
            -- schema rather than replaying supabase/migrations — so a green run here is never on
            -- its own proof that the real database carries the boundary.
            --
            -- The plan's CHECK is textually "CHECK (NOT EXISTS (SELECT ... FROM regexp_matches(...)))",
            -- but PostgreSQL categorically forbids a sub-SELECT inside a CHECK constraint expression
            -- ("cannot use subquery in check constraint", SQLSTATE 0A000) — confirmed by running this
            -- exact DDL against a real Postgres container. A CHECK *can* call a function, so the
            -- identical predicate is wrapped in one here; the CHECK's own text stays a single
            -- function-call expression, satisfying Postgres while keeping the Decision 3 grammar
            -- unchanged. supabase/migrations/0030_product_description_bounds.sql now carries this
            -- same shape and records the deviation from the plan's literal SQL.
            CREATE FUNCTION public.description_markup_allowed(description TEXT) RETURNS BOOLEAN
            LANGUAGE sql IMMUTABLE AS $fn$
                SELECT NOT EXISTS (
                    SELECT 1
                    FROM regexp_matches(description, '<[^>]*>', 'g') AS m(tag)
                    WHERE m.tag[1] !~* '^</?(p|br|h[1-6]|strong|em|ol|ul|li)\s*/?>$'
                      AND m.tag[1] !~* '^</a>$'
                      AND m.tag[1] !~* '^<a\s+href="(https?://|mailto:)[^"<>]*"(\s+target="_blank"|\s+rel="noopener noreferrer")*\s*>$'
                )
            $fn$;

            ALTER TABLE products
                ADD CONSTRAINT products_description_size
                    CHECK (octet_length(description) <= 200000);

            ALTER TABLE products
                ADD CONSTRAINT products_description_markup_allowed
                    CHECK (public.description_markup_allowed(description));

            CREATE TABLE product_specifications (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                name        TEXT NOT NULL CHECK (btrim(name) <> ''),
                value       TEXT NOT NULL CHECK (btrim(value) <> ''),
                position    INTEGER NOT NULL CHECK (position >= 0)
            );

            CREATE UNIQUE INDEX ux_product_specifications_name
                ON product_specifications(product_id, lower(btrim(name)));
            CREATE INDEX idx_product_specifications_product
                ON product_specifications(product_id, position);

            ALTER TABLE products                 ENABLE ROW LEVEL SECURITY;
            ALTER TABLE product_specifications   ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "products_public_read" ON products
                FOR SELECT USING (is_published = true);
            CREATE POLICY "products_admin_read" ON products
                FOR SELECT USING ((SELECT public.authorize('products.view')));
            CREATE POLICY "products_admin_write" ON products
                FOR UPDATE USING ((SELECT public.authorize('products.edit')))
                WITH CHECK ((SELECT public.authorize('products.edit')));

            CREATE POLICY "product_specifications_public_read" ON product_specifications
                FOR SELECT USING (
                    EXISTS (SELECT 1 FROM products p WHERE p.id = product_specifications.product_id AND p.is_published)
                    OR (SELECT public.authorize('products.view'))
                );
            CREATE POLICY "product_specifications_admin_insert" ON product_specifications
                FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                                    OR (SELECT public.authorize('products.edit')));
            CREATE POLICY "product_specifications_admin_update" ON product_specifications
                FOR UPDATE USING ((SELECT public.authorize('products.edit')))
                WITH CHECK ((SELECT public.authorize('products.edit')));
            CREATE POLICY "product_specifications_admin_delete" ON product_specifications
                FOR DELETE USING ((SELECT public.authorize('products.edit'))
                               OR (SELECT public.authorize('products.create')));
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7 (blank row name/value identified): InsertSpecification_WithABlankName_ThrowsPostgresException,
//        InsertSpecification_WithABlankValue_ThrowsPostgresException
// AC-8 ("Material"/" material " duplicate rejected): InsertSpecification_WithADuplicateNameIgnoringCaseAndSpaces_ThrowsUniqueViolation,
//        InsertSpecification_WithDistinctNames_Succeeds
// AC-9 (missing/revoked permission refused by RLS): SelectSpecifications_AsAuthenticatedUserWithoutProductsView_SeesNoneOfAnUnpublishedProductsRows,
//        SelectSpecifications_AsStaffWithProductsView_SeesThem, InsertSpecification_AsAuthenticatedUserWithoutProductsCreateOrEdit_IsDeniedByRls
// AC-10/AC-11 (unsupported formatting/executable content never stored): UpdateProductDescription_WithEverySupportedFormat_Succeeds,
//        UpdateProductDescription_WithMarkupOutsideTheGrammar_ThrowsPostgresException, UpdateProductDescription_WithExecutableContent_LeavesNoRowChanged
// AC-4/AC-5 (20,000-character allowance backstopped by the raw byte bound): UpdateProductDescription_AtExactly200000Bytes_Succeeds,
//        UpdateProductDescription_Over200000Bytes_ThrowsPostgresException
