-- Variant filters: scoped choices and same-variant matching.

CREATE OR REPLACE FUNCTION public.variant_filter_options(p_admin BOOLEAN DEFAULT FALSE)
RETURNS JSONB LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = '' AS $$
DECLARE result JSONB;
BEGIN
    IF p_admin AND NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
        'key', 'variant:' || g.type_key, 'name', g.name,
        'values', g.values, 'options', g.options) ORDER BY g.type_key), '[]'::jsonb)
    INTO result
    FROM (
        SELECT d.type_key, min(d.type_label COLLATE "C") AS name,
            jsonb_agg(d.value_label ORDER BY d.value_key) AS values,
            jsonb_agg(jsonb_build_object('value', d.value_key, 'label', d.value_label)
                ORDER BY d.value_key) AS options
        FROM (
            SELECT lower(btrim(t.name)) AS type_key, lower(btrim(o.value)) AS value_key,
                min(btrim(t.name) COLLATE "C") AS type_label,
                min(btrim(o.value) COLLATE "C") AS value_label
            FROM public.products p
            JOIN public.product_variants v ON v.product_id = p.id
            JOIN public.product_variant_option_values a ON a.variant_id = v.id
            JOIN public.product_option_values o ON o.id = a.option_value_id
            JOIN public.product_option_types t ON t.id = o.option_type_id AND t.product_id = p.id
            WHERE p_admin OR (p.is_published AND v.is_available)
            GROUP BY lower(btrim(t.name)), lower(btrim(o.value))
        ) d GROUP BY d.type_key
    ) g;
    RETURN result;
END;
$$;

CREATE OR REPLACE FUNCTION public.product_matches_variant_filters(
    p_product_id UUID, p_filters JSONB, p_admin BOOLEAN DEFAULT FALSE)
RETURNS BOOLEAN LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = '' AS $$
BEGIN
    IF p_admin AND NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    IF jsonb_typeof(COALESCE(p_filters, '{}'::jsonb)) <> 'object' THEN
        RAISE EXCEPTION 'invalid variant filters' USING ERRCODE = '22023';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_each(COALESCE(p_filters, '{}'::jsonb)) f
               WHERE jsonb_typeof(f.value) <> 'array') THEN
        RAISE EXCEPTION 'invalid variant filters' USING ERRCODE = '22023';
    END IF;
    RETURN COALESCE(p_filters, '{}'::jsonb) = '{}'::jsonb OR EXISTS (
        SELECT 1 FROM public.product_variants v
        JOIN public.products p ON p.id = v.product_id
        WHERE v.product_id = p_product_id
          AND (p_admin OR (p.is_published AND v.is_available))
          AND NOT EXISTS (
              SELECT 1 FROM jsonb_each(p_filters) f
              WHERE NOT EXISTS (
                  SELECT 1 FROM public.product_variant_option_values a
                  JOIN public.product_option_values o ON o.id = a.option_value_id
                  JOIN public.product_option_types t ON t.id = o.option_type_id
                  WHERE a.variant_id = v.id AND t.product_id = p.id
                    AND f.key = 'variant:' || lower(btrim(t.name))
                    AND f.value ? lower(btrim(o.value))
              )
          )
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.get_catalogue_filters()
RETURNS JSONB LANGUAGE sql STABLE SECURITY INVOKER SET search_path = '' AS $$
    SELECT jsonb_build_object(
        'categories', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name)
            ORDER BY c.name) FROM public.categories c WHERE c.is_active), '[]'::jsonb),
        'brands', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name)
            ORDER BY b.name) FROM public.brands b WHERE b.is_active), '[]'::jsonb),
        'option_types', public.variant_filter_options(FALSE),
        'price_min', COALESCE((SELECT min(display_price) FROM public.products WHERE is_published), 0),
        'price_max', COALESCE((SELECT max(display_price) FROM public.products WHERE is_published), 0));
$$;

CREATE OR REPLACE FUNCTION public.get_admin_product_filters()
RETURNS JSONB LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = '' AS $$
BEGIN
    IF NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN jsonb_build_object(
        'brands', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name)
            ORDER BY b.name) FROM public.brands b
            WHERE EXISTS (SELECT 1 FROM public.products p WHERE p.brand_id = b.id)), '[]'::jsonb),
        'categories', COALESCE((SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name)
            ORDER BY c.name) FROM public.categories c
            WHERE EXISTS (SELECT 1 FROM public.products p WHERE p.category_id = c.id)), '[]'::jsonb),
        'option_types', public.variant_filter_options(TRUE),
        'price_min', COALESCE((SELECT min(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM public.product_variants v
            UNION ALL SELECT COALESCE(p.sale_price, p.original_price) FROM public.products p
            WHERE NOT EXISTS (SELECT 1 FROM public.product_variants v WHERE v.product_id = p.id)
        ) prices), 0),
        'price_max', COALESCE((SELECT max(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM public.product_variants v
            UNION ALL SELECT COALESCE(p.sale_price, p.original_price) FROM public.products p
            WHERE NOT EXISTS (SELECT 1 FROM public.product_variants v WHERE v.product_id = p.id)
        ) prices), 0));
END;
$$;

CREATE OR REPLACE FUNCTION public.catalogue_products_page(
    p_brand_ids UUID[], p_category_ids UUID[], p_price_min NUMERIC, p_price_max NUMERIC,
    p_sort TEXT, p_limit INT, p_offset INT, p_variant_filters JSONB DEFAULT '{}'::jsonb)
RETURNS JSONB LANGUAGE sql STABLE SECURITY INVOKER SET search_path = '' AS $$
    WITH matched AS MATERIALIZED (
        SELECT p.*, b.name AS brand_name, c.name AS category_name,
            COALESCE((SELECT i.object_key FROM public.product_images i WHERE i.product_id = p.id
                ORDER BY i.is_primary DESC, i.position, i.id LIMIT 1), p.image_path) AS primary_image_key
        FROM public.products p
        JOIN public.brands b ON b.id = p.brand_id
        JOIN public.categories c ON c.id = p.category_id
        WHERE p.is_published
          AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
          AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
          AND (p_price_min IS NULL OR p.display_price >= p_price_min)
          AND (p_price_max IS NULL OR p.display_price <= p_price_max)
          AND public.product_matches_variant_filters(p.id, p_variant_filters, FALSE)
    ), ordered AS (
        SELECT m.*, row_number() OVER (ORDER BY
            CASE WHEN p_sort = 'price-asc' THEN m.display_price END ASC,
            CASE WHEN p_sort = 'price-desc' THEN m.display_price END DESC,
            CASE WHEN p_sort = 'name-asc' THEN m.name END ASC,
            CASE WHEN p_sort = 'name-desc' THEN m.name END DESC,
            CASE WHEN p_sort NOT IN ('price-asc','price-desc','name-asc','name-desc')
                OR p_sort IS NULL THEN m.created_at END DESC,
            m.id) AS listing_order
        FROM matched m
    ), slice AS (
        SELECT * FROM ordered ORDER BY listing_order LIMIT p_limit OFFSET p_offset
    )
    SELECT jsonb_build_object(
        'items', COALESCE((SELECT jsonb_agg(to_jsonb(s) - 'listing_order' ORDER BY s.listing_order)
            FROM slice s), '[]'::jsonb),
        'total_count', (SELECT count(*) FROM matched));
$$;

DROP FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT);
CREATE FUNCTION public.admin_products_page(
    p_search TEXT, p_status TEXT, p_brand_ids UUID[], p_category_ids UUID[],
    p_price_min NUMERIC, p_price_max NUMERIC, p_sort TEXT, p_limit INT, p_offset INT,
    p_variant_filters JSONB DEFAULT '{}'::jsonb)
RETURNS TABLE (
    id UUID, name TEXT, sku TEXT, brand_id UUID, brand_name TEXT, category_id UUID,
    category_name TEXT, currency TEXT, is_published BOOLEAN, primary_image_key TEXT,
    variant_count INT, min_price NUMERIC, max_price NUMERIC, total_count BIGINT)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = '' AS $$
DECLARE v_search TEXT := NULLIF(btrim(COALESCE(p_search, '')), '');
BEGIN
    IF NOT COALESCE(public.authorize('products.view'), FALSE) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN QUERY
    WITH variant_price AS (
        SELECT v.product_id, COALESCE(v.sale_price, v.original_price) AS price
        FROM public.product_variants v
    ), rollup AS (
        SELECT p.id AS product_id,
            (SELECT count(*)::int FROM public.product_variants v WHERE v.product_id = p.id) AS variant_count,
            COALESCE((SELECT min(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                COALESCE(p.sale_price, p.original_price)) AS min_price,
            COALESCE((SELECT max(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                COALESCE(p.sale_price, p.original_price)) AS max_price
        FROM public.products p
    ), matched AS (
        SELECT p.id, p.name, p.sku, p.brand_id, b.name AS brand_name,
            p.category_id, c.name AS category_name, p.currency, p.is_published,
            (SELECT i.object_key FROM public.product_images i
                WHERE i.product_id = p.id AND i.is_primary LIMIT 1) AS primary_image_key,
            r.variant_count, r.min_price, r.max_price, p.created_at, count(*) OVER () AS total_count
        FROM public.products p
        JOIN public.brands b ON b.id = p.brand_id
        JOIN public.categories c ON c.id = p.category_id
        JOIN rollup r ON r.product_id = p.id
        WHERE (v_search IS NULL OR p.name ILIKE '%' || v_search || '%')
          AND (p_status IS NULL OR p.is_published = (p_status = 'active'))
          AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
          AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
          AND public.product_matches_variant_filters(p.id, p_variant_filters, TRUE)
          AND ((p_price_min IS NULL AND p_price_max IS NULL)
              OR EXISTS (SELECT 1 FROM variant_price vp WHERE vp.product_id = p.id
                  AND vp.price IS NOT NULL
                  AND (p_price_min IS NULL OR vp.price >= p_price_min)
                  AND (p_price_max IS NULL OR vp.price <= p_price_max))
              OR (r.variant_count = 0 AND COALESCE(p.sale_price, p.original_price) IS NOT NULL
                  AND (p_price_min IS NULL OR COALESCE(p.sale_price, p.original_price) >= p_price_min)
                  AND (p_price_max IS NULL OR COALESCE(p.sale_price, p.original_price) <= p_price_max)))
    )
    SELECT m.id, m.name, m.sku, m.brand_id, m.brand_name, m.category_id, m.category_name,
        m.currency, m.is_published, m.primary_image_key, m.variant_count, m.min_price, m.max_price, m.total_count
    FROM matched m
    ORDER BY
        CASE WHEN p_sort = 'name-desc' THEN m.name END DESC,
        CASE WHEN p_sort = 'newest' THEN m.created_at END DESC,
        CASE WHEN p_sort = 'oldest' THEN m.created_at END ASC,
        CASE WHEN p_sort = 'price-desc' THEN m.min_price END DESC,
        CASE WHEN p_sort = 'price-asc' THEN m.min_price END ASC,
        CASE WHEN p_sort NOT IN ('name-desc','newest','oldest','price-asc','price-desc')
            THEN m.name END ASC, m.id
    LIMIT p_limit OFFSET p_offset;
END;
$$;


REVOKE ALL ON FUNCTION public.variant_filter_options(BOOLEAN) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.variant_filter_options(BOOLEAN) TO anon, authenticated;
REVOKE ALL ON FUNCTION public.product_matches_variant_filters(UUID, JSONB, BOOLEAN) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.product_matches_variant_filters(UUID, JSONB, BOOLEAN) TO anon, authenticated;
REVOKE ALL ON FUNCTION public.get_catalogue_filters() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.get_catalogue_filters() TO anon, authenticated;
REVOKE ALL ON FUNCTION public.get_admin_product_filters() FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_admin_product_filters() TO authenticated;
REVOKE ALL ON FUNCTION public.catalogue_products_page(UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.catalogue_products_page(UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) TO anon, authenticated;
REVOKE ALL ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT, JSONB) TO authenticated;
NOTIFY pgrst, 'reload schema';
