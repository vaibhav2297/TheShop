-- ============================================================================
-- 0022_create_product_variants
--
-- Promotes `products` from a read-only catalogue table into an admin-managed
-- aggregate: an ordered image gallery, per-product option types/values, and
-- generated variants — one SKU per product/variant sharing one store-wide
-- namespace (RULE-8). Companion plan: .specs/create-product/plan.md §10
-- ============================================================================

CREATE TABLE product_images (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    object_key  TEXT NOT NULL,
    position    INTEGER NOT NULL CHECK (position >= 0),
    is_primary  BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_product_images_primary ON product_images(product_id) WHERE is_primary;
CREATE INDEX idx_product_images_product ON product_images(product_id, position);

CREATE TABLE product_option_types (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    name        TEXT NOT NULL CHECK (btrim(name) <> ''),
    position    INTEGER NOT NULL CHECK (position >= 0)
);
CREATE UNIQUE INDEX ux_product_option_types_name
    ON product_option_types(product_id, lower(btrim(name)));

CREATE TABLE product_option_values (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    option_type_id  UUID NOT NULL REFERENCES product_option_types(id) ON DELETE CASCADE,
    value           TEXT NOT NULL CHECK (btrim(value) <> ''),
    position        INTEGER NOT NULL CHECK (position >= 0)
);
CREATE UNIQUE INDEX ux_product_option_values_value
    ON product_option_values(option_type_id, lower(btrim(value)));

CREATE TABLE product_variants (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id      UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    sku             TEXT NOT NULL CHECK (btrim(sku) <> ''),
    original_price  NUMERIC(10,2) CHECK (original_price IS NULL OR original_price > 0),
    sale_price      NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price > 0),
    stock_quantity  INTEGER CHECK (stock_quantity IS NULL OR stock_quantity >= 0),
    is_available    BOOLEAN NOT NULL DEFAULT TRUE,
    -- RULE-13: removing a pinned gallery image unpins its variants automatically.
    image_id        UUID REFERENCES product_images(id) ON DELETE SET NULL,
    position        INTEGER NOT NULL CHECK (position >= 0),
    CONSTRAINT variant_sale_below_original
        CHECK (sale_price IS NULL OR original_price IS NULL OR sale_price < original_price)
);
CREATE INDEX idx_product_variants_product ON product_variants(product_id, position);

CREATE TABLE product_variant_option_values (
    variant_id      UUID NOT NULL REFERENCES product_variants(id) ON DELETE CASCADE,
    option_value_id UUID NOT NULL REFERENCES product_option_values(id) ON DELETE CASCADE,
    PRIMARY KEY (variant_id, option_value_id)
);

-- Decision 6: ONE store-wide SKU namespace spanning products and variants (RULE-8). A single
-- primary key is the only way to express uniqueness across two tables atomically; cascades from
-- both parents keep it self-cleaning.
CREATE TABLE product_sku_registry (
    sku_normalized  TEXT PRIMARY KEY,
    product_id      UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    variant_id      UUID REFERENCES product_variants(id) ON DELETE CASCADE
);
CREATE INDEX idx_product_sku_registry_product ON product_sku_registry(product_id);

-- ============================================================================
-- Row-Level Security
-- ============================================================================
ALTER TABLE product_images                ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_option_types          ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_option_values         ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_variants              ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_variant_option_values ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_sku_registry          ENABLE ROW LEVEL SECURITY;

-- product_images: customer surface reads the children of PUBLISHED products only.
CREATE POLICY "product_images_public_read" ON product_images
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM products p WHERE p.id = product_images.product_id AND p.is_published)
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_images_admin_insert" ON product_images
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_images_admin_update" ON product_images
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_images_admin_delete" ON product_images
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));

-- product_option_types
CREATE POLICY "product_option_types_public_read" ON product_option_types
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM products p WHERE p.id = product_option_types.product_id AND p.is_published)
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_option_types_admin_insert" ON product_option_types
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_option_types_admin_update" ON product_option_types
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_option_types_admin_delete" ON product_option_types
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));

-- product_option_values (resolves owning product via option_type_id -> product_option_types)
CREATE POLICY "product_option_values_public_read" ON product_option_values
    FOR SELECT USING (
        EXISTS (
            SELECT 1 FROM product_option_types t
            JOIN products p ON p.id = t.product_id
            WHERE t.id = product_option_values.option_type_id AND p.is_published
        )
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_option_values_admin_insert" ON product_option_values
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_option_values_admin_update" ON product_option_values
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_option_values_admin_delete" ON product_option_values
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));

-- product_variants
CREATE POLICY "product_variants_public_read" ON product_variants
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM products p WHERE p.id = product_variants.product_id AND p.is_published)
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_variants_admin_insert" ON product_variants
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_variants_admin_update" ON product_variants
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_variants_admin_delete" ON product_variants
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));

-- product_variant_option_values (resolves owning product via variant_id -> product_variants)
CREATE POLICY "product_variant_option_values_public_read" ON product_variant_option_values
    FOR SELECT USING (
        EXISTS (
            SELECT 1 FROM product_variants v
            JOIN products p ON p.id = v.product_id
            WHERE v.id = product_variant_option_values.variant_id AND p.is_published
        )
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_variant_option_values_admin_insert" ON product_variant_option_values
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_variant_option_values_admin_update" ON product_variant_option_values
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_variant_option_values_admin_delete" ON product_variant_option_values
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));

-- product_sku_registry: admin-only read; all writes go through save_product() (SECURITY DEFINER),
-- which bypasses RLS as the function owner — no client-facing write policy is needed or granted.
CREATE POLICY "product_sku_registry_admin_read" ON product_sku_registry
    FOR SELECT USING ((SELECT public.authorize('products.view')));
