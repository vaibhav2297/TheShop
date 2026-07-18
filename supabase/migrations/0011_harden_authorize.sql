-- ============================================================================
-- 0011_harden_authorize
--
-- Phase 4 of the JWT-claims RBAC migration — database hardening:
--
--   1. `authorize()` drops the 8-hour `auth.sessions` age subquery. The
--      constraint is superseded by the access-token TTL: a token past its
--      TTL cannot authenticate at all, and session lifetime limits belong in
--      Supabase Auth session settings (timebox), not in every RLS check.
--   2. `authorize_fresh()` — the selective zero-staleness escape hatch. In
--      addition to `authorize()`, it requires the JWT's `perm_v` claim to be
--      at least the user's current `user_access_meta.perm_version`, so a
--      token minted BEFORE the user's last permission change is rejected.
--      Reserve it for high-risk policies (role management, refunds,
--      customer export); ordinary policies stay on `authorize()` and accept
--      TTL-bounded staleness. A token without a `perm_v` claim (minted
--      before the hook was enabled) always fails the freshness check —
--      fail closed.
--   3. Every RLS policy calling `authorize()` is rewritten to wrap the call
--      in a scalar subquery `(SELECT public.authorize(...))` so Postgres
--      evaluates it once per statement (InitPlan) instead of once per row.
--   4. `get_my_permissions()` loses its anon/authenticated grants — the
--      client reads permissions from token claims now, and nothing else
--      calls it. The function is kept (repointed to the same expiry-aware
--      query) for potential server-side/admin use.
-- ============================================================================

-- ============================================================================
-- 1. authorize() — permission check only; session-age constraint removed
-- ============================================================================
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

-- ============================================================================
-- 2. authorize_fresh() — permission check + token-freshness gate
-- ============================================================================
CREATE OR REPLACE FUNCTION public.authorize_fresh(requested_permission TEXT)
RETURNS BOOLEAN LANGUAGE plpgsql SECURITY DEFINER STABLE
SET search_path = public AS $$
BEGIN
    IF NOT public.authorize(requested_permission) THEN RETURN false; END IF;

    -- Missing/pre-hook claim coalesces to 0; missing meta row means version 1,
    -- so a claimless token can never pass (0 >= 1 is false) — fail closed.
    RETURN COALESCE((auth.jwt() ->> 'perm_v')::int, 0) >=
           COALESCE((SELECT uam.perm_version
                     FROM public.user_access_meta uam
                     WHERE uam.user_id = auth.uid()), 1);
END $$;

-- RLS predicates must be executable by the querying role (mirrors authorize()).
GRANT EXECUTE ON FUNCTION public.authorize_fresh(TEXT) TO anon, authenticated;

-- ============================================================================
-- 3. Wrap RLS call sites in (SELECT ...) — once per statement, not per row
-- ============================================================================
DROP POLICY IF EXISTS "user_roles_select_own" ON public.user_roles;
CREATE POLICY "user_roles_select_own" ON public.user_roles
    FOR SELECT USING (
        user_id = (SELECT auth.uid())
        OR (SELECT public.authorize('admin_users.view'))
    );

DROP POLICY IF EXISTS "customers_select" ON public.customers;
CREATE POLICY "customers_select" ON public.customers
    FOR SELECT USING (
        (SELECT auth.uid()) = id
        OR (SELECT public.authorize('customers.view'))
    );

DROP POLICY IF EXISTS "product_images_admin_read" ON storage.objects;
CREATE POLICY "product_images_admin_read" ON storage.objects
    FOR SELECT
    USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.view')));

DROP POLICY IF EXISTS "product_images_admin_insert" ON storage.objects;
CREATE POLICY "product_images_admin_insert" ON storage.objects
    FOR INSERT
    WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.create')));

DROP POLICY IF EXISTS "product_images_admin_update" ON storage.objects;
CREATE POLICY "product_images_admin_update" ON storage.objects
    FOR UPDATE
    USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')))
    WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')));

DROP POLICY IF EXISTS "product_images_admin_delete" ON storage.objects;
CREATE POLICY "product_images_admin_delete" ON storage.objects
    FOR DELETE
    USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.delete')));

DROP POLICY IF EXISTS "access_audit_select_role_admins" ON public.access_audit;
CREATE POLICY "access_audit_select_role_admins" ON public.access_audit
    FOR SELECT USING ((SELECT public.authorize('roles.view')));

-- ============================================================================
-- 4. get_my_permissions() — no client-facing callers remain
-- ============================================================================
REVOKE EXECUTE ON FUNCTION public.get_my_permissions() FROM PUBLIC, anon, authenticated;
