using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the <c>admin_module_count</c> <c>SECURITY DEFINER</c> function (migration
/// <c>0013_admin_dashboard_counts.sql</c>) that backs <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseAdminDashboardRepository"/>
/// — the plan's real authorization boundary for the admin console (spec RULE-2/RULE-3, FR-3, FR-5,
/// AC-3): the function re-checks each module's <c>authorize()</c> gate itself (so hiding a card in
/// the UI is never the only protection) and counts <b>every</b> row regardless of storefront
/// RLS/status — unpublished products, inactive brands, and the full <c>auth.users</c> total
/// (customers + staff) — plus the zero-records edge case and the unknown-module boundary.
///
/// Spins up a real Postgres container (Testcontainers) and reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (auth stub, <c>authorize()</c>, roles/permissions seed — the same
/// mechanism <see cref="RbacAuthorizationTests"/> and <see cref="SupabaseBrandRepositorySchemaTests"/>
/// use, including the <c>brands.*</c> permission seed mirroring migration 0015), then layers
/// minimal <c>products</c>/<c>categories</c>/<c>brands</c> stub tables on top, plus the
/// <c>admin_module_count</c> function itself, applied verbatim from migration 0013.
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public sealed class SupabaseAdminDashboardRepositorySchemaTests : IAsyncLifetime
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
        await RbacTestSchema.CreateRlsTestRolesAsync(conn); // so the migration's "GRANT ... TO authenticated" resolves
        await ApplyAdminDashboardSchemaAsync(conn);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    // =========================================================================
    // Per-module authorize() re-check — denied without the gate (RULE-2, AC-3)
    // =========================================================================

    [Theory]
    [InlineData("products")]
    [InlineData("categories")]
    [InlineData("brands")]
    [InlineData("users")]
    [InlineData("roles")]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_WhenCallerHoldsNoAdminPermissions_ThrowsInsufficientPrivilege(string module)
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // auto-Customer only — zero permissions
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var act = () => CallAdminModuleCountAsync(conn, module);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_WhenCallerIsUnauthenticated_ThrowsInsufficientPrivilege()
    {
        // No JWT claims set at all on this connection — auth.uid() is NULL (RULE-1 defense-in-depth).
        await using var conn = await OpenAsync();

        var act = () => CallAdminModuleCountAsync(conn, "roles");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // Products — counts every row, including unpublished ones (FR-3, RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_ForProducts_WhenCallerHoldsProductsView_ReturnsAllProductsRegardlessOfPublishedStatus()
    {
        await using var conn = await OpenAsync();
        await InsertProductAsync(conn, isPublished: true);
        await InsertProductAsync(conn, isPublished: true);
        await InsertProductAsync(conn, isPublished: false);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var count = await CallAdminModuleCountAsync(conn, "products");

        count.Should().Be(3, "the storefront's published-only visibility must never hide rows from the admin count");
    }

    // =========================================================================
    // Brands — counts every row, including inactive ones (FR-3, RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_ForBrands_WhenCallerHoldsBrandsView_ReturnsAllBrandsRegardlessOfActiveStatus()
    {
        await using var conn = await OpenAsync();
        await InsertBrandAsync(conn, isActive: true);
        await InsertBrandAsync(conn, isActive: true);
        await InsertBrandAsync(conn, isActive: false);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin"); // holds brands.view (seeded by RbacTestSchema)
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var count = await CallAdminModuleCountAsync(conn, "brands");

        count.Should().Be(3, "an inactive brand must still be counted, matching the true current total (RULE-3)");
    }

    // =========================================================================
    // Categories — true current total (RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_ForCategories_WhenCallerHoldsCategoriesView_ReturnsTheTrueCategoryCount()
    {
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn);
        await InsertCategoryAsync(conn);
        await InsertCategoryAsync(conn);
        await InsertCategoryAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var count = await CallAdminModuleCountAsync(conn, "categories");

        count.Should().Be(4);
    }

    // =========================================================================
    // Users — the full auth.users total, customers and staff alike (FR-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_ForUsers_WhenCallerHoldsAdminUsersView_ReturnsTheFullAuthUsersTotal()
    {
        await using var conn = await OpenAsync();
        await RbacTestSchema.InsertAuthUserAsync(conn); // plain customer
        await RbacTestSchema.InsertAuthUserAsync(conn); // plain customer
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "SuperAdmin"); // only SuperAdmin holds admin_users.view
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        var expectedTotal = await ScalarLongAsync(conn, "SELECT count(*) FROM auth.users");

        var count = await CallAdminModuleCountAsync(conn, "users");

        count.Should().Be(expectedTotal, "the Users count must include every registered account, not just staff");
        count.Should().BeGreaterThanOrEqualTo(3);
    }

    // =========================================================================
    // Roles — the true current total (RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_ForRoles_WhenCallerHoldsRolesView_ReturnsTheTrueRoleCount()
    {
        await using var conn = await OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO roles (name_key, is_system) VALUES ('Warehouse', false)";
            await cmd.ExecuteNonQueryAsync();
        }
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "SuperAdmin"); // only SuperAdmin holds roles.view
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        var expectedTotal = await ScalarLongAsync(conn, "SELECT count(*) FROM roles");

        var count = await CallAdminModuleCountAsync(conn, "roles");

        count.Should().Be(expectedTotal);
    }

    // =========================================================================
    // Zero records — a real 0, not an error (spec Edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_WhenTheModuleHasZeroRecords_ReturnsZero()
    {
        await using var conn = await OpenAsync(); // fresh container: no categories inserted yet
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var count = await CallAdminModuleCountAsync(conn, "categories");

        count.Should().Be(0);
    }

    // =========================================================================
    // Unknown module — defensive boundary on the function's own parameter contract
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task AdminModuleCount_WhenTheModuleIsUnrecognized_ThrowsInvalidParameterValue()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "SuperAdmin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var act = () => CallAdminModuleCountAsync(conn, "not-a-module");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "22023");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<long> ScalarLongAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> CallAdminModuleCountAsync(NpgsqlConnection conn, string module)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT public.admin_module_count('{module}')";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task InsertProductAsync(NpgsqlConnection conn, bool isPublished)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO public.products (is_published) VALUES ({(isPublished ? "true" : "false")})";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertCategoryAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.categories DEFAULT VALUES";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertBrandAsync(NpgsqlConnection conn, bool isActive)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO public.brands (is_active) VALUES ({(isActive ? "true" : "false")})";
        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================================
    // Schema setup — minimal products/categories/brands stubs (this feature reads existing
    // tables; the real column shapes are owned by their own features/migrations) plus
    // admin_module_count itself, applied verbatim from migration 0013. The brands.*
    // permission seed comes from RbacTestSchema (mirroring migration 0015).
    // =========================================================================

    private static async Task ApplyAdminDashboardSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE public.products (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                is_published BOOLEAN NOT NULL DEFAULT true
            );

            CREATE TABLE public.categories (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid()
            );

            CREATE TABLE public.brands (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                is_active BOOLEAN NOT NULL DEFAULT true
            );

            -- brands.* permissions and the Admin grant are seeded by RbacTestSchema
            -- (mirroring migrations 0007 + 0015).

            -- Verbatim from supabase/migrations/0013_admin_dashboard_counts.sql
            CREATE OR REPLACE FUNCTION public.admin_module_count(p_module TEXT)
            RETURNS BIGINT
            LANGUAGE plpgsql SECURITY DEFINER STABLE
            SET search_path = public AS $$
            DECLARE
                v_count BIGINT;
            BEGIN
                CASE p_module
                    WHEN 'products' THEN
                        IF NOT public.authorize('products.view')   THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
                        SELECT count(*) INTO v_count FROM public.products;
                    WHEN 'categories' THEN
                        IF NOT public.authorize('categories.view') THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
                        SELECT count(*) INTO v_count FROM public.categories;
                    WHEN 'brands' THEN
                        IF NOT public.authorize('brands.view')     THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
                        SELECT count(*) INTO v_count FROM public.brands;
                    WHEN 'users' THEN
                        IF NOT public.authorize('admin_users.view') THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
                        SELECT count(*) INTO v_count FROM auth.users;
                    WHEN 'roles' THEN
                        IF NOT public.authorize('roles.view')      THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
                        SELECT count(*) INTO v_count FROM public.roles;
                    ELSE
                        RAISE EXCEPTION 'unknown module %', p_module USING ERRCODE = 'invalid_parameter_value';
                END CASE;

                RETURN v_count;
            END $$;

            GRANT EXECUTE ON FUNCTION public.admin_module_count(TEXT) TO authenticated;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (supports; the correct-count half of the happy path): AdminModuleCount_ForProducts_WhenCallerHoldsProductsView_ReturnsAllProductsRegardlessOfPublishedStatus,
//        AdminModuleCount_ForCategories_WhenCallerHoldsCategoriesView_ReturnsTheTrueCategoryCount,
//        AdminModuleCount_ForBrands_WhenCallerHoldsBrandsView_ReturnsAllBrandsRegardlessOfActiveStatus,
//        AdminModuleCount_ForUsers_WhenCallerHoldsAdminUsersView_ReturnsTheFullAuthUsersTotal,
//        AdminModuleCount_ForRoles_WhenCallerHoldsRolesView_ReturnsTheTrueRoleCount
// AC-3: AdminModuleCount_WhenCallerHoldsNoAdminPermissions_ThrowsInsufficientPrivilege
// AC-4 (defense-in-depth at the database boundary): AdminModuleCount_WhenCallerIsUnauthenticated_ThrowsInsufficientPrivilege
// AC-5 (supports — the storage-level truth the placeholder falls back from): the "counts every
//        row regardless of status" tests above
// (spec Edge case — zero records): AdminModuleCount_WhenTheModuleHasZeroRecords_ReturnsZero
