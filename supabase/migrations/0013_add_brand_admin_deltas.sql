-- ============================================================================
-- 0013_add_brand_admin_deltas
--
-- Add-brand admin capability (.specs/add-brand/plan.md §10). Most of this
-- feature's schema/RLS/seed work (brands.description/logo_path/is_active,
-- the ux_brands_normalized_name unique index, the brands.* permissions and
-- their Admin/SuperAdmin grants, the brand-logos bucket and its storage RLS,
-- and the get_catalogue_filters() is_active hardening) was already applied
-- by 0012_add_brand_management. Two gaps remained:
--
--   1. brands.created_at was never added.
--   2. The brands SELECT policy was still "brands_public_read" (USING true)
--      from the original product-catalogue migration (0002) — every caller,
--      including anon, could read Inactive brands. That violates AC-7/
--      Decision 3 (Inactive brands must be invisible to customers). Replaced
--      with the is_active OR authorize('brands.view') predicate.
-- ============================================================================

ALTER TABLE brands
    ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();

DROP POLICY IF EXISTS "brands_public_read" ON brands;

CREATE POLICY "brands_read" ON brands
    FOR SELECT USING (is_active OR (SELECT public.authorize('brands.view')));
