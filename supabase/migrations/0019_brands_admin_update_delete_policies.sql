-- ============================================================================
-- 0019_brands_admin_update_delete_policies
--
-- brands.* has had only two of its four documented RLS policies committed to
-- this repo (brands_read, brands_admin_insert) — the UPDATE/DELETE pair was
-- applied directly to a live project and never captured as a migration file.
-- Discovered via E2E testing on a freshly-provisioned local stack: without
-- these, editing or deleting a brand matches zero rows under RLS while
-- PostgREST still returns 200 OK, so the UI shows a false success toast and
-- nothing actually changes.
--
-- Companion plan: .specs/manage-brands/plan.md §10 (documents all four
-- policies as "already live and verified"; only these two were missing here).
-- ============================================================================

CREATE POLICY "brands_admin_update" ON brands
    FOR UPDATE
    USING ((SELECT public.authorize('brands.edit')))
    WITH CHECK ((SELECT public.authorize('brands.edit')));

CREATE POLICY "brands_admin_delete" ON brands
    FOR DELETE
    USING ((SELECT public.authorize('brands.delete')));
