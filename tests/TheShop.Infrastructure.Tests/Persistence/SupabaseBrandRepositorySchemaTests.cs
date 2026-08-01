using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the add-brand schema and RLS deltas on the <c>brands</c> table plus the
/// new <c>brand-logos</c> storage bucket policies, exercised by
/// <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseBrandRepository"/> and
/// <see cref="TheShop.Infrastructure.Storage.SupabaseFileStorage"/>. Sourced from the plan's
/// Data Model (§4) and Database Schema &amp; RLS Policies (§10): the <c>ux_brands_normalized_name</c>
/// unique index (RULE-2 storage backstop, AC-3), the rewritten <c>brands_read</c> policy that
/// hides Inactive brands from customers while keeping them visible to <c>brands.view</c> holders
/// (Decision 3, FR-7/AC-7), the <c>brands_admin_insert</c> policy (AC-8), and the
/// <c>brand-logos</c> bucket's insert/read policies (Decision 4).
///
/// Spins up a real Postgres container (Testcontainers), reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (auth stub, <c>authorize()</c>, roles/permissions seed — the same
/// mechanism <see cref="RbacPolicyRegressionTests"/> and <see cref="RbacAuthorizationTests"/> use),
/// then layers the <c>brands</c> table and <c>storage.objects</c> stub on top, seeding the four
/// <c>brands.*</c> permissions and granting the module to Admin (plan §10). Record ↔ domain
/// mapping is covered separately in <see cref="BrandMapperTests"/>.
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public sealed class SupabaseBrandRepositorySchemaTests : IAsyncLifetime
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
        await ApplyBrandsSchemaAsync(conn);
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
    // ux_brands_normalized_name — case/space-insensitive uniqueness (RULE-2, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task InsertBrand_WhenNameAlreadyExistsExactMatch_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        var act = () => InsertBrandAsync(conn, "Elf Bar", "elf-bar-2");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task InsertBrand_WhenNameDiffersOnlyByCapitalization_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        var act = () => InsertBrandAsync(conn, "ELF BAR", "elf-bar-2");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task InsertBrand_WhenNameDiffersOnlyByLeadingOrTrailingSpaces_ThrowsUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        var act = () => InsertBrandAsync(conn, "  Elf Bar  ", "elf-bar-2");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task InsertBrand_WhenNamesAreDifferent_BothRowsInserted()
    {
        await using var conn = await OpenAsync();

        var act = async () =>
        {
            await InsertBrandAsync(conn, "Elf Bar", "elf-bar");
            await InsertBrandAsync(conn, "Lost Mary", "lost-mary");
        };

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // is_active default + happy-path round-trip
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task InsertBrand_WithoutExplicitIsActive_DefaultsToTrue()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        var isActive = await ScalarAsync(conn, $"SELECT is_active FROM brands WHERE id = '{id}'");

        ((bool)isActive!).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task InsertBrand_WithValidData_RowCanBeSelectedBack()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(
            conn, "Elf Bar", "elf-bar", description: "A vape brand.", logoPath: "brands/abc/logo.webp", isActive: false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT name, slug, description, logo_path, is_active FROM brands WHERE id = '{id}'
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        var read = await reader.ReadAsync();

        read.Should().BeTrue("the row must exist after a successful insert");
        reader.GetString(0).Should().Be("Elf Bar");
        reader.GetString(1).Should().Be("elf-bar");
        reader.GetString(2).Should().Be("A vape brand.");
        reader.GetString(3).Should().Be("brands/abc/logo.webp");
        reader.GetBoolean(4).Should().BeFalse();
    }

    // =========================================================================
    // brands_read — Inactive brands hidden from customers, visible to brands.view staff
    // (Decision 3, FR-7, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandsRead_WhenBrandIsActiveAndCallerIsAnonymous_IsVisible()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar", "elf-bar", isActive: true);

        await using var anonConn = await OpenAsAnonAsync();
        var count = await ScalarAsync(anonConn, $"SELECT COUNT(*) FROM brands WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(1, "an Active brand must be visible everywhere brands are used");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandsRead_WhenBrandIsInactiveAndCallerIsAnonymous_IsHidden()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar", "elf-bar", isActive: false);

        await using var anonConn = await OpenAsAnonAsync();
        var count = await ScalarAsync(anonConn, $"SELECT COUNT(*) FROM brands WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(0,
            "an Inactive brand must not appear anywhere in the store, including the catalogue's brand filter");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandsRead_WhenBrandIsInactiveAndCallerLacksBrandsViewPermission_IsHidden()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar", "elf-bar", isActive: false);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM brands WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(0,
            "hiding the control must never be the only protection — RLS must filter the row for product assignment too");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandsRead_WhenBrandIsInactiveAndCallerHoldsBrandsViewPermission_IsVisible()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar", "elf-bar", isActive: false);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM brands WHERE id = '{id}'");

        Convert.ToInt32(count).Should().Be(1,
            "an Inactive brand must remain visible to staff in the admin panel's brand list (FR-7)");
    }

    // =========================================================================
    // brands_admin_insert — authorize('brands.create') (AC-1 happy path, AC-8 boundary)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandsAdminInsert_WhenCallerHoldsBrandsCreatePermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = () => InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandsAdminInsert_WhenCallerLacksBrandsCreatePermission_ThrowsRowLevelSecurityViolation()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = () => InsertBrandAsync(conn, "Elf Bar", "elf-bar");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // brand-logos storage bucket policies (Decision 4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandLogosInsert_WhenCallerHoldsBrandsCreatePermission_Succeeds()
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
            cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('brand-logos')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandLogosInsert_WhenCallerLacksBrandsCreatePermission_ThrowsRowLevelSecurityViolation()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('brand-logos')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandLogosRead_WhenCallerHoldsBrandsViewPermission_CanSelectTheObject()
    {
        await using var conn = await OpenAsync();
        var objectId = await InsertBrandLogoObjectAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM storage.objects WHERE id = '{objectId}'");

        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task BrandLogosRead_WhenCallerLacksBrandsViewPermission_CannotSelectTheObject()
    {
        await using var conn = await OpenAsync();
        var objectId = await InsertBrandLogoObjectAsync(conn);
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

    private static async Task<Guid> InsertBrandAsync(
        NpgsqlConnection conn,
        string name,
        string slug,
        string? description = null,
        string? logoPath = null,
        bool? isActive = null)
    {
        var columns = new List<string> { "name", "slug" };
        var values = new List<string> { $"'{name.Replace("'", "''")}'", $"'{slug}'" };

        if (description is not null)
        {
            columns.Add("description");
            values.Add($"'{description.Replace("'", "''")}'");
        }

        if (logoPath is not null)
        {
            columns.Add("logo_path");
            values.Add($"'{logoPath}'");
        }

        if (isActive is not null)
        {
            columns.Add("is_active");
            values.Add(isActive.Value ? "true" : "false");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO brands ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertBrandLogoObjectAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('brand-logos') RETURNING id";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    // =========================================================================
    // Schema setup — the add-brand deltas layered on RbacTestSchema's RBAC core
    // (plan §10: brands columns, ux_brands_normalized_name, brands_read/brands_admin_insert,
    // and the brand-logos bucket's storage.objects policies; the brands.* permission seed
    // itself now lives in RbacTestSchema, mirroring migration 0015)
    // =========================================================================

    private static async Task ApplyBrandsSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE brands (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name        TEXT NOT NULL,
                slug        TEXT NOT NULL UNIQUE,
                description TEXT,
                logo_path   TEXT,
                is_active   BOOLEAN NOT NULL DEFAULT TRUE,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX ux_brands_normalized_name ON brands (lower(btrim(name)));

            ALTER TABLE brands ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "brands_read" ON brands
                FOR SELECT USING (is_active OR (SELECT public.authorize('brands.view')));

            CREATE POLICY "brands_admin_insert" ON brands
                FOR INSERT WITH CHECK ((SELECT public.authorize('brands.create')));

            CREATE SCHEMA IF NOT EXISTS storage;

            CREATE TABLE IF NOT EXISTS storage.objects (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                bucket_id TEXT NOT NULL
            );

            ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "brand_logos_read" ON storage.objects
                FOR SELECT
                USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.view')));

            CREATE POLICY "brand_logos_insert" ON storage.objects
                FOR INSERT
                WITH CHECK (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.create')));

            -- brands.* permissions and the Admin grant are seeded by RbacTestSchema
            -- (mirroring migrations 0007 + 0015).
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: InsertBrand_WithValidData_RowCanBeSelectedBack,
//        BrandsAdminInsert_WhenCallerHoldsBrandsCreatePermission_Succeeds,
//        BrandsRead_WhenBrandIsActiveAndCallerIsAnonymous_IsVisible
// AC-3: InsertBrand_WhenNameAlreadyExistsExactMatch_ThrowsUniqueViolation,
//        InsertBrand_WhenNameDiffersOnlyByCapitalization_ThrowsUniqueViolation,
//        InsertBrand_WhenNameDiffersOnlyByLeadingOrTrailingSpaces_ThrowsUniqueViolation,
//        InsertBrand_WhenNamesAreDifferent_BothRowsInserted
// AC-4: BrandLogosInsert_WhenCallerHoldsBrandsCreatePermission_Succeeds
// AC-5: BrandLogosInsert_WhenCallerLacksBrandsCreatePermission_ThrowsRowLevelSecurityViolation
//        (storage-level backstop: a caller who should never reach upload cannot write the object)
// AC-6: InsertBrand_WithoutExplicitIsActive_DefaultsToTrue
// AC-7: BrandsRead_WhenBrandIsInactiveAndCallerIsAnonymous_IsHidden,
//        BrandsRead_WhenBrandIsInactiveAndCallerLacksBrandsViewPermission_IsHidden,
//        BrandsRead_WhenBrandIsInactiveAndCallerHoldsBrandsViewPermission_IsVisible
// AC-8: BrandsAdminInsert_WhenCallerLacksBrandsCreatePermission_ThrowsRowLevelSecurityViolation,
//        BrandLogosRead_WhenCallerLacksBrandsViewPermission_CannotSelectTheObject,
//        BrandLogosRead_WhenCallerHoldsBrandsViewPermission_CanSelectTheObject
