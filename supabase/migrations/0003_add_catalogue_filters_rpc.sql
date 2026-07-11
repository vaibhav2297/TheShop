-- ============================================================================
-- 0003_add_catalogue_filters_rpc
--
-- get_catalogue_filters() — the whole catalogue filter sidebar in one round-
-- trip. Replaces five separate client reads (categories, brands, and three
-- full `products` scans for flavour / nicotine / price) with a single
-- server-side aggregation returning one JSON object. The client no longer
-- downloads every published product row just to compute distinct facets.
--
-- SECURITY INVOKER: the function reads only public storefront data (published
-- products + public categories/brands), so RLS governs it exactly as it would
-- a direct SELECT — the is_published predicate is also applied explicitly as
-- defence in depth. search_path is locked to '' to prevent search_path
-- injection; every table is therefore fully schema-qualified.
--
-- Companion plan: .specs/product-catalogue/plan.md
-- ============================================================================
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
