-- ============================================================================
-- 0028_manage_products
--
-- Manage-product admin listing (.specs/manage-product/plan.md §10): a paged,
-- searched, filtered, sorted read over products + their variant price rollup,
-- an admin filter-option source, and atomic partial-success deletion.
-- ============================================================================

CREATE INDEX IF NOT EXISTS idx_products_created_at ON products (created_at DESC);

-- One page of the manage-product list. SECURITY DEFINER: the aggregation spans
-- products and product_variants, and unpublished products must be visible to
-- products.view holders regardless of per-table policy evaluation order.
-- RULE-9/10/11: the rollup covers EVERY variant row (inactive and unavailable
-- included; a deleted variant has no row), the price filter matches an ACTUAL
-- variant price, and both price sorts use the unfiltered lowest price.
-- A product with no variants falls back to its own effective price as a single
-- implicit price (plan §5 Decision 13).
CREATE OR REPLACE FUNCTION public.admin_products_page(
    p_search       TEXT,
    p_status       TEXT,          -- 'active' | 'inactive' | NULL for no narrowing
    p_brand_ids    UUID[],
    p_category_ids UUID[],
    p_price_min    NUMERIC,
    p_price_max    NUMERIC,
    p_sort         TEXT,          -- name-asc | name-desc | newest | oldest | price-asc | price-desc
    p_limit        INT,
    p_offset       INT)
RETURNS TABLE (
    id                UUID,
    name              TEXT,
    sku               TEXT,
    brand_id          UUID,
    brand_name        TEXT,
    category_id       UUID,
    category_name     TEXT,
    currency          TEXT,
    is_published      BOOLEAN,
    primary_image_key TEXT,
    variant_count     INT,
    min_price         NUMERIC,
    max_price         NUMERIC,
    total_count       BIGINT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_search TEXT := NULLIF(btrim(COALESCE(p_search, '')), '');
BEGIN
    IF NOT public.authorize('products.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH variant_price AS (
        SELECT v.product_id, COALESCE(v.sale_price, v.original_price) AS price
        FROM product_variants v
    ),
    rollup AS (
        SELECT p.id AS product_id,
               (SELECT count(*)::int FROM product_variants v WHERE v.product_id = p.id) AS variant_count,
               COALESCE((SELECT min(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                        COALESCE(p.sale_price, p.original_price)) AS min_price,
               COALESCE((SELECT max(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                        COALESCE(p.sale_price, p.original_price)) AS max_price
        FROM products p
    ),
    matched AS (
        SELECT p.id, p.name, p.sku, p.brand_id, b.name AS brand_name,
               p.category_id, c.name AS category_name, p.currency, p.is_published,
               (SELECT i.object_key FROM product_images i
                 WHERE i.product_id = p.id AND i.is_primary LIMIT 1) AS primary_image_key,
               r.variant_count, r.min_price, r.max_price, p.created_at,
               count(*) OVER () AS total_count
        FROM products p
        JOIN brands b     ON b.id = p.brand_id
        JOIN categories c ON c.id = p.category_id
        JOIN rollup r     ON r.product_id = p.id
        WHERE (v_search IS NULL OR p.name ILIKE '%' || v_search || '%')
          AND (p_status IS NULL OR p.is_published = (p_status = 'active'))
          AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
          AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
          AND (
                (p_price_min IS NULL AND p_price_max IS NULL)
                OR EXISTS (
                    SELECT 1 FROM variant_price vp
                    WHERE vp.product_id = p.id
                      AND vp.price IS NOT NULL
                      AND (p_price_min IS NULL OR vp.price >= p_price_min)
                      AND (p_price_max IS NULL OR vp.price <= p_price_max))
                OR (r.variant_count = 0
                    AND COALESCE(p.sale_price, p.original_price) IS NOT NULL
                    AND (p_price_min IS NULL OR COALESCE(p.sale_price, p.original_price) >= p_price_min)
                    AND (p_price_max IS NULL OR COALESCE(p.sale_price, p.original_price) <= p_price_max))
              )
    )
    SELECT m.id, m.name, m.sku, m.brand_id, m.brand_name, m.category_id, m.category_name,
           m.currency, m.is_published, m.primary_image_key,
           m.variant_count, m.min_price, m.max_price, m.total_count
    FROM matched m
    ORDER BY
        CASE WHEN p_sort = 'name-desc'  THEN m.name END DESC,
        CASE WHEN p_sort = 'newest'     THEN m.created_at END DESC,
        CASE WHEN p_sort = 'oldest'     THEN m.created_at END ASC,
        CASE WHEN p_sort = 'price-desc' THEN m.min_price END DESC,
        CASE WHEN p_sort = 'price-asc'  THEN m.min_price END ASC,
        CASE WHEN p_sort NOT IN ('name-desc','newest','oldest','price-asc','price-desc')
             THEN m.name END ASC,
        m.id                                  -- deterministic tie-break across pages
    LIMIT p_limit OFFSET p_offset;
END;
$$;

-- Filter options for the admin list: brands and categories that actually own a
-- product (Active or not — an Inactive brand's products must stay filterable),
-- plus the variant-aware price bounds the range control needs.
CREATE OR REPLACE FUNCTION public.get_admin_product_filters()
RETURNS JSONB
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_result JSONB;
BEGIN
    IF NOT public.authorize('products.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    SELECT jsonb_build_object(
        'brands', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name) ORDER BY b.name)
            FROM brands b WHERE EXISTS (SELECT 1 FROM products p WHERE p.brand_id = b.id)), '[]'::jsonb),
        'categories', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name) ORDER BY c.name)
            FROM categories c WHERE EXISTS (SELECT 1 FROM products p WHERE p.category_id = c.id)), '[]'::jsonb),
        'price_min', COALESCE((SELECT min(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM product_variants v
            UNION ALL
            SELECT COALESCE(p.sale_price, p.original_price) FROM products p
             WHERE NOT EXISTS (SELECT 1 FROM product_variants v2 WHERE v2.product_id = p.id)) prices), 0),
        'price_max', COALESCE((SELECT max(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM product_variants v
            UNION ALL
            SELECT COALESCE(p.sale_price, p.original_price) FROM products p
             WHERE NOT EXISTS (SELECT 1 FROM product_variants v2 WHERE v2.product_id = p.id)) prices), 0)
    ) INTO v_result;

    RETURN v_result;
END;
$$;

-- Atomic partial-success deletion (RULE-3/RULE-4). Counting and deleting in one
-- statement closes the TOCTOU window a client-side guard would leave open.
-- reference_count counts records that REFERENCE a product from outside it. The
-- product's own children (product_images, product_option_types, product_variants,
-- product_sku_registry) are owned parts with ON DELETE CASCADE and never block
-- their parent. No such referencing table exists yet; when order_items ships, add
-- its count to the single expression below and nothing else changes.
-- image_keys carries only object keys no OTHER product's row uses, computed in
-- `candidates` BEFORE the delete: PostgreSQL does not guarantee that a row
-- removed by an FK ON DELETE CASCADE trigger (product_images cascading off the
-- `removed` CTE's DELETE) is visible to a sibling CTE's read in the same
-- statement, so a post-delete "still exists" check always finds the
-- already-cascaded row and wrongly reports every key as still in use. Checking
-- "exclusively owned" against the live pre-delete rows avoids that, and the
-- CASE below still zeroes image_keys for a candidate that was not actually
-- removed (RULE-8).
CREATE OR REPLACE FUNCTION public.delete_products(product_ids UUID[])
RETURNS TABLE (id UUID, name TEXT, reference_count BIGINT, deleted BOOLEAN, image_keys TEXT[])
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('products.delete') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH candidates AS (
        SELECT p.id, p.name,
               0::bigint AS reference_count,          -- no external referencing table exists yet
               COALESCE((
                   SELECT array_agg(i.object_key) FROM product_images i
                   WHERE i.product_id = p.id
                     AND NOT EXISTS (
                         SELECT 1 FROM product_images i2
                         WHERE i2.object_key = i.object_key AND i2.product_id <> p.id)
               ), ARRAY[]::text[]) AS image_keys
        FROM products p
        WHERE p.id = ANY(product_ids)
    ),
    removed AS (
        DELETE FROM products
        WHERE products.id IN (SELECT c.id FROM candidates c WHERE c.reference_count = 0)
        RETURNING products.id
    )
    SELECT c.id, c.name, c.reference_count,
           EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id),
           CASE WHEN EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id)
                THEN c.image_keys
                ELSE ARRAY[]::text[]
           END
    FROM candidates c;
END;
$$;

REVOKE ALL ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_admin_product_filters() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.delete_products(UUID[])     FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_admin_product_filters() TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_products(UUID[])     TO authenticated;

ALTER TABLE products ENABLE ROW LEVEL SECURITY;   -- already enabled; stated for completeness

CREATE POLICY "products_admin_delete" ON products
    FOR DELETE USING ((SELECT public.authorize('products.delete')));
