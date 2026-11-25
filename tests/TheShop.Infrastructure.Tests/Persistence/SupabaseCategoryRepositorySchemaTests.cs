using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the manage-categories schema and RLS deltas on the <c>categories</c>
/// table plus the new <c>category-images</c> storage bucket policies, exercised by
/// <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseCategoryRepository"/> and
/// <see cref="TheShop.Infrastructure.Storage.SupabaseFileStorage"/>. Sourced from the plan's Data
/// Model (§4) and Database Schema &amp; RLS Policies (§10): the
/// <c>ux_categories_name_normalized</c> unique index (RULE-2 storage backstop, AC-11), the
/// unchanged <c>categories_public_read</c> policy that stays <c>USING (true)</c> for both Active
/// and Inactive categories (Decision 2 — restricting it would null out a product's embedded
/// category join and break the catalogue), the <c>categories_admin_insert</c> policy, and the
/// <c>category-images</c> bucket's insert/read policies.
///
/// Spins up a real Postgres container (Testcontainers), reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (auth stub, <c>authorize()</c>, roles/permissions seed — the same
/// mechanism <see cref="ManageCategoriesSchemaTests"/> uses), then layers the <c>categories</c>
/// table and <c>storage.objects</c> stub on top. Record ↔ domain mapping is covered separately in
/// <see cref="CategoryMapperTests"/>.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public sealed class SupabaseCategoryRepositorySchemaTests : IAsyncLifetime
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
        await ApplyCategoriesSchemaAsync(conn);
        await RbacTestSchema.CreateRlsTestRolesAsync(conn);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
        await RbacTestSchema.GrantStorageRlsRolePrivilegesAsync(conn);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    // =========================================================================
    // ux_categories_name_normalized — case/space-insensitive uniqueness (RULE-2, AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task InsertCategory_WhenNameAlreadyExistsExactMatch_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn, "Disposables");

        var act = () => InsertCategoryAsync(conn, "Disposables");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task InsertCategory_WhenNameDiffersOnlyByCapitalization_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn, "Disposables");

        var act = () => InsertCategoryAsync(conn, "DISPOSABLES");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task InsertCategory_WhenNameDiffersOnlyByLeadingOrTrailingSpaces_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn, "Disposables");

        var act = () => InsertCategoryAsync(conn, "  Disposables  ");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task InsertCategory_WhenNamesAreDifferent_BothRowsInserted()
    {
        await using var conn = await OpenAsync();

        var act = async () =>
        {
            await InsertCategoryAsync(conn, "Disposables");
            await InsertCategoryAsync(conn, "Pod Systems");
        };

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // is_active default + happy-path round-trip
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task InsertCategory_WithoutExplicitIsActive_DefaultsToTrue()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");

        var isActive = await ScalarAsync(conn, $"SELECT is_active FROM categories WHERE id = '{id}'");

        ((bool)isActive!).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task InsertCategory_WithValidData_RowCanBeSelectedBack()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(
            conn, "Disposables", description: "Single-use vape devices.", imagePath: "categories/abc/image.webp", isActive: false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT name, description, image_path, is_active FROM categories WHERE id = '{id}'
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        var read = await reader.ReadAsync();

        read.Should().BeTrue("the row must exist after a successful insert");
        reader.GetString(0).Should().Be("Disposables");
        reader.GetString(1).Should().Be("Single-use vape devices.");
        reader.GetString(2).Should().Be("categories/abc/image.webp");
        reader.GetBoolean(3).Should().BeFalse();
    }

    // =========================================================================
    // categories_public_read — USING (true) for both Active and Inactive categories (Decision 2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoriesPublicRead_WhenCategoryIsActiveAndCallerIsAnonymous_IsVisible()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables", isActive: true);

        await using var anonConn = await OpenAsAnonAsync();
        var count = await ScalarAsync(anonConn, $"SELECT COUNT(*) FROM categories WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoriesPublicRead_WhenCategoryIsInactiveAndCallerIsAnonymous_IsStillVisible()
    {
        // Decision 2: restricting SELECT to is_active would null out a published product's
        // embedded CategoryRecord and break the catalogue (ProductMapper.ToDomain throws on a
        // null embed). Inactive categories are hidden from customers by the
        // get_catalogue_filters() facet filter instead, not by RLS.
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables", isActive: false);

        await using var anonConn = await OpenAsAnonAsync();
        var count = await ScalarAsync(anonConn, $"SELECT COUNT(*) FROM categories WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(1,
            "an Inactive category row stays readable — RLS is never what hides it from customers (Decision 2)");
    }

    // =========================================================================
    // categories_admin_insert — authorize('categories.create') (AC-6 happy path, AC-9 boundary)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoriesAdminInsert_WhenCallerHoldsCategoriesCreatePermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = () => InsertCategoryAsync(conn, "Disposables");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoriesAdminInsert_WhenCallerLacksCategoriesCreatePermission_ThrowsRowLevelSecurityViolation()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = () => InsertCategoryAsync(conn, "Disposables");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // category-images storage bucket policies
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryImagesInsert_WhenCallerHoldsCategoriesCreatePermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('category-images')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryImagesInsert_WhenCallerLacksCategoriesCreatePermission_ThrowsRowLevelSecurityViolation()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('category-images')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryImagesRead_WhenCallerHoldsCategoriesViewPermission_CanSelectTheObject()
    {
        await using var conn = await OpenAsync();
        var objectId = await InsertCategoryImageObjectAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM storage.objects WHERE id = '{objectId}'");

        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryImagesRead_WhenCallerLacksCategoriesViewPermission_CannotSelectTheObject()
    {
        await using var conn = await OpenAsync();
        var objectId = await InsertCategoryImageObjectAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM storage.objects WHERE id = '{objectId}'");

        Convert.ToInt32(count).Should().Be(0);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<NpgsqlConnection> OpenAsAnonAsync()
    {
        var conn = await OpenAsync();
        await using (var clearCmd = conn.CreateCommand())
        {
            clearCmd.CommandText = "SET request.jwt.claims = ''";
            await clearCmd.ExecuteNonQueryAsync();
        }
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE anon";
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    private static async Task SetRoleAuthenticatedAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE authenticated";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<Guid> InsertCategoryAsync(
        NpgsqlConnection conn,
        string name,
        string? description = null,
        string? imagePath = null,
        bool? isActive = null)
    {
        var columns = new List<string> { "name" };
        var values = new List<string> { $"'{name.Replace("'", "''")}'" };

        if (description is not null)
        {
            columns.Add("description");
            values.Add($"'{description.Replace("'", "''")}'");
        }

        if (imagePath is not null)
        {
            columns.Add("image_path");
            values.Add($"'{imagePath}'");
        }

        if (isActive is not null)
        {
            columns.Add("is_active");
            values.Add(isActive.Value ? "true" : "false");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO categories ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertCategoryImageObjectAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('category-images') RETURNING id";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    // =========================================================================
    // Schema setup — the manage-categories deltas layered on RbacTestSchema's RBAC core
    // (plan §10: categories columns, ux_categories_name_normalized,
    // categories_public_read/categories_admin_insert, and the category-images bucket's
    // storage.objects policies; the categories.* permission seed itself lives in RbacTestSchema,
    // mirroring migration 0020)
    // =========================================================================

    private static async Task ApplyCategoriesSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE categories (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name        TEXT NOT NULL,
                description TEXT,
                image_path  TEXT,
                is_active   BOOLEAN NOT NULL DEFAULT TRUE,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX ux_categories_name_normalized ON categories (lower(btrim(name)));

            ALTER TABLE categories ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "categories_public_read" ON categories
                FOR SELECT USING (true);

            CREATE POLICY "categories_admin_insert" ON categories
                FOR INSERT WITH CHECK ((SELECT public.authorize('categories.create')));

            CREATE SCHEMA IF NOT EXISTS storage;

            CREATE TABLE IF NOT EXISTS storage.objects (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                bucket_id TEXT NOT NULL
            );

            ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "category_images_read" ON storage.objects
                FOR SELECT
                USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.view')));

            CREATE POLICY "category_images_insert" ON storage.objects
                FOR INSERT
                WITH CHECK (bucket_id = 'category-images' AND (SELECT public.authorize('categories.create')));

            -- categories.* permissions and the Admin grant are seeded by RbacTestSchema
            -- (mirroring migrations 0007 + 0020).
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: InsertCategory_WithValidData_RowCanBeSelectedBack,
//        CategoriesAdminInsert_WhenCallerHoldsCategoriesCreatePermission_Succeeds,
//        CategoriesPublicRead_WhenCategoryIsActiveAndCallerIsAnonymous_IsVisible
// AC-10: InsertCategory_WhenNameAlreadyExistsExactMatch_ThrowsUniqueViolation,
//         InsertCategory_WhenNameDiffersOnlyByCapitalization_ThrowsUniqueViolation,
//         InsertCategory_WhenNameDiffersOnlyByLeadingOrTrailingSpaces_ThrowsUniqueViolation,
//         InsertCategory_WhenNamesAreDifferent_BothRowsInserted
// AC-6 (image upload): CategoryImagesInsert_WhenCallerHoldsCategoriesCreatePermission_Succeeds
// AC-14 (storage-level backstop: a caller who should never reach upload cannot write the object):
//         CategoryImagesInsert_WhenCallerLacksCategoriesCreatePermission_ThrowsRowLevelSecurityViolation
// AC-7: InsertCategory_WithoutExplicitIsActive_DefaultsToTrue
// AC-15: CategoriesPublicRead_WhenCategoryIsInactiveAndCallerIsAnonymous_IsStillVisible
//         (Decision 2 — the facet, not RLS, hides an Inactive category from customers)
// AC-9: CategoriesAdminInsert_WhenCallerLacksCategoriesCreatePermission_ThrowsRowLevelSecurityViolation,
//        CategoryImagesRead_WhenCallerLacksCategoriesViewPermission_CannotSelectTheObject,
//        CategoryImagesRead_WhenCallerHoldsCategoriesViewPermission_CanSelectTheObject
