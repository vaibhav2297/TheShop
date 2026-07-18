-- ============================================================================
-- 0009_rename_role_name_keys
--
-- Follow-up to 0007_create_rbac: drops the redundant "Role_" prefix from
-- roles.name_key (Role_Customer -> Customer, Role_Support -> Support,
-- Role_Admin -> Admin, Role_SuperAdmin -> SuperAdmin) to match the
-- corresponding Strings.resx keys, which were renamed the same way.
--
-- Companion plan: .specs/role-based-access-control/plan.md §10
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
