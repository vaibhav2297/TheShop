-- ============================================================================
-- 0006_add_effective_price
--
-- The storefront displays, filters, and sorts on the price a customer actually
-- pays — the sale price when a product is discounted, otherwise the original
-- price (the domain's ProductPricing.Effective). Until now the catalogue query
-- filtered/sorted on original_price and the filter sidebar's range was built
-- from min/max(original_price), so a discounted product shown at its sale price
-- could fall outside a price filter that its displayed price satisfies.
--
-- This migration introduces a STORED generated column, effective_price =
-- COALESCE(sale_price, original_price), so PostgREST can filter and order on it
-- with a plain column reference (COALESCE cannot be expressed client-side), and
-- an index to keep those range/order scans cheap. get_catalogue_filters() is
-- redefined to source its price range from effective_price so the sidebar
-- bounds match what the grid filters on.
--
-- Companion plan: .specs/product-catalogue/plan.md
-- ============================================================================

-- COALESCE(sale_price, original_price) is immutable and references only same-row
-- columns, so it is a valid STORED generated expression. Both operands are
-- NUMERIC(10,2), so effective_price inherits that precision/scale.
ALTER TABLE public.products
    ADD COLUMN IF NOT EXISTS effective_price NUMERIC(10,2)
        GENERATED ALWAYS AS (COALESCE(sale_price, original_price)) STORED;

-- Backs the catalogue price-range filter and the "Price: low/high" sort.
CREATE INDEX IF NOT EXISTS idx_products_effective_price ON public.products(effective_price);

-- ----------------------------------------------------------------------------
-- Redefine get_catalogue_filters() so the price range reflects the effective
-- (displayed) price. Only the price_min / price_max sources change from
-- original_price to effective_price; every other facet is unchanged from 0003.
-- SECURITY INVOKER + search_path = '' + full schema-qualification are retained.
-- ----------------------------------------------------------------------------
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
        'price_min', COALESCE((SELECT min(effective_price) FROM public.products WHERE is_published = true), 0),
        'price_max', COALESCE((SELECT max(effective_price) FROM public.products WHERE is_published = true), 0)
    );
$$;

GRANT EXECUTE ON FUNCTION public.get_catalogue_filters() TO anon, authenticated;
