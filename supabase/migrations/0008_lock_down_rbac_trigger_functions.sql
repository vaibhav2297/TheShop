-- ============================================================================
-- 0008_lock_down_rbac_trigger_functions
--
-- Follow-up to 0007_create_rbac: Postgres grants EXECUTE to PUBLIC by default
-- on function creation, and Supabase additionally applies a schema-level
-- ALTER DEFAULT PRIVILEGES rule that grants EXECUTE on every new public-
-- schema function directly to `anon`, `authenticated`, and `service_role` —
-- independent of the PUBLIC pseudo-role. Both together made the RBAC
-- trigger/guard functions — assign_customer_role, guard_system_role_immutable,
-- guard_user_roles_self_change, guard_super_admin_floor_on_user_roles,
-- guard_super_admin_floor_on_role_permissions — callable directly via
-- PostgREST's `/rest/v1/rpc/...` surface (flagged by the Supabase advisor
-- lints anon_security_definer_function_executable /
-- authenticated_security_definer_function_executable). These functions exist
-- only to run as trigger callbacks and have no legitimate direct-RPC use
-- case, so both the PUBLIC and the explicit anon/authenticated grants are
-- revoked here. `authorize()` and `get_my_permissions()` keep their explicit
-- anon/authenticated grants from 0007 — those two ARE meant to be called
-- directly (IPermissionService, and the `authorize()` RLS predicate itself
-- must remain invokable by the querying role).
--
-- Companion plan: .specs/role-based-access-control/plan.md §10
-- ============================================================================

REVOKE EXECUTE ON FUNCTION public.assign_customer_role() FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION public.guard_system_role_immutable() FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION public.guard_user_roles_self_change() FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION public.guard_super_admin_floor_on_user_roles() FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION public.guard_super_admin_floor_on_role_permissions() FROM PUBLIC;

-- Supabase's ALTER DEFAULT PRIVILEGES rule grants these roles EXECUTE
-- explicitly (not merely via PUBLIC), so the PUBLIC revoke above is not
-- sufficient on its own — revoke the explicit grants too.
REVOKE EXECUTE ON FUNCTION public.assign_customer_role() FROM anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.guard_system_role_immutable() FROM anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.guard_user_roles_self_change() FROM anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.guard_super_admin_floor_on_user_roles() FROM anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.guard_super_admin_floor_on_role_permissions() FROM anon, authenticated;
