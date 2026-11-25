-- ============================================================================
-- 0009_rbac_audit_and_perm_version
--
-- Phase 1 of the JWT-claims RBAC migration (additive only — no behavior
-- change for existing code paths):
--
--   1. `user_roles` gains grant metadata (granted_by), optional expiry
--      (expires_at, enabling just-in-time/time-bound assignments), and
--      dormant scope columns (scope_type/scope_id) so store/tenant-scoped
--      role assignments never require a schema redesign. `assigned_at`
--      already exists from 0007 and serves as the grant timestamp.
--   2. `user_access_meta.perm_version` — a per-user counter bumped by
--      trigger on every change that can alter the user's effective
--      permission set. The access-token hook (0010) stamps it into the JWT
--      as `perm_v`; `authorize_fresh()` (0011) compares the claim against
--      this table so high-risk policies can reject stale tokens without
--      re-reading permissions on every ordinary request.
--   3. `access_audit` — append-only who-granted-what-to-whom-when trail,
--      written by AFTER triggers on user_roles / role_permissions. Actor and
--      subject ids are stored WITHOUT foreign keys on purpose: deleting a
--      user must never mutate or cascade into the audit history.
--   4. `authorize()` / `get_my_permissions()` now honor `expires_at` so an
--      expired assignment stops granting on the next check.
--   5. Follow-up to 0007_create_rbac: drops the redundant "Role_" prefix from
--      roles.name_key (Role_Customer -> Customer, etc.) to match the
--      corresponding Strings.resx keys, which were renamed the same way.
--      Folded into this file (rather than its own 0009) because both were
--      authored in the same commit and originally collided on version 0009;
--      0010 onward is already applied elsewhere, so it can't be renumbered.
--
-- Companion plan: JWT migration plan (chat), Phase 1;
--                 .specs/role-based-access-control/plan.md §10 (section 5).
-- ============================================================================

-- ============================================================================
-- 1. user_roles — grant metadata, expiry, dormant scope
-- ============================================================================
ALTER TABLE public.user_roles
    ADD COLUMN granted_by UUID NULL,
    ADD COLUMN expires_at TIMESTAMPTZ NULL,
    ADD COLUMN scope_type TEXT NULL,
    ADD COLUMN scope_id UUID NULL,
    ADD CONSTRAINT user_roles_scope_pair_chk
        CHECK ((scope_type IS NULL) = (scope_id IS NULL));

COMMENT ON COLUMN public.user_roles.granted_by IS
    'auth.users.id of the granting actor; NULL for system grants (signup trigger, migrations). No FK: audit-style column, must survive actor deletion.';
COMMENT ON COLUMN public.user_roles.expires_at IS
    'When set, the assignment stops granting permissions after this instant (checked by authorize()/get_my_permissions()/the token hook).';
COMMENT ON COLUMN public.user_roles.scope_type IS
    'Dormant this release. Reserved for scoped assignments (e.g. ''store''); NULL = global.';

-- ============================================================================
-- 2. user_access_meta — per-user permission version
-- ============================================================================
CREATE TABLE public.user_access_meta (
    user_id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    perm_version INTEGER NOT NULL DEFAULT 1,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE public.user_access_meta ENABLE ROW LEVEL SECURITY;

-- Users may read their own version (lets a client detect staleness); no
-- client write path exists — writes happen only via the trigger below.
CREATE POLICY "user_access_meta_select_own" ON public.user_access_meta
    FOR SELECT USING (user_id = (SELECT auth.uid()));

-- A missing row means version 1 (the hook and authorize_fresh() both
-- COALESCE to 1), so no backfill is needed; the first bump inserts at 2.
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

-- A role_permissions change affects every holder of that role.
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

-- ============================================================================
-- 3. access_audit — append-only RBAC change trail
-- ============================================================================
CREATE TABLE public.access_audit (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_id UUID NULL,          -- auth.uid(); NULL for migrations/system triggers
    action TEXT NOT NULL,        -- 'user_role.granted' | 'user_role.revoked' | 'user_role.updated'
                                 -- | 'role_permission.granted' | 'role_permission.revoked' | 'role_permission.updated'
    subject_user_id UUID NULL,   -- the user whose access changed (user_roles events)
    role_id UUID NULL,
    permission_id UUID NULL,     -- role_permissions events only
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    detail JSONB NULL
);

CREATE INDEX idx_access_audit_subject ON public.access_audit(subject_user_id, occurred_at);

ALTER TABLE public.access_audit ENABLE ROW LEVEL SECURITY;

-- Readable by role administrators only; no client INSERT/UPDATE/DELETE policy
-- exists — rows are written exclusively by the SECURITY DEFINER triggers
-- below (which bypass RLS as table owner), making the trail append-only from
-- every client-facing surface.
CREATE POLICY "access_audit_select_role_admins" ON public.access_audit
    FOR SELECT USING (public.authorize('roles.view'));

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

-- ============================================================================
-- 4. Honor expires_at in the existing permission readers
-- ============================================================================
CREATE OR REPLACE FUNCTION public.authorize(requested_permission TEXT)
RETURNS BOOLEAN LANGUAGE plpgsql SECURITY DEFINER STABLE
SET search_path = public AS $$
BEGIN
    IF auth.uid() IS NULL THEN RETURN false; END IF;

    -- Admin-area permissions: enforce the 8-hour admin session constraint
    -- (all catalogue permissions are admin-area this release).
    -- NOTE: removed by 0011 — superseded by the access-token TTL.
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

-- ============================================================================
-- Lock-down (0008 lesson: Supabase default privileges grant EXECUTE on every
-- new public function to anon/authenticated/PUBLIC — none of these functions
-- has a legitimate direct-RPC use case).
-- ============================================================================
REVOKE EXECUTE ON FUNCTION public.bump_perm_version(UUID) FROM PUBLIC, anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.bump_perm_version_on_user_roles() FROM PUBLIC, anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.bump_perm_version_on_role_permissions() FROM PUBLIC, anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.audit_user_roles_change() FROM PUBLIC, anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.audit_role_permissions_change() FROM PUBLIC, anon, authenticated;

-- ============================================================================
-- 5. Rename role name_key values (drops the "Role_" prefix)
-- ============================================================================

-- roles_system_protection (0007) blocks UPDATE on is_system rows, which
-- includes all four seeded roles — briefly disable it for the rename.
ALTER TABLE public.roles DISABLE TRIGGER roles_system_protection;

UPDATE public.roles SET name_key = 'Customer' WHERE name_key = 'Role_Customer';
UPDATE public.roles SET name_key = 'Support' WHERE name_key = 'Role_Support';
UPDATE public.roles SET name_key = 'Admin' WHERE name_key = 'Role_Admin';
UPDATE public.roles SET name_key = 'SuperAdmin' WHERE name_key = 'Role_SuperAdmin';

ALTER TABLE public.roles ENABLE TRIGGER roles_system_protection;

-- Re-create the guard/trigger functions that had the old name_key literals
-- baked into their function bodies at 0007 creation time.
CREATE OR REPLACE FUNCTION public.assign_customer_role()
RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER
SET search_path = public AS $$
BEGIN
    INSERT INTO public.user_roles (user_id, role_id)
    SELECT NEW.id, r.id FROM public.roles r WHERE r.name_key = 'Customer'
    ON CONFLICT (user_id, role_id) DO NOTHING;
    RETURN NEW;
END $$;

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
