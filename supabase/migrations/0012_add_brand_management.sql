-- ============================================================================
-- 0012_add_brand_management
--
-- Add-brand admin capability (.specs/add-brand/plan.md §10): extends `brands`
-- with mutable admin attributes, registers the `brands.*` permission module,
-- grants it to Admin/SuperAdmin, and provisions the public `brand-logos`
-- storage bucket with its RLS policies.
--
-- Reconstructed from the plan after this migration was applied directly to
-- the shared project but never committed as a file — see the note in
-- 0013_add_brand_admin_deltas.sql, which layers two small deltas (created_at,
-- and the brands SELECT policy rename) on top of this one.
--
-- Companion plan: .specs/add-brand/plan.md §10
-- ============================================================================

ALTER TABLE brands
    ADD COLUMN IF NOT EXISTS description TEXT,
    ADD COLUMN IF NOT EXISTS logo_path   TEXT,
    ADD COLUMN IF NOT EXISTS is_active   BOOLEAN     NOT NULL DEFAULT TRUE;

-- RULE-2: case/space-insensitive name uniqueness, enforced at the DB.
CREATE UNIQUE INDEX IF NOT EXISTS ux_brands_name_normalized
    ON brands (lower(btrim(name)));

-- New permission module (generated from PermissionCatalogue.Brands).
INSERT INTO permissions (code, module) VALUES
    ('brands.view', 'brands'), ('brands.create', 'brands'),
    ('brands.edit', 'brands'), ('brands.delete', 'brands')
ON CONFLICT (code) DO NOTHING;

-- Admin gets the brands module; SuperAdmin already gets everything via its seed.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r JOIN permissions p ON p.module = 'brands'
WHERE r.name_key = 'Admin'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r JOIN permissions p ON p.module = 'brands'
WHERE r.name_key = 'SuperAdmin'
ON CONFLICT DO NOTHING;

-- Logo bucket (public, mirrors product-images).
INSERT INTO storage.buckets (id, name, public)
VALUES ('brand-logos', 'brand-logos', true)
ON CONFLICT (id) DO NOTHING;

-- brand-logos storage objects: read for brands.view, write per action.
CREATE POLICY "brand_logos_read" ON storage.objects
    FOR SELECT USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.view')));
CREATE POLICY "brand_logos_insert" ON storage.objects
    FOR INSERT WITH CHECK (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.create')));
CREATE POLICY "brand_logos_update" ON storage.objects
    FOR UPDATE USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.edit')))
    WITH CHECK (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.edit')));
CREATE POLICY "brand_logos_delete" ON storage.objects
    FOR DELETE USING (bucket_id = 'brand-logos' AND (SELECT public.authorize('brands.delete')));

-- brands table: gate writes on brands.create. The SELECT policy itself is
-- replaced by 0013_add_brand_admin_deltas (still "brands_public_read" here;
-- that migration swaps it for the is_active-aware predicate).
CREATE POLICY "brands_admin_insert" ON brands
    FOR INSERT WITH CHECK ((SELECT public.authorize('brands.create')));

-- get_catalogue_filters() brand facet: defence-in-depth is_active filter,
-- matching the function's existing is_published posture for products.
CREATE OR REPLACE FUNCTION public.get_catalogue_filters()
RETURNS JSONB
LANGUAGE sql
STABLE
SECURITY INVOKER
SET search_path = ''
AS $$
    SELECT jsonb_build_object(
        'categories', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name) ORDER BY c.name)
            FROM public.categories c
        ), '[]'::jsonb),
        'brands', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name) ORDER BY b.name)
            FROM public.brands b
            WHERE b.is_active
        ), '[]'::jsonb),
        'flavours', COALESCE((
            SELECT jsonb_agg(f ORDER BY f)
            FROM (
                SELECT DISTINCT flavour AS f
                FROM public.products
                WHERE is_published = true
                  AND flavour IS NOT NULL
                  AND btrim(flavour) <> ''
            ) distinct_flavours
        ), '[]'::jsonb),
        'nicotine_strengths', COALESCE((
            SELECT jsonb_agg(n ORDER BY n)
            FROM (
                SELECT DISTINCT nicotine_strength_mg AS n
                FROM public.products
                WHERE is_published = true
                  AND nicotine_strength_mg IS NOT NULL
            ) distinct_strengths
        ), '[]'::jsonb),
        'price_min', COALESCE((SELECT min(original_price) FROM public.products WHERE is_published = true), 0),
        'price_max', COALESCE((SELECT max(original_price) FROM public.products WHERE is_published = true), 0)
    );
$$;

GRANT EXECUTE ON FUNCTION public.get_catalogue_filters() TO anon, authenticated;
