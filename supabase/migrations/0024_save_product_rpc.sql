-- ============================================================================
-- 0024_save_product_rpc
--
-- save_product(payload): one transactional upsert of product + gallery +
-- option types + values + variants + pins + SKU registry rows. Sequences all
-- deletes before all inserts on an update, so a SKU moving between rows in
-- the same save can never self-collide on product_sku_registry's primary
-- key. SECURITY DEFINER with an authorize() guard and an updated_at
-- concurrency check. Companion plan: .specs/create-product/plan.md §10
-- ============================================================================

CREATE OR REPLACE FUNCTION public.save_product(payload JSONB)
RETURNS TABLE (id UUID, updated_at TIMESTAMPTZ)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_mode                TEXT := payload->>'mode';
    v_product             JSONB := payload->'product';
    v_product_id          UUID := (v_product->>'id')::uuid;
    v_expected_updated_at TIMESTAMPTZ := NULLIF(v_product->>'expected_updated_at', '')::timestamptz;
    v_updated_at          TIMESTAMPTZ;
    v_matched_rows        INT;
    v_image               JSONB;
    v_type                JSONB;
    v_value                JSONB;
    v_variant              JSONB;
    v_option_value_id_text TEXT;
BEGIN
    IF NOT (public.authorize('products.create') OR public.authorize('products.edit')) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    IF v_mode = 'create' THEN
        INSERT INTO products (
            id, name, description, sku, category_id, brand_id,
            original_price, sale_price, stock_quantity, is_published,
            has_sellable_variant, updated_at
        ) VALUES (
            v_product_id,
            v_product->>'name',
            COALESCE(v_product->>'description', ''),
            v_product->>'sku',
            (v_product->>'category_id')::uuid,
            (v_product->>'brand_id')::uuid,
            NULLIF(v_product->>'original_price', '')::numeric,
            NULLIF(v_product->>'sale_price', '')::numeric,
            NULLIF(v_product->>'stock_quantity', '')::integer,
            (v_product->>'is_published')::boolean,
            COALESCE((NULLIF(v_product->>'stock_quantity', '')::integer) > 0, false),
            now()
        )
        RETURNING products.updated_at INTO v_updated_at;

        INSERT INTO product_sku_registry (sku_normalized, product_id)
        VALUES (lower(btrim(v_product->>'sku')), v_product_id);
    ELSE
        UPDATE products SET
            name = v_product->>'name',
            description = COALESCE(v_product->>'description', ''),
            sku = v_product->>'sku',
            category_id = (v_product->>'category_id')::uuid,
            brand_id = (v_product->>'brand_id')::uuid,
            original_price = NULLIF(v_product->>'original_price', '')::numeric,
            sale_price = NULLIF(v_product->>'sale_price', '')::numeric,
            stock_quantity = NULLIF(v_product->>'stock_quantity', '')::integer,
            is_published = (v_product->>'is_published')::boolean,
            has_sellable_variant = COALESCE((NULLIF(v_product->>'stock_quantity', '')::integer) > 0, false),
            updated_at = now()
        WHERE products.id = v_product_id AND products.updated_at = v_expected_updated_at
        RETURNING products.updated_at INTO v_updated_at;

        GET DIAGNOSTICS v_matched_rows = ROW_COUNT;
        IF v_matched_rows = 0 THEN
            RAISE EXCEPTION 'concurrent modification' USING ERRCODE = 'serialization_failure';
        END IF;

        -- Delete-before-insert (all children + registry) so a SKU moving between rows in this
        -- same save can never self-collide on product_sku_registry's primary key.
        DELETE FROM product_sku_registry WHERE product_id = v_product_id;
        DELETE FROM product_variant_option_values
            WHERE variant_id IN (SELECT pv.id FROM product_variants pv WHERE pv.product_id = v_product_id);
        DELETE FROM product_variants WHERE product_id = v_product_id;
        DELETE FROM product_option_values
            WHERE option_type_id IN (SELECT t.id FROM product_option_types t WHERE t.product_id = v_product_id);
        DELETE FROM product_option_types WHERE product_id = v_product_id;
        DELETE FROM product_images WHERE product_id = v_product_id;

        INSERT INTO product_sku_registry (sku_normalized, product_id)
        VALUES (lower(btrim(v_product->>'sku')), v_product_id);
    END IF;

    -- Gallery -----------------------------------------------------------------
    FOR v_image IN SELECT * FROM jsonb_array_elements(COALESCE(payload->'images', '[]'::jsonb))
    LOOP
        INSERT INTO product_images (id, product_id, object_key, position, is_primary)
        VALUES (
            COALESCE(NULLIF(v_image->>'id', '')::uuid, gen_random_uuid()),
            v_product_id,
            v_image->>'object_key',
            (v_image->>'position')::integer,
            (v_image->>'is_primary')::boolean
        );
    END LOOP;

    -- Option types + values -----------------------------------------------------
    FOR v_type IN SELECT * FROM jsonb_array_elements(COALESCE(payload->'option_types', '[]'::jsonb))
    LOOP
        DECLARE
            v_type_id UUID := COALESCE(NULLIF(v_type->>'id', '')::uuid, gen_random_uuid());
        BEGIN
            INSERT INTO product_option_types (id, product_id, name, position)
            VALUES (v_type_id, v_product_id, v_type->>'name', (v_type->>'position')::integer);

            FOR v_value IN SELECT * FROM jsonb_array_elements(COALESCE(v_type->'values', '[]'::jsonb))
            LOOP
                INSERT INTO product_option_values (id, option_type_id, value, position)
                VALUES (
                    COALESCE(NULLIF(v_value->>'id', '')::uuid, gen_random_uuid()),
                    v_type_id,
                    v_value->>'value',
                    (v_value->>'position')::integer
                );
            END LOOP;
        END;
    END LOOP;

    -- Variants + their option-value links + SKU registry -------------------------
    FOR v_variant IN SELECT * FROM jsonb_array_elements(COALESCE(payload->'variants', '[]'::jsonb))
    LOOP
        DECLARE
            v_variant_id UUID := COALESCE(NULLIF(v_variant->>'id', '')::uuid, gen_random_uuid());
        BEGIN
            INSERT INTO product_variants (
                id, product_id, sku, original_price, sale_price, stock_quantity,
                is_available, image_id, position
            ) VALUES (
                v_variant_id,
                v_product_id,
                v_variant->>'sku',
                NULLIF(v_variant->>'original_price', '')::numeric,
                NULLIF(v_variant->>'sale_price', '')::numeric,
                NULLIF(v_variant->>'stock_quantity', '')::integer,
                (v_variant->>'is_available')::boolean,
                NULLIF(v_variant->>'image_id', '')::uuid,
                (v_variant->>'position')::integer
            );

            INSERT INTO product_sku_registry (sku_normalized, product_id, variant_id)
            VALUES (lower(btrim(v_variant->>'sku')), v_product_id, v_variant_id);

            FOR v_option_value_id_text IN
                SELECT * FROM jsonb_array_elements_text(COALESCE(v_variant->'option_value_ids', '[]'::jsonb))
            LOOP
                INSERT INTO product_variant_option_values (variant_id, option_value_id)
                VALUES (v_variant_id, v_option_value_id_text::uuid);
            END LOOP;
        END;
    END LOOP;

    PERFORM public.refresh_product_read_model(v_product_id);

    SELECT products.updated_at INTO v_updated_at FROM products WHERE products.id = v_product_id;

    RETURN QUERY SELECT v_product_id, v_updated_at;
END;
$$;

REVOKE ALL ON FUNCTION public.save_product(JSONB) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.save_product(JSONB) TO authenticated;
