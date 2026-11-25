-- ============================================================================
-- 0020_manage_categories
-- Promotes `categories` from read-only reference data to a managed admin
-- aggregate. Companion plan: .specs/manage-categories/plan.md §10
-- ============================================================================

ALTER TABLE categories
    ADD COLUMN IF NOT EXISTS description TEXT,
    ADD COLUMN IF NOT EXISTS image_path  TEXT,
    ADD COLUMN IF NOT EXISTS is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS created_at  TIMESTAMPTZ NOT NULL DEFAULT now();

-- Retire the name-derived identifier (spec FR-11, Decision 6). Dropping the column drops
-- categories_slug_key with it, so a rename can no longer be refused for a slug clash (AC-24).
-- Migration 0002 seeds products via `WHERE slug = …`, but 0002 always runs before this one on
-- a replay, so that seed stays valid.
ALTER TABLE categories DROP COLUMN IF EXISTS slug;

-- RULE-2: case/space-insensitive name uniqueness, enforced at the DB. After the slug drop this
-- is the ONLY uniqueness constraint a category name must satisfy.
CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_name_normalized
    ON categories (lower(btrim(name)));

CREATE INDEX IF NOT EXISTS idx_categories_name_lower  ON categories (lower(name));
CREATE INDEX IF NOT EXISTS idx_categories_created_at  ON categories (created_at DESC);

-- Authoritative per-category product counts. SECURITY DEFINER because products' only SELECT
-- policy is products_public_read USING (is_published = true) — an unpublished product must
-- still count against deletion (spec RULE-6, AC-19), and no client role can see it.
CREATE OR REPLACE FUNCTION public.category_product_counts(category_ids UUID[])
RETURNS TABLE (category_id UUID, product_count BIGINT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('categories.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    SELECT c.id, count(p.id)
    FROM categories c
    LEFT JOIN products p ON p.category_id = c.id
    WHERE c.id = ANY(category_ids)
    GROUP BY c.id;
END;
$$;

-- Atomic partial-success deletion (spec RULE-15). Counting and deleting in one statement closes
-- the TOCTOU window a client-side guard would leave open.
CREATE OR REPLACE FUNCTION public.delete_categories(category_ids UUID[])
RETURNS TABLE (id UUID, name TEXT, image_path TEXT, product_count BIGINT, deleted BOOLEAN)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('categories.delete') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH candidates AS (
        SELECT c.id, c.name, c.image_path,
               (SELECT count(*) FROM products p WHERE p.category_id = c.id) AS product_count
        FROM categories c
        WHERE c.id = ANY(category_ids)
    ),
    removed AS (
        DELETE FROM categories
        WHERE categories.id IN (SELECT k.id FROM candidates k WHERE k.product_count = 0)
        RETURNING categories.id
    )
    SELECT k.id, k.name, k.image_path, k.product_count,
           EXISTS (SELECT 1 FROM removed r WHERE r.id = k.id)
    FROM candidates k;
END;
$$;

REVOKE ALL ON FUNCTION public.category_product_counts(UUID[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.delete_categories(UUID[])       FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.category_product_counts(UUID[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_categories(UUID[])       TO authenticated;

-- Category-image bucket, staff-visible only (spec: the image reaches no customer surface).
INSERT INTO storage.buckets (id, name, public)
VALUES ('category-images', 'category-images', true)
ON CONFLICT (id) DO NOTHING;

-- SELECT is deliberately UNCHANGED — see plan §5 Decision 2. Restricting it to is_active would
-- null out ProductRecord's embedded CategoryRecord for every product in a deactivated category
-- and break the catalogue, contradicting spec RULE-13 ("no product is hidden as a side effect").
-- Inactive categories are hidden from customers by the facet filter below, not by RLS.

CREATE POLICY "categories_admin_insert" ON categories
    FOR INSERT WITH CHECK ((SELECT public.authorize('categories.create')));

CREATE POLICY "categories_admin_update" ON categories
    FOR UPDATE
    USING ((SELECT public.authorize('categories.edit')))
    WITH CHECK ((SELECT public.authorize('categories.edit')));

CREATE POLICY "categories_admin_delete" ON categories
    FOR DELETE USING ((SELECT public.authorize('categories.delete')));

-- category-images storage objects: read for categories.view, write per action (mirrors brand-logos).
CREATE POLICY "category_images_read" ON storage.objects
    FOR SELECT USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.view')));
CREATE POLICY "category_images_insert" ON storage.objects
    FOR INSERT WITH CHECK (bucket_id = 'category-images' AND (SELECT public.authorize('categories.create')));
CREATE POLICY "category_images_update" ON storage.objects
    FOR UPDATE USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.edit')))
    WITH CHECK (bucket_id = 'category-images' AND (SELECT public.authorize('categories.edit')));
CREATE POLICY "category_images_delete" ON storage.objects
    FOR DELETE USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.delete')));

-- get_catalogue_filters(): the category facet gains the is_active predicate the brand facet
-- already has (migration 0012). This is the whole of FR-13's customer-facing effect — an
-- Inactive category leaves the filter sidebar while its products stay published and browsable.
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
            WHERE c.is_active
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
