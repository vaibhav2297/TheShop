-- ============================================================================
-- 0023_products_admin_writes
--
-- Adds a required store-wide SKU and draft tolerance to `products`; retires
-- flavour/nicotine (RULE-19) in favour of the generic per-product option
-- types customers now filter by; adds the min_variant_price/has_sellable_variant
-- read model (RULE-16, AC-29); grants staff admin read/write on `products`;
-- rewrites the product-images bucket's obsolete role-claim policies onto
-- authorize() (Decision 10). Companion plan: .specs/create-product/plan.md §10
-- ============================================================================

-- RULE-2 (Decision 7): unique names are what keep name-derived SKU suggestions from colliding.
-- The 0002 seed names are already distinct, so this index applies without a de-duplication pass.
CREATE UNIQUE INDEX ux_products_name_normalized ON products (lower(btrim(name)));

ALTER TABLE products ADD COLUMN IF NOT EXISTS sku TEXT;
UPDATE products SET sku = upper(regexp_replace(btrim(name), '[^A-Za-z0-9]+', '-', 'g'))
    WHERE sku IS NULL;                                            -- back-fill the 0002 seed rows
ALTER TABLE products ALTER COLUMN sku SET NOT NULL;
CREATE INDEX IF NOT EXISTS idx_products_sku_lower ON products (lower(sku));

ALTER TABLE products ALTER COLUMN original_price DROP NOT NULL;   -- RULE-15 drafts
ALTER TABLE products ALTER COLUMN stock_quantity DROP NOT NULL;
ALTER TABLE products DROP COLUMN IF EXISTS flavour;               -- RULE-19
ALTER TABLE products DROP COLUMN IF EXISTS nicotine_strength_mg;  -- RULE-19
ALTER TABLE products
    ADD COLUMN IF NOT EXISTS min_variant_price     NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS has_sellable_variant  BOOLEAN     NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS updated_at            TIMESTAMPTZ NOT NULL DEFAULT now();

-- Backfill has_sellable_variant for the pre-existing no-variant seed rows: with no variants,
-- "sellable" is exactly their own in-stock state.
UPDATE products SET has_sellable_variant = (stock_quantity IS NOT NULL AND stock_quantity > 0);

INSERT INTO product_sku_registry (sku_normalized, product_id)
    SELECT lower(btrim(sku)), id FROM products ON CONFLICT DO NOTHING;

-- ----------------------------------------------------------------------------
-- Read-model refresh: min_variant_price / has_sellable_variant (RULE-16, AC-29).
-- A ROW-level (not statement-level) trigger — a deliberate, behavior-preserving
-- deviation from the plan's "statement trigger" phrasing (plan §10): transition
-- tables would be needed for a combined INSERT/UPDATE/DELETE statement trigger,
-- and the row-level form is the simpler, provably-correct equivalent.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.refresh_product_read_model(p_product_id UUID)
RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    UPDATE products p
    SET min_variant_price = (
            SELECT min(COALESCE(v.sale_price, v.original_price))
            FROM product_variants v
            WHERE v.product_id = p.id
        ),
        has_sellable_variant = EXISTS (
            SELECT 1 FROM product_variants v
            WHERE v.product_id = p.id AND v.is_available AND v.stock_quantity > 0
        )
    WHERE p.id = p_product_id
      AND EXISTS (SELECT 1 FROM product_variants v WHERE v.product_id = p.id);
END;
$$;

CREATE OR REPLACE FUNCTION public.trg_refresh_product_read_model()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    PERFORM public.refresh_product_read_model(COALESCE(NEW.product_id, OLD.product_id));
    RETURN NULL;
END;
$$;

CREATE TRIGGER product_variants_refresh_read_model
AFTER INSERT OR UPDATE OR DELETE ON product_variants
FOR EACH ROW EXECUTE FUNCTION public.trg_refresh_product_read_model();

REVOKE ALL ON FUNCTION public.refresh_product_read_model(UUID) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.refresh_product_read_model(UUID) TO authenticated;

-- ----------------------------------------------------------------------------
-- get_catalogue_filters(): drop the two dedicated facets (Decision 3, RULE-19);
-- publish the generic option types of published products instead. Every other
-- facet (categories/brands with is_active, the effective_price range) is
-- unchanged from migration 0020.
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
            WHERE c.is_active
        ), '[]'::jsonb),
        'brands', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name) ORDER BY b.name)
            FROM public.brands b
            WHERE b.is_active
        ), '[]'::jsonb),
        'option_types', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('name', t.name, 'values', t.values) ORDER BY t.name)
            FROM (
                SELECT ot.name, jsonb_agg(DISTINCT ov.value) AS values
                FROM public.product_option_types ot
                JOIN public.product_option_values ov ON ov.option_type_id = ot.id
                JOIN public.products p ON p.id = ot.product_id
                WHERE p.is_published
                GROUP BY ot.name
            ) t
        ), '[]'::jsonb),
        'price_min', COALESCE((SELECT min(effective_price) FROM public.products WHERE is_published = true), 0),
        'price_max', COALESCE((SELECT max(effective_price) FROM public.products WHERE is_published = true), 0)
    );
$$;

GRANT EXECUTE ON FUNCTION public.get_catalogue_filters() TO anon, authenticated;

-- ----------------------------------------------------------------------------
-- products: admin read (RULE-17 — staff see Unpublished) and first write policies.
-- ----------------------------------------------------------------------------
CREATE POLICY "products_admin_read" ON products
    FOR SELECT USING ((SELECT public.authorize('products.view')));
CREATE POLICY "products_admin_insert" ON products
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create')));
CREATE POLICY "products_admin_update" ON products
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
-- No DELETE policy: spec Section 1 puts product deletion out of scope.

-- ----------------------------------------------------------------------------
-- Decision 10 — replace migration 0004's obsolete role-claim predicates on the
-- product-images Storage bucket with authorize(). Left as-is, every gallery
-- upload would be refused for staff holding products.create, since no token
-- carries a role: admin claim any more (RBAC replaced role-claim checks).
-- ----------------------------------------------------------------------------
DROP POLICY IF EXISTS "product_images_admin_read"   ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_insert" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_update" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_delete" ON storage.objects;

CREATE POLICY "product_images_read" ON storage.objects
    FOR SELECT USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.view')));
CREATE POLICY "product_images_insert" ON storage.objects
    FOR INSERT WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.create')));
CREATE POLICY "product_images_update" ON storage.objects
    FOR UPDATE USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')))
    WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')));
CREATE POLICY "product_images_delete" ON storage.objects
    FOR DELETE USING (bucket_id = 'product-images'
                      AND ((SELECT public.authorize('products.edit'))
                        OR (SELECT public.authorize('products.create'))));
