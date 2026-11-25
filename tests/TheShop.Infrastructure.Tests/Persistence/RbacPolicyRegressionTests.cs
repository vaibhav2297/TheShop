using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Regression guard for the RLS policies migration <c>0007_create_rbac.sql</c> rewrote off the
/// old JWT role-name check (<c>auth.jwt() ->> 'role' = 'admin'</c>) onto the single
/// <c>public.authorize(...)</c> predicate (FR-5, FR-11) — the <c>customers</c> select policy
/// (migration 0001) and the four <c>product-images</c> storage policies (migration 0004).
///
/// Unlike <see cref="RbacAuthorizationTests"/>, these tests exercise actual row-level security
/// enforcement end-to-end: queries run as a non-owner, non-superuser <c>authenticated</c>
/// Postgres role (matching Supabase's real RLS-subject role) so Postgres does not silently
/// bypass the policies the way it would for the migration-applying superuser connection.
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public sealed class RbacPolicyRegressionTests : IAsyncLifetime
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
        await ApplyCustomersAndStorageSchemaAsync(conn);
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

    // Mirrors migration 0001 (customers) with the select policy in its migration-0011 form
    // ((SELECT public.authorize(...)) InitPlan wrapping), and a minimal storage.objects stub
    // carrying only the column the rewritten product-images policies (migration 0004, rewritten
    // by 0007, wrapped by 0011) actually reference.
    private static async Task ApplyCustomersAndStorageSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE customers (
                id              UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
                first_name      TEXT NOT NULL,
                last_name       TEXT NOT NULL,
                date_of_birth   DATE NOT NULL,
                email           TEXT NOT NULL,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            ALTER TABLE customers ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "customers_select" ON customers
                FOR SELECT USING (
                    (SELECT auth.uid()) = id
                    OR (SELECT public.authorize('customers.view'))
                );

            CREATE POLICY "customers_self_insert" ON customers
                FOR INSERT WITH CHECK ((SELECT auth.uid()) = id);

            CREATE POLICY "customers_self_update" ON customers
                FOR UPDATE
                USING     ((SELECT auth.uid()) = id)
                WITH CHECK ((SELECT auth.uid()) = id);

            CREATE SCHEMA IF NOT EXISTS storage;

            CREATE TABLE storage.objects (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                bucket_id TEXT NOT NULL
            );

            ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "product_images_admin_read" ON storage.objects
                FOR SELECT
                USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.view')));

            CREATE POLICY "product_images_admin_insert" ON storage.objects
                FOR INSERT
                WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.create')));

            CREATE POLICY "product_images_admin_update" ON storage.objects
                FOR UPDATE
                USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')))
                WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')));

            CREATE POLICY "product_images_admin_delete" ON storage.objects
                FOR DELETE
                USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.delete')));
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SetRoleAuthenticatedAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE authenticated";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertCustomerAsync(NpgsqlConnection conn, Guid id, string email)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO customers (id, first_name, last_name, date_of_birth, email)
            VALUES ('{id}', 'Jane', 'Doe', '2000-01-01', '{email}')
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertProductImageAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('product-images') RETURNING id";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    // =========================================================================
    // customers_select — rewritten policy truth table
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task CustomersSelect_WhenUserSelectsOwnRow_ReturnsRowRegardlessOfPermission()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await InsertCustomerAsync(conn, userId, "self@example.com");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM customers WHERE id = '{userId}'";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue("a customer with no customers.view permission must still read their own row");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task CustomersSelect_WhenUserLacksCustomersViewPermission_CannotSeeAnotherCustomersRow()
    {
        await using var conn = await OpenAsync();
        var viewerId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var otherId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await InsertCustomerAsync(conn, otherId, "other@example.com");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, viewerId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, viewerId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM customers WHERE id = '{otherId}'";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeFalse("hiding the control must never be the only protection — RLS must filter the row");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task CustomersSelect_WhenUserHoldsCustomersViewPermission_CanSeeAnotherCustomersRow()
    {
        await using var conn = await OpenAsync();
        var supportUserId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, supportUserId, "Support");
        var otherId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await InsertCustomerAsync(conn, otherId, "other2@example.com");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, supportUserId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, supportUserId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM customers WHERE id = '{otherId}'";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue("Support holds customers.view — the authorize()-based replacement for the old admin JWT-role check");
    }

    // =========================================================================
    // product_images_admin_read — rewritten policy truth table
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesRead_WhenUserHoldsProductsViewPermission_CanSelectObjects()
    {
        await using var conn = await OpenAsync();
        var imageId = await InsertProductImageAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM storage.objects WHERE id = '{imageId}'";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesRead_WhenUserLacksProductsViewPermission_CannotSelectObjects()
    {
        await using var conn = await OpenAsync();
        var imageId = await InsertProductImageAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM storage.objects WHERE id = '{imageId}'";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeFalse();
    }

    // =========================================================================
    // product_images_admin_insert
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesInsert_WhenUserHoldsProductsCreatePermission_Succeeds()
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
            cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('product-images')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesInsert_WhenUserLacksProductsCreatePermission_ThrowsRowLevelSecurityViolation()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO storage.objects (bucket_id) VALUES ('product-images')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // product_images_admin_update
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesUpdate_WhenUserHoldsProductsEditPermission_UpdatesTheRow()
    {
        await using var conn = await OpenAsync();
        var imageId = await InsertProductImageAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE storage.objects SET bucket_id = 'product-images' WHERE id = '{imageId}'";
        var affected = await cmd.ExecuteNonQueryAsync();

        affected.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesUpdate_WhenUserLacksProductsEditPermission_AffectsNoRows()
    {
        await using var conn = await OpenAsync();
        var imageId = await InsertProductImageAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE storage.objects SET bucket_id = 'product-images' WHERE id = '{imageId}'";
        var affected = await cmd.ExecuteNonQueryAsync();

        affected.Should().Be(0, "an unauthorized update must change nothing, not merely be refused");
    }

    // =========================================================================
    // product_images_admin_delete
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesDelete_WhenUserHoldsProductsDeletePermission_DeletesTheRow()
    {
        await using var conn = await OpenAsync();
        var imageId = await InsertProductImageAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM storage.objects WHERE id = '{imageId}'";
        var affected = await cmd.ExecuteNonQueryAsync();

        affected.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task ProductImagesDelete_WhenUserLacksProductsDeletePermission_AffectsNoRows()
    {
        await using var conn = await OpenAsync();
        var imageId = await InsertProductImageAsync(conn);
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // Customer-only
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);

        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);
        await SetRoleAuthenticatedAsync(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM storage.objects WHERE id = '{imageId}'";
        var affected = await cmd.ExecuteNonQueryAsync();

        affected.Should().Be(0, "an unauthorized delete must change nothing, not merely be refused");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-5: CustomersSelect_WhenUserLacksCustomersViewPermission_CannotSeeAnotherCustomersRow,
//        ProductImagesRead_WhenUserLacksProductsViewPermission_CannotSelectObjects,
//        ProductImagesInsert_WhenUserLacksProductsCreatePermission_ThrowsRowLevelSecurityViolation
// AC-6: CustomersSelect_WhenUserHoldsCustomersViewPermission_CanSeeAnotherCustomersRow,
//        ProductImagesRead_WhenUserHoldsProductsViewPermission_CanSelectObjects,
//        ProductImagesInsert_WhenUserHoldsProductsCreatePermission_Succeeds
// AC-10: ProductImagesUpdate_WhenUserLacksProductsEditPermission_AffectsNoRows,
//         ProductImagesDelete_WhenUserLacksProductsDeletePermission_AffectsNoRows
