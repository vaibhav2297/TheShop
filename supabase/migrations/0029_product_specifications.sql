-- ============================================================================
-- 0029_product_specifications
--
-- An ordered name/value child of products, with the same lifecycle as
-- product_option_types. Companion plan: .specs/product-description/plan.md §10
-- ============================================================================

CREATE TABLE product_specifications (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    name        TEXT NOT NULL CHECK (btrim(name) <> ''),
    value       TEXT NOT NULL CHECK (btrim(value) <> ''),
    position    INTEGER NOT NULL CHECK (position >= 0)
);

CREATE UNIQUE INDEX ux_product_specifications_name
    ON product_specifications(product_id, lower(btrim(name)));
CREATE INDEX idx_product_specifications_product
    ON product_specifications(product_id, position);

ALTER TABLE product_specifications ENABLE ROW LEVEL SECURITY;

-- Mirrors product_option_types: the customer surface reads the children of
-- PUBLISHED products only; staff with products.view read every row.
CREATE POLICY "product_specifications_public_read" ON product_specifications
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM products p WHERE p.id = product_specifications.product_id AND p.is_published)
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_specifications_admin_insert" ON product_specifications
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_specifications_admin_update" ON product_specifications
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_specifications_admin_delete" ON product_specifications
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));
