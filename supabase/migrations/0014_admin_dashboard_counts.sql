-- ============================================================================
-- 0014_admin_dashboard_counts
--
-- Admin console (.specs/admin-console/plan.md §10). Per-module record count for
-- the /admin dashboard. SECURITY DEFINER so it counts EVERY row regardless of
-- storefront RLS/status (spec FR-3/RULE-3) and can read auth.users; it
-- re-checks the module's authorize() gate itself so hiding a card in the UI is
-- never the only protection.
-- ============================================================================

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
        WHEN 'users' THEN                                            -- all accounts (customers + staff)
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
