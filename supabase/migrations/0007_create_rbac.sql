-- ============================================================================
-- 0007_create_rbac
--
-- Builds the database-backed RBAC authorization mechanism: role/permission
-- tables, the single `public.authorize(text)` RLS predicate, auto-Customer
-- assignment, and the guard triggers that enforce the Super-Admin floor,
-- system-role immutability, and self-role-change refusal (FR-1/FR-2/FR-5/
-- FR-7/FR-8/FR-10/FR-11). RBAC configuration is read-only this release —
-- provisioned entirely by the seed data below; no client write policy exists
-- on any of the four RBAC tables.
--
-- Critical correction to existing code: migrations 0001 and 0004 authorized
-- admin writes via `auth.jwt() ->> 'role' = 'admin'` — a role-name check
-- frozen into the JWT at login. This violates FR-5 (permission-based, not
-- role-based) and FR-11 (revocation must be effective on the very next
-- action; JWT claims stay stale until re-login). This migration replaces
-- both clauses with `public.authorize(...)` calls that always read fresh
-- from the database.
--
-- Session-dependency probe (Phase 3, first task): verified live against this
-- project via Supabase MCP prior to writing `authorize()` below —
-- `auth.sessions.created_at` is queryable from a SECURITY DEFINER function
-- despite `authenticator` having no direct table privilege on `auth.sessions`
-- (confirmed by a throwaway probe function; owner-based SECURITY DEFINER
-- access succeeded). The designed `public.session_starts` fallback is
-- therefore NOT needed this release — `authorize()` reads
-- `auth.sessions.created_at` directly via the JWT `session_id` claim.
--
-- Companion plan: .specs/role-based-access-control/plan.md §10
-- ============================================================================

-- ============================================================================
-- Schema
-- ============================================================================
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name_key TEXT NOT NULL UNIQUE,          -- resource key; localized in Web
    is_system BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code TEXT NOT NULL UNIQUE,              -- 'products.view', 'orders.refund', ...
    module TEXT NOT NULL                    -- 'products', 'orders', ...
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
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX idx_user_roles_user_id ON user_roles(user_id);
CREATE INDEX idx_role_permissions_role_id ON role_permissions(role_id);
-- permissions(code) is already covered by the UNIQUE constraint above.

-- ============================================================================
-- Authorization function — the single RLS predicate (FR-5, FR-7, FR-11)
-- ============================================================================
CREATE OR REPLACE FUNCTION public.authorize(requested_permission TEXT)
RETURNS BOOLEAN LANGUAGE plpgsql SECURITY DEFINER STABLE
SET search_path = public AS $$
BEGIN
    IF auth.uid() IS NULL THEN RETURN false; END IF;

    -- Admin-area permissions: enforce the 8-hour admin session constraint
    -- (all catalogue permissions are admin-area this release).
    IF NOT EXISTS (
        SELECT 1 FROM auth.sessions s
        WHERE s.id = (auth.jwt() ->> 'session_id')::uuid
          AND s.created_at > now() - interval '8 hours'
    ) THEN RETURN false; END IF;

    RETURN EXISTS (
        SELECT 1
        FROM user_roles ur
        JOIN role_permissions rp ON rp.role_id = ur.role_id
        JOIN permissions p ON p.id = rp.permission_id
        WHERE ur.user_id = auth.uid() AND p.code = requested_permission
    );
END $$;

CREATE OR REPLACE FUNCTION public.get_my_permissions()
RETURNS SETOF TEXT LANGUAGE sql SECURITY DEFINER STABLE
SET search_path = public AS $$
    SELECT p.code FROM user_roles ur
    JOIN role_permissions rp ON rp.role_id = ur.role_id
    JOIN permissions p ON p.id = rp.permission_id
    WHERE ur.user_id = auth.uid();
$$;

-- Explicit grants (mirrors the customer_exists() convention in 0001):
--   • anon so an unauthenticated caller resolves to an empty set instead of
--     a permission-denied error (IPermissionService contract).
--   • authenticated for signed-in customers/staff.
GRANT EXECUTE ON FUNCTION public.authorize(TEXT) TO anon, authenticated;
GRANT EXECUTE ON FUNCTION public.get_my_permissions() TO anon, authenticated;

-- ============================================================================
-- Triggers
-- ============================================================================

-- 1. Auto-Customer at account creation (FR-1) — fires for every registration
--    route, atomically with the auth.users row. VerifySignUpOtpHandler needs
--    no change (design decision 3).
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

-- 2. System-role protection (FR-2) — BEFORE UPDATE/DELETE on roles where
--    is_system. Every future admin API and every seed/config migration hits
--    this same choke point.
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

-- 3. Self-change guard (FR-10) — BEFORE INSERT/UPDATE/DELETE on user_roles
--    where auth.uid() = user_id. NULL auth.uid() (migrations/seeds, and the
--    auto-Customer trigger above, which runs outside a request context) is
--    exempt, but still subject to the Super-Admin floor guard below.
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

-- 4. Super-Admin floor (FR-10/AC-9) — BEFORE UPDATE/DELETE on user_roles and
--    role_permissions. Blocks a change that would leave zero users holding
--    the Super Admin role, or that would strip the Super Admin role down to
--    zero permissions (functionally the same failure mode: nobody left who
--    can manage RBAC). INSERT is intentionally NOT gated — granting access
--    can never create the zero-Super-Admin condition.
CREATE OR REPLACE FUNCTION public.guard_super_admin_floor_on_user_roles()
RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
SET search_path = public AS $$
DECLARE
    v_super_admin_role_id UUID;
    v_remaining_holders INTEGER;
BEGIN
    SELECT id INTO v_super_admin_role_id FROM public.roles WHERE name_key = 'SuperAdmin';

    -- Not touching the Super Admin role at all — nothing to guard.
    IF OLD.role_id IS DISTINCT FROM v_super_admin_role_id THEN
        RETURN COALESCE(NEW, OLD);
    END IF;

    -- An UPDATE that keeps the same user assigned to Super Admin is a no-op
    -- for this guard (e.g. touching assigned_at).
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

-- ============================================================================
-- Row-Level Security — read-only config: SELECT only; NO insert/update/
-- delete policies on any RBAC table (FR-8). Writes only ever happen via
-- migrations/seeds, which run outside RLS as the migration role.
-- ============================================================================
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
    FOR SELECT USING (user_id = (SELECT auth.uid()) OR public.authorize('admin_users.view'));

-- ============================================================================
-- Seed data — 4 system roles + full permission catalogue (mirrors
-- TheShop.Domain.ValueObjects.PermissionCatalogue.All, FR-3) + least-privilege
-- grants per spec Constraints.
-- ============================================================================
INSERT INTO roles (name_key, is_system) VALUES
    ('Customer', true),
    ('Support', true),
    ('Admin', true),
    ('SuperAdmin', true);

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

-- Role_Support: exactly orders.view, orders.edit, customers.view. No delete,
-- no refund — refunds are sensitive and stay Admin+ (ratified by user).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('orders.view', 'orders.edit', 'customers.view')
WHERE r.name_key = 'Support';

-- Role_Admin: every module except Settings, Admin Users, Roles.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.module IN ('products', 'categories', 'orders', 'customers', 'coupons', 'promotions', 'reports')
WHERE r.name_key = 'Admin';

-- Role_SuperAdmin: everything.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name_key = 'SuperAdmin';

-- Backfill: every existing auth.users row gets Customer (the trigger above
-- only fires for future signups).
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM auth.users u
CROSS JOIN roles r
WHERE r.name_key = 'Customer'
ON CONFLICT (user_id, role_id) DO NOTHING;

-- ----------------------------------------------------------------------------
-- First Super Admin assignment — store-setup step, not automated by this
-- migration (no target user is known at migration time). After identifying
-- the first Super Admin's auth.users.id, run once, by hand:
--
--   INSERT INTO user_roles (user_id, role_id)
--   SELECT '<auth-user-uuid>', id FROM roles WHERE name_key = 'SuperAdmin';
--
-- No JWT refresh/re-login is needed afterward — authorize() reads user_roles
-- directly on the very next request (FR-11), unlike the old JWT-role scheme.
-- ----------------------------------------------------------------------------

-- ============================================================================
-- Migrate existing policies off JWT role-name checks (FR-5 / FR-11)
-- ============================================================================

-- customers (0001): ... OR (SELECT auth.jwt() ->> 'role') = 'admin'
--                    → ... OR public.authorize('customers.view')
DROP POLICY IF EXISTS "customers_select" ON customers;
CREATE POLICY "customers_select" ON customers
    FOR SELECT USING (
        (SELECT auth.uid()) = id
        OR public.authorize('customers.view')
    );

-- product-images (0004): role = 'admin' clauses
--                    → public.authorize('products.view' / 'products.create' / 'products.edit' / 'products.delete')
DROP POLICY IF EXISTS "product_images_admin_read" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_insert" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_update" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_delete" ON storage.objects;

CREATE POLICY "product_images_admin_read" ON storage.objects
    FOR SELECT
    USING (bucket_id = 'product-images' AND public.authorize('products.view'));

CREATE POLICY "product_images_admin_insert" ON storage.objects
    FOR INSERT
    WITH CHECK (bucket_id = 'product-images' AND public.authorize('products.create'));

CREATE POLICY "product_images_admin_update" ON storage.objects
    FOR UPDATE
    USING (bucket_id = 'product-images' AND public.authorize('products.edit'))
    WITH CHECK (bucket_id = 'product-images' AND public.authorize('products.edit'));

CREATE POLICY "product_images_admin_delete" ON storage.objects
    FOR DELETE
    USING (bucket_id = 'product-images' AND public.authorize('products.delete'));
