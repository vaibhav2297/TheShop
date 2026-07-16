-- ============================================================================
-- 0010_custom_access_token_hook
--
-- Phase 2 of the JWT-claims RBAC migration: the Custom Access Token Hook.
-- Supabase Auth invokes this function every time it mints an access token
-- (sign-in, OTP verification, and every silent refresh). It stamps three
-- app claims into the JWT:
--
--   app_roles : the user's role name_keys, e.g. ["Customer","Admin"]
--   perms     : the effective permission codes across all non-expired role
--               assignments, e.g. ["products.view","orders.refund", ...]
--   perm_v    : the user_access_meta.perm_version counter at mint time.
--               authorize_fresh() (0011) compares this claim against the
--               table so high-risk policies reject tokens minted before the
--               user's last permission change.
--
-- Revocation model: a role change is reflected in the next minted token —
-- i.e. within one access-token TTL (project default, uniform for all users
-- per design decision). Emergency revocation = revoke the user's sessions,
-- which kills the refresh path immediately. RLS (`authorize()`) remains the
-- authoritative boundary regardless of what any token claims.
--
-- FAIL-CLOSED: any unexpected error returns the event with EMPTY perms
-- rather than aborting — a database hiccup must never block token issuance
-- (users could not even sign in), but it may only ever remove access, never
-- grant it.
--
-- The hook runs as `supabase_auth_admin` (SECURITY INVOKER, per the Supabase
-- reference implementation), which is not exempt from RLS — hence the
-- explicit per-table SELECT policies below. EXECUTE is revoked from every
-- client-facing role; only Supabase Auth may call this function.
--
-- MANUAL STEP (not automatable via SQL) — enable the hook:
--   Hosted:  Dashboard → Authentication → Hooks (Beta) →
--            Custom Access Token → Postgres function →
--            public.custom_access_token_hook
--   Local:   supabase/config.toml:
--              [auth.hook.custom_access_token]
--              enabled = true
--              uri = "pg-functions://postgres/public/custom_access_token_hook"
-- ============================================================================

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
    -- Assigned in the body, not DECLARE: plpgsql exception handlers do not cover
    -- DECLARE-time failures, and a malformed user_id must fail closed, not abort issuance.
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
    -- Fail closed: token still mints, but with no app access claims.
    RETURN jsonb_set(event, '{claims}',
        COALESCE(event -> 'claims', '{}'::jsonb) || jsonb_build_object(
            'app_roles', '[]'::jsonb,
            'perms', '[]'::jsonb,
            'perm_v', 0));
END $$;

-- ============================================================================
-- Grants — Supabase Auth only
-- ============================================================================
GRANT USAGE ON SCHEMA public TO supabase_auth_admin;
GRANT EXECUTE ON FUNCTION public.custom_access_token_hook(JSONB) TO supabase_auth_admin;
REVOKE EXECUTE ON FUNCTION public.custom_access_token_hook(JSONB) FROM PUBLIC, anon, authenticated;

GRANT SELECT ON public.user_roles, public.roles, public.role_permissions,
                public.permissions, public.user_access_meta
    TO supabase_auth_admin;

-- The hook runs with auth.uid() = NULL, so the existing user-scoped RLS
-- policies would return zero rows. Give supabase_auth_admin its own read
-- policies (SELECT only — the hook never writes).
CREATE POLICY "user_roles_auth_admin_read" ON public.user_roles
    FOR SELECT TO supabase_auth_admin USING (true);
CREATE POLICY "roles_auth_admin_read" ON public.roles
    FOR SELECT TO supabase_auth_admin USING (true);
CREATE POLICY "role_permissions_auth_admin_read" ON public.role_permissions
    FOR SELECT TO supabase_auth_admin USING (true);
CREATE POLICY "permissions_auth_admin_read" ON public.permissions
    FOR SELECT TO supabase_auth_admin USING (true);
CREATE POLICY "user_access_meta_auth_admin_read" ON public.user_access_meta
    FOR SELECT TO supabase_auth_admin USING (true);
