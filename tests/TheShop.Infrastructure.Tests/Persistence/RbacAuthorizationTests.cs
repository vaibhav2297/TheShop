using System.Text.Json;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the database-backed RBAC mechanism in its cumulative post-migration
/// state (<c>supabase/migrations/0007–0011</c>) — the guard triggers that enforce system-role
/// immutability, the self-role-change refusal, and the Super-Admin floor; the auto-Customer
/// assignment trigger; the <c>authorize()</c> RLS predicate (expiry-aware since 0009/0011); the
/// custom access token hook that mints <c>app_roles</c>/<c>perms</c>/<c>perm_v</c> claims
/// (0010); the <c>perm_version</c> bump + <c>access_audit</c> triggers (0009); the
/// <c>authorize_fresh()</c> token-freshness gate (0011); and the seed data's completeness
/// against <see cref="PermissionCatalogue"/> and least-privilege grants.
///
/// Spins up a real Postgres container (Testcontainers) and applies the migrations' schema,
/// functions, triggers, and seed, plus a minimal <c>auth</c> schema stub — see
/// <see cref="RbacTestSchema"/> — since a plain Postgres container has no Supabase Auth service.
/// The rewritten <c>customers</c>/product-images RLS policies are covered separately in
/// <see cref="RbacPolicyRegressionTests"/>.
/// </summary>
public sealed class RbacAuthorizationTests : IAsyncLifetime
{
    // Support's seeded grant set (0007 + 0015 + 0016 + 0017): every module's view permission
    // (dashboard.view included) plus orders.edit. Derived from the catalogue so a new module
    // extends it automatically.
    private static readonly string[] SupportGrantedCodes =
    [
        .. PermissionCatalogue.All
            .Select(p => p.Code)
            .Where(c => c.EndsWith(".view", StringComparison.Ordinal)),
        "orders.edit",
    ];

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
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    // =========================================================================
    // Trigger: auto-Customer assignment (FR-1, AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AssignCustomerRole_WhenNewAuthUserIsInserted_GrantsExactlyTheCustomerRole()
    {
        await using var conn = await OpenAsync();

        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);

        var roles = await RbacTestSchema.GetUserRoleNameKeysAsync(conn, userId);

        roles.Should().BeEquivalentTo(["Customer"]);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AssignCustomerRole_ForANewlySignedUpUser_GrantsNoPermissions()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var permissions = await RbacTestSchema.CallGetMyPermissionsAsync(conn);

        permissions.Should().BeEmpty();
    }

    // =========================================================================
    // Trigger: system-role immutability (FR-2, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task RolesSystemProtection_WhenRenamingASystemRole_ThrowsInsufficientPrivilege()
    {
        await using var conn = await OpenAsync();

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE roles SET name_key = 'Hacked' WHERE name_key = 'Support'";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task RolesSystemProtection_WhenDeletingASystemRole_ThrowsInsufficientPrivilege()
    {
        await using var conn = await OpenAsync();

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM roles WHERE name_key = 'Support'";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // Trigger: self-role-change refusal (FR-10, AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task UserRolesSelfChangeGuard_WhenAUserAssignsARoleToThemselves_ThrowsInsufficientPrivilege()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        var supportRoleId = await RbacTestSchema.GetRoleIdAsync(conn, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO user_roles (user_id, role_id) VALUES ('{userId}', '{supportRoleId}')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task UserRolesSelfChangeGuard_WhenAssigningARoleToAnotherUser_Succeeds()
    {
        await using var conn = await OpenAsync();
        var actingUserId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var targetUserId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, actingUserId, DateTimeOffset.UtcNow);
        var supportRoleId = await RbacTestSchema.GetRoleIdAsync(conn, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, actingUserId, sessionId);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO user_roles (user_id, role_id) VALUES ('{targetUserId}', '{supportRoleId}')";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task UserRolesSelfChangeGuard_WhenChangeIsMadeWithNoRequestContext_Succeeds()
    {
        // Migrations/seeds run with auth.uid() NULL — exempt (Design Decision 4).
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);

        var act = () => RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // Trigger: Super-Admin floor on user_roles (FR-10, AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task SuperAdminFloor_WhenRemovingTheLastSuperAdmin_ThrowsInsufficientPrivilege()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "SuperAdmin");
        var superAdminRoleId = await RbacTestSchema.GetRoleIdAsync(conn, "SuperAdmin");

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM user_roles WHERE user_id = '{userId}' AND role_id = '{superAdminRoleId}'";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task SuperAdminFloor_WhenASecondSuperAdminExists_RemovingOneSucceeds()
    {
        await using var conn = await OpenAsync();
        var userA = await RbacTestSchema.InsertAuthUserAsync(conn);
        var userB = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userA, "SuperAdmin");
        await RbacTestSchema.AssignRoleByNameAsync(conn, userB, "SuperAdmin");
        var superAdminRoleId = await RbacTestSchema.GetRoleIdAsync(conn, "SuperAdmin");

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM user_roles WHERE user_id = '{userA}' AND role_id = '{superAdminRoleId}'";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // Trigger: Super-Admin floor on role_permissions (FR-10, AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task SuperAdminFloor_WhenStrippingTheLastPermissionFromSuperAdminRole_ThrowsInsufficientPrivilege()
    {
        await using var conn = await OpenAsync();
        var superAdminRoleId = await RbacTestSchema.GetRoleIdAsync(conn, "SuperAdmin");

        // Isolate the Super Admin role to exactly one permission so its removal is "the last one".
        await using (var narrow = conn.CreateCommand())
        {
            narrow.CommandText = $"""
                DELETE FROM role_permissions
                WHERE role_id = '{superAdminRoleId}'
                  AND permission_id <> (SELECT id FROM permissions WHERE code = 'roles.view')
                """;
            await narrow.ExecuteNonQueryAsync();
        }
        var remainingPermissionId = await RbacTestSchema.GetPermissionIdAsync(conn, "roles.view");

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM role_permissions WHERE role_id = '{superAdminRoleId}' AND permission_id = '{remainingPermissionId}'";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // authorize() — the single RLS predicate truth table (FR-5, FR-7, FR-11, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenCallerIsUnauthenticated_ReturnsFalse()
    {
        await using var conn = await OpenAsync();

        (await RbacTestSchema.CallAuthorizeAsync(conn, "products.view")).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenCallerHoldsThePermission_ReturnsTrue()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeAsync(conn, "orders.view")).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenCallerLacksThePermission_ReturnsFalse()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeAsync(conn, "orders.refund")).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenTheAssignmentHasExpired_ReturnsFalse()
    {
        // A time-bound (just-in-time) assignment stops granting the moment it lapses (0009).
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeAsync(conn, "orders.view")).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenTheAssignmentExpiresInTheFuture_ReturnsTrue()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support", expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeAsync(conn, "orders.view")).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenPermissionIsRevokedMidSession_TheVeryNextCallReturnsFalse()
    {
        // AC-7: a revoked permission blocks the very next attempt — no sign-out required.
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeAsync(conn, "orders.view")).Should().BeTrue();

        // A configuration change revokes the Support role — no JWT refresh/re-login happens.
        await RbacTestSchema.RemoveRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeAsync(conn, "orders.view")).Should().BeFalse();
    }

    // =========================================================================
    // get_my_permissions() — used by IPermissionService/GetMyPermissionsQuery
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetMyPermissions_WhenUserHoldsTheSupportRole_ReturnsExactlyItsGrantedCodes()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        var codes = await RbacTestSchema.CallGetMyPermissionsAsync(conn);

        codes.Should().BeEquivalentTo(SupportGrantedCodes);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetMyPermissions_WhenCallerIsUnauthenticated_ReturnsEmptySet()
    {
        await using var conn = await OpenAsync();

        var codes = await RbacTestSchema.CallGetMyPermissionsAsync(conn);

        codes.Should().BeEmpty();
    }

    // =========================================================================
    // custom_access_token_hook() — mints app_roles / perms / perm_v claims (0010)
    // =========================================================================

    private static JsonElement HookClaims(string hookOutput) =>
        JsonDocument.Parse(hookOutput).RootElement.GetProperty("claims");

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Hook_WhenUserHoldsSuperAdmin_MintsEveryCataloguePermissionAndTheRole()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "SuperAdmin");

        var claims = HookClaims(await RbacTestSchema.CallAccessTokenHookAsync(conn, userId.ToString()));

        claims.GetProperty("perms").EnumerateArray().Select(p => p.GetString())
            .Should().BeEquivalentTo(PermissionCatalogue.All.Select(p => p.Code));
        claims.GetProperty("app_roles").EnumerateArray().Select(r => r.GetString())
            .Should().Contain("SuperAdmin");
        claims.GetProperty("perm_v").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        claims.GetProperty("role").GetString().Should().Be("authenticated",
            "the hook must merge claims, never replace the ones Supabase already minted");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Hook_WhenUserIsAPlainCustomer_MintsZeroPermissions()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // auto-Customer via trigger

        var claims = HookClaims(await RbacTestSchema.CallAccessTokenHookAsync(conn, userId.ToString()));

        claims.GetProperty("perms").EnumerateArray().Should().BeEmpty();
        claims.GetProperty("app_roles").EnumerateArray().Select(r => r.GetString())
            .Should().BeEquivalentTo(["Customer"]);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Hook_WhenTheAssignmentHasExpired_ExcludesItsPermissionsFromTheToken()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var claims = HookClaims(await RbacTestSchema.CallAccessTokenHookAsync(conn, userId.ToString()));

        claims.GetProperty("perms").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Hook_WhenTheEventIsMalformed_FailsClosedWithEmptyPermsInsteadOfAborting()
    {
        // A hook failure must never block token issuance (users could not sign in at all),
        // but it may only ever remove access — never grant it.
        await using var conn = await OpenAsync();

        var claims = HookClaims(await RbacTestSchema.CallAccessTokenHookAsync(conn, "not-a-uuid"));

        claims.GetProperty("perms").EnumerateArray().Should().BeEmpty();
        claims.GetProperty("app_roles").EnumerateArray().Should().BeEmpty();
        claims.GetProperty("perm_v").GetInt32().Should().Be(0);
    }

    // =========================================================================
    // perm_version bump triggers + authorize_fresh() token-freshness gate (0009/0011)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task PermVersion_WhenARoleAssignmentChanges_Bumps()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn); // auto-Customer bumps once already
        var afterSignup = await RbacTestSchema.GetPermVersionAsync(conn, userId);

        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");

        (await RbacTestSchema.GetPermVersionAsync(conn, userId)).Should().BeGreaterThan(afterSignup);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AuthorizeFresh_WhenTheTokenPermVersionIsCurrent_ReturnsTrue()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        var currentVersion = await RbacTestSchema.GetPermVersionAsync(conn, userId);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId, permVersion: currentVersion);

        (await RbacTestSchema.CallAuthorizeFreshAsync(conn, "orders.view")).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AuthorizeFresh_WhenTheTokenPredatesAPermissionChange_ReturnsFalse()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        var staleVersion = await RbacTestSchema.GetPermVersionAsync(conn, userId);

        // A later change bumps the version; the caller still presents the older token.
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId, permVersion: staleVersion);

        (await RbacTestSchema.CallAuthorizeFreshAsync(conn, "orders.view")).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AuthorizeFresh_WhenTheTokenCarriesNoPermVersionClaim_ReturnsFalse()
    {
        // Tokens minted before the hook was enabled must fail the freshness gate — fail closed.
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        (await RbacTestSchema.CallAuthorizeFreshAsync(conn, "orders.view")).Should().BeFalse();
    }

    // =========================================================================
    // access_audit — append-only RBAC change trail (0009)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AccessAudit_WhenARoleIsGrantedAndRevoked_WritesBothTrailRows()
    {
        await using var conn = await OpenAsync();
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);

        await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Support");
        await RbacTestSchema.RemoveRoleByNameAsync(conn, userId, "Support");

        var actions = await RbacTestSchema.GetAuditActionsForUserAsync(conn, userId);
        // The signup trigger's auto-Customer grant is audited too, hence Contain over equality.
        actions.Should().Contain("user_role.granted").And.Contain("user_role.revoked");
    }

    // =========================================================================
    // Seed completeness vs. PermissionCatalogue (FR-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Seed_PermissionCodes_MatchPermissionCatalogueExactly()
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT code FROM permissions";
        await using var reader = await cmd.ExecuteReaderAsync();
        var dbCodes = new List<string>();
        while (await reader.ReadAsync()) dbCodes.Add(reader.GetString(0));

        dbCodes.Should().BeEquivalentTo(PermissionCatalogue.All.Select(p => p.Code));
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Seed_AllFourBuiltInRoles_ExistAndAreMarkedSystem()
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name_key, is_system FROM roles";
        await using var reader = await cmd.ExecuteReaderAsync();
        var roles = new Dictionary<string, bool>();
        while (await reader.ReadAsync()) roles[reader.GetString(0)] = reader.GetBoolean(1);

        roles.Keys.Should().BeEquivalentTo(["Customer", "Support", "Admin", "SuperAdmin"]);
        roles.Values.Should().OnlyContain(isSystem => isSystem);
    }

    // =========================================================================
    // Seed grants — least-privilege per spec Constraints (AC-2, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Seed_CustomerRole_GrantsNoPermissions()
    {
        await using var conn = await OpenAsync();

        var codes = await RbacTestSchema.GetRolePermissionCodesAsync(conn, "Customer");

        codes.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Seed_SupportRole_GrantsEveryViewPermissionPlusOrdersEdit()
    {
        await using var conn = await OpenAsync();

        var codes = await RbacTestSchema.GetRolePermissionCodesAsync(conn, "Support");

        codes.Should().BeEquivalentTo(SupportGrantedCodes);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Seed_AdminRole_GrantsEveryMerchandisingModuleButNotSettingsAdminUsersOrRoles()
    {
        await using var conn = await OpenAsync();

        var codes = await RbacTestSchema.GetRolePermissionCodesAsync(conn, "Admin");

        codes.Should().NotContain(c => c.StartsWith("settings.", StringComparison.Ordinal)
                                        || c.StartsWith("admin_users.", StringComparison.Ordinal)
                                        || c.StartsWith("roles.", StringComparison.Ordinal));
        codes.Should().Contain([
            "products.view", "products.create", "products.edit", "products.delete",
            "categories.view", "orders.view", "orders.refund", "customers.export",
            "coupons.view", "promotions.view", "reports.view", "dashboard.view",
        ]);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Seed_SuperAdminRole_GrantsEveryCataloguePermission()
    {
        await using var conn = await OpenAsync();

        var codes = await RbacTestSchema.GetRolePermissionCodesAsync(conn, "SuperAdmin");

        codes.Should().BeEquivalentTo(PermissionCatalogue.All.Select(p => p.Code));
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: AssignCustomerRole_WhenNewAuthUserIsInserted_GrantsExactlyTheCustomerRole,
//        AssignCustomerRole_ForANewlySignedUpUser_GrantsNoAdminAreaPermissions
// AC-2: RolesSystemProtection_WhenRenamingASystemRole_ThrowsInsufficientPrivilege,
//        RolesSystemProtection_WhenDeletingASystemRole_ThrowsInsufficientPrivilege,
//        Seed_AllFourBuiltInRoles_ExistAndAreMarkedSystem
// AC-3: Seed_PermissionCodes_MatchPermissionCatalogueExactly
// AC-5: Authorize_WhenCallerLacksThePermission_ReturnsFalse
// AC-6: Seed_SupportRole_GrantsEveryViewPermissionPlusOrdersEdit,
//        Seed_AdminRole_GrantsEveryMerchandisingModuleButNotSettingsAdminUsersOrRoles,
//        Seed_SuperAdminRole_GrantsEveryCataloguePermission,
//        Seed_CustomerRole_GrantsNoPermissions
// AC-7: Authorize_WhenPermissionIsRevokedMidSession_TheVeryNextCallReturnsFalse
// AC-8: UserRolesSelfChangeGuard_WhenAUserAssignsARoleToThemselves_ThrowsInsufficientPrivilege,
//        UserRolesSelfChangeGuard_WhenAssigningARoleToAnotherUser_Succeeds,
//        UserRolesSelfChangeGuard_WhenChangeIsMadeWithNoRequestContext_Succeeds
// AC-9: SuperAdminFloor_WhenRemovingTheLastSuperAdmin_ThrowsInsufficientPrivilege,
//        SuperAdminFloor_WhenASecondSuperAdminExists_RemovingOneSucceeds,
//        SuperAdminFloor_WhenStrippingTheLastPermissionFromSuperAdminRole_ThrowsInsufficientPrivilege
// AC-10: Authorize_WhenCallerIsUnauthenticated_ReturnsFalse
