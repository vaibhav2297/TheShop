using Npgsql;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Shared schema/seed/query helpers for the RBAC Testcontainers suites
/// (<see cref="RbacAuthorizationTests"/>, <see cref="RbacPolicyRegressionTests"/>).
///
/// A plain Postgres container has no Supabase Auth service, so this class first stubs the
/// minimal slice of the <c>auth</c> schema the RBAC migrations depend on — <c>auth.users</c>,
/// <c>auth.sessions</c>, <c>auth.uid()</c>, <c>auth.jwt()</c> — reproducing Supabase's own
/// well-known implementation (GUC-backed, reading <c>request.jwt.claims</c>) so the actual
/// functions and triggers run unmodified against it. The applied SQL mirrors the cumulative
/// post-migration state of <c>supabase/migrations/0007–0011</c>: the RBAC schema and guards
/// (0007, with the role <c>name_key</c> rename applied), grant metadata / <c>expires_at</c> /
/// <c>user_access_meta.perm_version</c> / <c>access_audit</c> (0009), the custom access token
/// hook (0010, minus the <c>supabase_auth_admin</c> grants that only exist on a real Supabase
/// instance), and the hardened <c>authorize()</c> + <c>authorize_fresh()</c> (0011).
/// </summary>
internal static class RbacTestSchema
{
    // =========================================================================
    // Schema application
    // =========================================================================

    public static async Task ApplyAuthStubAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE SCHEMA IF NOT EXISTS auth;

            CREATE TABLE auth.users (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                email TEXT
            );

            CREATE TABLE auth.sessions (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            -- Reproduces Supabase's own auth.uid()/auth.jwt(): both read GUCs that PostgREST
            -- sets per-request from the caller's JWT. Tests drive them via SetJwtClaimsAsync.
            CREATE OR REPLACE FUNCTION auth.uid() RETURNS UUID
            LANGUAGE sql STABLE AS $$
                SELECT COALESCE(
                    NULLIF(current_setting('request.jwt.claim.sub', true), ''),
                    (NULLIF(current_setting('request.jwt.claims', true), '')::jsonb ->> 'sub')
                )::uuid
            $$;

            CREATE OR REPLACE FUNCTION auth.jwt() RETURNS JSONB
            LANGUAGE sql STABLE AS $$
                SELECT NULLIF(current_setting('request.jwt.claims', true), '')::jsonb
            $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Applies the cumulative post-0011 RBAC state: schema (with the 0009 columns), the hardened
    /// <c>authorize()</c>/<c>get_my_permissions()</c>/<c>authorize_fresh()</c>, guard triggers,
    /// <c>user_access_meta</c> + <c>access_audit</c> with their triggers, the custom access token
    /// hook, RLS, and the seed data.
    /// </summary>
    public static async Task ApplyRbacCoreAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SchemaAndFunctionsSql + TriggersSql + AccessMetaAndAuditSql + HookSql + RlsSql + SeedSql;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task CreateRlsTestRolesAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                    CREATE ROLE anon NOLOGIN;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                    CREATE ROLE authenticated NOLOGIN;
                END IF;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Grants the RLS-subject roles enough base privilege to reach every currently-existing
    /// table/function so row security (not a missing GRANT) is what a query is exercising.
    /// Call again after adding tables beyond <see cref="ApplyRbacCoreAsync"/>'s four.
    /// </summary>
    public static async Task GrantRlsRolePrivilegesAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            GRANT USAGE ON SCHEMA public TO anon, authenticated;
            GRANT SELECT ON ALL TABLES IN SCHEMA public TO anon;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO authenticated;
            GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO anon, authenticated;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task GrantStorageRlsRolePrivilegesAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            GRANT USAGE ON SCHEMA storage TO anon, authenticated;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA storage TO authenticated;
            GRANT SELECT ON ALL TABLES IN SCHEMA storage TO anon;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================================
    // Auth-context helpers
    // =========================================================================

    public static async Task<Guid> InsertAuthUserAsync(NpgsqlConnection conn, string? email = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO auth.users (email) VALUES ('{email ?? $"{Guid.NewGuid()}@example.com"}') RETURNING id";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task<Guid> InsertSessionAsync(NpgsqlConnection conn, Guid userId, DateTimeOffset createdAt)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO auth.sessions (user_id, created_at)
            VALUES ('{userId}', '{createdAt:O}')
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Simulates a request carrying the given caller's JWT (sub + session_id, plus the hook's
    /// <c>perm_v</c> claim when <paramref name="permVersion"/> is supplied — needed by
    /// <c>authorize_fresh()</c>).
    /// </summary>
    public static async Task SetJwtClaimsAsync(
        NpgsqlConnection conn, Guid userId, Guid sessionId, int? permVersion = null)
    {
        var permVClaim = permVersion is null ? string.Empty : $",\"perm_v\":{permVersion}";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET request.jwt.claims = '{{\"sub\":\"{userId}\",\"session_id\":\"{sessionId}\"{permVClaim}}}'";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Simulates no request context at all (migrations/seeds — <c>auth.uid()</c> is NULL).</summary>
    public static async Task ClearJwtClaimsAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET request.jwt.claims = ''";
        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================================
    // Role/permission read + write helpers (always run with claims cleared —
    // i.e. as a migration/config change, exempt from the self-change guard)
    // =========================================================================

    public static async Task<Guid> GetRoleIdAsync(NpgsqlConnection conn, string nameKey)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM roles WHERE name_key = '{nameKey}'";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task<Guid> GetPermissionIdAsync(NpgsqlConnection conn, string code)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM permissions WHERE code = '{code}'";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task AssignRoleByNameAsync(
        NpgsqlConnection conn, Guid userId, string roleNameKey, DateTimeOffset? expiresAt = null)
    {
        await ClearJwtClaimsAsync(conn);
        var expiresValue = expiresAt is null ? "NULL" : $"'{expiresAt:O}'";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO user_roles (user_id, role_id, expires_at)
            SELECT '{userId}', id, {expiresValue} FROM roles WHERE name_key = '{roleNameKey}'
            ON CONFLICT (user_id, role_id) DO NOTHING
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task RemoveRoleByNameAsync(NpgsqlConnection conn, Guid userId, string roleNameKey)
    {
        await ClearJwtClaimsAsync(conn);
        var roleId = await GetRoleIdAsync(conn, roleNameKey);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM user_roles WHERE user_id = '{userId}' AND role_id = '{roleId}'";
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<string>> GetUserRoleNameKeysAsync(NpgsqlConnection conn, Guid userId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT r.name_key FROM user_roles ur
            JOIN roles r ON r.id = ur.role_id
            WHERE ur.user_id = '{userId}'
            """;
        return await ReadStringColumnAsync(cmd);
    }

    public static async Task<List<string>> GetRolePermissionCodesAsync(NpgsqlConnection conn, string roleNameKey)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT p.code FROM role_permissions rp
            JOIN permissions p ON p.id = rp.permission_id
            JOIN roles r ON r.id = rp.role_id
            WHERE r.name_key = '{roleNameKey}'
            """;
        return await ReadStringColumnAsync(cmd);
    }

    public static async Task<bool> CallAuthorizeAsync(NpgsqlConnection conn, string permissionCode)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT public.authorize('{permissionCode}')";
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task<bool> CallAuthorizeFreshAsync(NpgsqlConnection conn, string permissionCode)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT public.authorize_fresh('{permissionCode}')";
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Invokes the custom access token hook exactly as Supabase Auth would and returns the raw event JSON.</summary>
    public static async Task<string> CallAccessTokenHookAsync(NpgsqlConnection conn, string userId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT public.custom_access_token_hook(
                jsonb_build_object('user_id', '{userId}', 'claims', jsonb_build_object('role', 'authenticated')))::text
            """;
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>The user's current perm_version; 1 when no <c>user_access_meta</c> row exists yet.</summary>
    public static async Task<int> GetPermVersionAsync(NpgsqlConnection conn, Guid userId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE((SELECT perm_version FROM user_access_meta WHERE user_id = '{userId}'), 1)";
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task<List<string>> GetAuditActionsForUserAsync(NpgsqlConnection conn, Guid subjectUserId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT action FROM access_audit WHERE subject_user_id = '{subjectUserId}' ORDER BY id";
        return await ReadStringColumnAsync(cmd);
    }

    public static async Task<List<string>> CallGetMyPermissionsAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM public.get_my_permissions()";
        return await ReadStringColumnAsync(cmd);
    }

    private static async Task<List<string>> ReadStringColumnAsync(NpgsqlCommand cmd)
    {
        var values = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));
        return values;
    }

    // =========================================================================
    // Migrations 0007 + 0009 + 0011 — schema and permission-reader functions in
    // their cumulative post-0011 form
    // =========================================================================

    private const string SchemaAndFunctionsSql = """
        CREATE TABLE roles (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name_key TEXT NOT NULL UNIQUE,
            is_system BOOLEAN NOT NULL DEFAULT false,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE permissions (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            code TEXT NOT NULL UNIQUE,
            module TEXT NOT NULL
        );

        CREATE TABLE role_permissions (
            role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
            permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
            PRIMARY KEY (role_id, permission_id)
        );

        CREATE TABLE user_roles (
            user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
            role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
            assigned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            granted_by UUID NULL,
            expires_at TIMESTAMPTZ NULL,
            scope_type TEXT NULL,
            scope_id UUID NULL,
            PRIMARY KEY (user_id, role_id),
            CONSTRAINT user_roles_scope_pair_chk CHECK ((scope_type IS NULL) = (scope_id IS NULL))
        );

        CREATE INDEX idx_user_roles_user_id ON user_roles(user_id);
        CREATE INDEX idx_role_permissions_role_id ON role_permissions(role_id);

        CREATE OR REPLACE FUNCTION public.authorize(requested_permission TEXT)
        RETURNS BOOLEAN LANGUAGE plpgsql SECURITY DEFINER STABLE
        SET search_path = public AS $$
        BEGIN
            IF auth.uid() IS NULL THEN RETURN false; END IF;

            RETURN EXISTS (
                SELECT 1
                FROM user_roles ur
                JOIN role_permissions rp ON rp.role_id = ur.role_id
                JOIN permissions p ON p.id = rp.permission_id
                WHERE ur.user_id = auth.uid()
                  AND p.code = requested_permission
                  AND (ur.expires_at IS NULL OR ur.expires_at > now())
            );
        END $$;

        CREATE OR REPLACE FUNCTION public.get_my_permissions()
        RETURNS SETOF TEXT LANGUAGE sql SECURITY DEFINER STABLE
        SET search_path = public AS $$
            SELECT p.code FROM user_roles ur
            JOIN role_permissions rp ON rp.role_id = ur.role_id
            JOIN permissions p ON p.id = rp.permission_id
            WHERE ur.user_id = auth.uid()
              AND (ur.expires_at IS NULL OR ur.expires_at > now());
        $$;

        GRANT EXECUTE ON FUNCTION public.authorize(TEXT) TO PUBLIC;
        GRANT EXECUTE ON FUNCTION public.get_my_permissions() TO PUBLIC;
        """;

    private const string TriggersSql = """
        CREATE OR REPLACE FUNCTION public.assign_customer_role()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        BEGIN
            INSERT INTO public.user_roles (user_id, role_id)
            SELECT NEW.id, r.id FROM public.roles r WHERE r.name_key = 'Customer'
            ON CONFLICT (user_id, role_id) DO NOTHING;
            RETURN NEW;
        END $$;

        CREATE TRIGGER on_auth_user_created
            AFTER INSERT ON auth.users
            FOR EACH ROW EXECUTE FUNCTION public.assign_customer_role();

        CREATE OR REPLACE FUNCTION public.guard_system_role_immutable()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        BEGIN
            IF OLD.is_system THEN
                RAISE EXCEPTION 'System role "%" cannot be modified or deleted.', OLD.name_key
                    USING ERRCODE = 'insufficient_privilege';
            END IF;
            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER roles_system_protection
            BEFORE UPDATE OR DELETE ON public.roles
            FOR EACH ROW EXECUTE FUNCTION public.guard_system_role_immutable();

        CREATE OR REPLACE FUNCTION public.guard_user_roles_self_change()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        DECLARE
            v_target_user_id UUID := COALESCE(NEW.user_id, OLD.user_id);
        BEGIN
            IF auth.uid() IS NOT NULL AND auth.uid() = v_target_user_id THEN
                RAISE EXCEPTION 'You cannot change your own role assignment.'
                    USING ERRCODE = 'insufficient_privilege';
            END IF;
            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER user_roles_self_change_guard
            BEFORE INSERT OR UPDATE OR DELETE ON public.user_roles
            FOR EACH ROW EXECUTE FUNCTION public.guard_user_roles_self_change();

        CREATE OR REPLACE FUNCTION public.guard_super_admin_floor_on_user_roles()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        DECLARE
            v_super_admin_role_id UUID;
            v_remaining_holders INTEGER;
        BEGIN
            SELECT id INTO v_super_admin_role_id FROM public.roles WHERE name_key = 'SuperAdmin';

            IF OLD.role_id IS DISTINCT FROM v_super_admin_role_id THEN
                RETURN COALESCE(NEW, OLD);
            END IF;

            IF TG_OP = 'UPDATE' AND NEW.role_id = v_super_admin_role_id THEN
                RETURN NEW;
            END IF;

            SELECT COUNT(*) INTO v_remaining_holders
            FROM public.user_roles
            WHERE role_id = v_super_admin_role_id AND user_id <> OLD.user_id;

            IF v_remaining_holders = 0 THEN
                RAISE EXCEPTION 'Cannot remove the last Super Admin.'
                    USING ERRCODE = 'insufficient_privilege';
            END IF;

            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER user_roles_super_admin_floor
            BEFORE UPDATE OR DELETE ON public.user_roles
            FOR EACH ROW EXECUTE FUNCTION public.guard_super_admin_floor_on_user_roles();

        CREATE OR REPLACE FUNCTION public.guard_super_admin_floor_on_role_permissions()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        DECLARE
            v_super_admin_role_id UUID;
            v_remaining_permissions INTEGER;
        BEGIN
            SELECT id INTO v_super_admin_role_id FROM public.roles WHERE name_key = 'SuperAdmin';

            IF OLD.role_id IS DISTINCT FROM v_super_admin_role_id THEN
                RETURN COALESCE(NEW, OLD);
            END IF;

            IF TG_OP = 'UPDATE' AND NEW.role_id = v_super_admin_role_id THEN
                RETURN NEW;
            END IF;

            SELECT COUNT(*) INTO v_remaining_permissions
            FROM public.role_permissions
            WHERE role_id = v_super_admin_role_id
              AND permission_id <> OLD.permission_id;

            IF v_remaining_permissions = 0 THEN
                RAISE EXCEPTION 'Cannot strip the last permission from the Super Admin role.'
                    USING ERRCODE = 'insufficient_privilege';
            END IF;

            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER role_permissions_super_admin_floor
            BEFORE UPDATE OR DELETE ON public.role_permissions
            FOR EACH ROW EXECUTE FUNCTION public.guard_super_admin_floor_on_role_permissions();
        """;

    // =========================================================================
    // Migration 0009 — user_access_meta (perm_version) + access_audit + triggers
    // =========================================================================

    private const string AccessMetaAndAuditSql = """
        CREATE TABLE public.user_access_meta (
            user_id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
            perm_version INTEGER NOT NULL DEFAULT 1,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        ALTER TABLE public.user_access_meta ENABLE ROW LEVEL SECURITY;

        CREATE POLICY "user_access_meta_select_own" ON public.user_access_meta
            FOR SELECT USING (user_id = (SELECT auth.uid()));

        CREATE OR REPLACE FUNCTION public.bump_perm_version(target_user_id UUID)
        RETURNS VOID LANGUAGE sql SECURITY DEFINER
        SET search_path = public AS $$
            INSERT INTO public.user_access_meta (user_id, perm_version)
            VALUES (target_user_id, 2)
            ON CONFLICT (user_id) DO UPDATE
                SET perm_version = user_access_meta.perm_version + 1,
                    updated_at = now();
        $$;

        CREATE OR REPLACE FUNCTION public.bump_perm_version_on_user_roles()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        BEGIN
            PERFORM public.bump_perm_version(COALESCE(NEW.user_id, OLD.user_id));
            IF TG_OP = 'UPDATE' AND NEW.user_id IS DISTINCT FROM OLD.user_id THEN
                PERFORM public.bump_perm_version(OLD.user_id);
            END IF;
            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER user_roles_bump_perm_version
            AFTER INSERT OR UPDATE OR DELETE ON public.user_roles
            FOR EACH ROW EXECUTE FUNCTION public.bump_perm_version_on_user_roles();

        CREATE OR REPLACE FUNCTION public.bump_perm_version_on_role_permissions()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        BEGIN
            PERFORM public.bump_perm_version(ur.user_id)
            FROM public.user_roles ur
            WHERE ur.role_id = COALESCE(NEW.role_id, OLD.role_id);
            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER role_permissions_bump_perm_version
            AFTER INSERT OR UPDATE OR DELETE ON public.role_permissions
            FOR EACH ROW EXECUTE FUNCTION public.bump_perm_version_on_role_permissions();

        CREATE TABLE public.access_audit (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            actor_id UUID NULL,
            action TEXT NOT NULL,
            subject_user_id UUID NULL,
            role_id UUID NULL,
            permission_id UUID NULL,
            occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            detail JSONB NULL
        );

        CREATE INDEX idx_access_audit_subject ON public.access_audit(subject_user_id, occurred_at);

        ALTER TABLE public.access_audit ENABLE ROW LEVEL SECURITY;

        CREATE POLICY "access_audit_select_role_admins" ON public.access_audit
            FOR SELECT USING ((SELECT public.authorize('roles.view')));

        CREATE OR REPLACE FUNCTION public.audit_user_roles_change()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        BEGIN
            INSERT INTO public.access_audit (actor_id, action, subject_user_id, role_id, detail)
            VALUES (
                auth.uid(),
                CASE TG_OP
                    WHEN 'INSERT' THEN 'user_role.granted'
                    WHEN 'DELETE' THEN 'user_role.revoked'
                    ELSE 'user_role.updated'
                END,
                COALESCE(NEW.user_id, OLD.user_id),
                COALESCE(NEW.role_id, OLD.role_id),
                jsonb_build_object(
                    'granted_by', COALESCE(NEW.granted_by, OLD.granted_by),
                    'expires_at', COALESCE(NEW.expires_at, OLD.expires_at),
                    'scope_type', COALESCE(NEW.scope_type, OLD.scope_type),
                    'scope_id',   COALESCE(NEW.scope_id, OLD.scope_id)
                )
            );
            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER user_roles_audit
            AFTER INSERT OR UPDATE OR DELETE ON public.user_roles
            FOR EACH ROW EXECUTE FUNCTION public.audit_user_roles_change();

        CREATE OR REPLACE FUNCTION public.audit_role_permissions_change()
        RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = public AS $$
        BEGIN
            INSERT INTO public.access_audit (actor_id, action, role_id, permission_id)
            VALUES (
                auth.uid(),
                CASE TG_OP
                    WHEN 'INSERT' THEN 'role_permission.granted'
                    WHEN 'DELETE' THEN 'role_permission.revoked'
                    ELSE 'role_permission.updated'
                END,
                COALESCE(NEW.role_id, OLD.role_id),
                COALESCE(NEW.permission_id, OLD.permission_id)
            );
            RETURN COALESCE(NEW, OLD);
        END $$;

        CREATE TRIGGER role_permissions_audit
            AFTER INSERT OR UPDATE OR DELETE ON public.role_permissions
            FOR EACH ROW EXECUTE FUNCTION public.audit_role_permissions_change();
        """;

    // =========================================================================
    // Migrations 0010 + 0011 — custom access token hook and authorize_fresh()
    // (the supabase_auth_admin grants/policies are omitted: that role only
    // exists on a real Supabase instance; tests invoke the hook as the owner)
    // =========================================================================

    private const string HookSql = """
        CREATE OR REPLACE FUNCTION public.custom_access_token_hook(event JSONB)
        RETURNS JSONB LANGUAGE plpgsql STABLE
        SET search_path = public AS $$
        DECLARE
            v_user_id UUID;
            v_claims JSONB := COALESCE(event -> 'claims', '{}'::jsonb);
            v_roles JSONB;
            v_perms JSONB;
            v_perm_v INTEGER;
        BEGIN
            v_user_id := (event ->> 'user_id')::uuid;

            SELECT COALESCE(jsonb_agg(DISTINCT r.name_key), '[]'::jsonb)
            INTO v_roles
            FROM user_roles ur
            JOIN roles r ON r.id = ur.role_id
            WHERE ur.user_id = v_user_id
              AND (ur.expires_at IS NULL OR ur.expires_at > now());

            SELECT COALESCE(jsonb_agg(DISTINCT p.code), '[]'::jsonb)
            INTO v_perms
            FROM user_roles ur
            JOIN role_permissions rp ON rp.role_id = ur.role_id
            JOIN permissions p ON p.id = rp.permission_id
            WHERE ur.user_id = v_user_id
              AND (ur.expires_at IS NULL OR ur.expires_at > now());

            SELECT COALESCE(
                (SELECT uam.perm_version FROM user_access_meta uam WHERE uam.user_id = v_user_id),
                1)
            INTO v_perm_v;

            v_claims := v_claims || jsonb_build_object(
                'app_roles', v_roles,
                'perms', v_perms,
                'perm_v', v_perm_v);

            RETURN jsonb_set(event, '{claims}', v_claims);
        EXCEPTION WHEN OTHERS THEN
            RETURN jsonb_set(event, '{claims}',
                COALESCE(event -> 'claims', '{}'::jsonb) || jsonb_build_object(
                    'app_roles', '[]'::jsonb,
                    'perms', '[]'::jsonb,
                    'perm_v', 0));
        END $$;

        CREATE OR REPLACE FUNCTION public.authorize_fresh(requested_permission TEXT)
        RETURNS BOOLEAN LANGUAGE plpgsql SECURITY DEFINER STABLE
        SET search_path = public AS $$
        BEGIN
            IF NOT public.authorize(requested_permission) THEN RETURN false; END IF;

            RETURN COALESCE((auth.jwt() ->> 'perm_v')::int, 0) >=
                   COALESCE((SELECT uam.perm_version
                             FROM public.user_access_meta uam
                             WHERE uam.user_id = auth.uid()), 1);
        END $$;

        GRANT EXECUTE ON FUNCTION public.authorize_fresh(TEXT) TO PUBLIC;
        """;

    private const string RlsSql = """
        ALTER TABLE roles ENABLE ROW LEVEL SECURITY;
        ALTER TABLE permissions ENABLE ROW LEVEL SECURITY;
        ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;
        ALTER TABLE user_roles ENABLE ROW LEVEL SECURITY;

        CREATE POLICY "roles_select_authenticated" ON roles
            FOR SELECT USING ((SELECT auth.uid()) IS NOT NULL);
        CREATE POLICY "permissions_select_authenticated" ON permissions
            FOR SELECT USING ((SELECT auth.uid()) IS NOT NULL);
        CREATE POLICY "role_permissions_select_authenticated" ON role_permissions
            FOR SELECT USING ((SELECT auth.uid()) IS NOT NULL);
        CREATE POLICY "user_roles_select_own" ON user_roles
            FOR SELECT USING (user_id = (SELECT auth.uid()) OR (SELECT public.authorize('admin_users.view')));
        """;

    private const string SeedSql = """
        INSERT INTO roles (name_key, is_system) VALUES
            ('Customer', true), ('Support', true), ('Admin', true), ('SuperAdmin', true);

        INSERT INTO permissions (code, module) VALUES
            ('products.view', 'products'), ('products.create', 'products'), ('products.edit', 'products'), ('products.delete', 'products'),
            ('categories.view', 'categories'), ('categories.create', 'categories'), ('categories.edit', 'categories'), ('categories.delete', 'categories'),
            ('orders.view', 'orders'), ('orders.create', 'orders'), ('orders.edit', 'orders'), ('orders.delete', 'orders'), ('orders.refund', 'orders'),
            ('customers.view', 'customers'), ('customers.create', 'customers'), ('customers.edit', 'customers'), ('customers.delete', 'customers'), ('customers.export', 'customers'),
            ('coupons.view', 'coupons'), ('coupons.create', 'coupons'), ('coupons.edit', 'coupons'), ('coupons.delete', 'coupons'),
            ('promotions.view', 'promotions'), ('promotions.create', 'promotions'), ('promotions.edit', 'promotions'), ('promotions.delete', 'promotions'),
            ('reports.view', 'reports'), ('reports.create', 'reports'), ('reports.edit', 'reports'), ('reports.delete', 'reports'),
            ('settings.view', 'settings'), ('settings.create', 'settings'), ('settings.edit', 'settings'), ('settings.delete', 'settings'),
            ('admin_users.view', 'admin_users'), ('admin_users.create', 'admin_users'), ('admin_users.edit', 'admin_users'), ('admin_users.delete', 'admin_users'),
            ('roles.view', 'roles'), ('roles.create', 'roles'), ('roles.edit', 'roles'), ('roles.delete', 'roles');

        -- Role_Customer: zero admin-area permissions (AC-1) — no rows needed.

        INSERT INTO role_permissions (role_id, permission_id)
        SELECT r.id, p.id FROM roles r
        JOIN permissions p ON p.code IN ('orders.view', 'orders.edit', 'customers.view')
        WHERE r.name_key = 'Support';

        INSERT INTO role_permissions (role_id, permission_id)
        SELECT r.id, p.id FROM roles r
        JOIN permissions p ON p.module IN ('products', 'categories', 'orders', 'customers', 'coupons', 'promotions', 'reports')
        WHERE r.name_key = 'Admin';

        INSERT INTO role_permissions (role_id, permission_id)
        SELECT r.id, p.id FROM roles r
        CROSS JOIN permissions p
        WHERE r.name_key = 'SuperAdmin';
        """;
}
