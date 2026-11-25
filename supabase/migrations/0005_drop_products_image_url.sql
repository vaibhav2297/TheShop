-- ============================================================================
-- 0005_drop_products_image_url
--
-- Retires the legacy `products.image_url` column. As of migration 0004,
-- `products.image_path` (a Supabase Storage object key resolved to a public URL
-- at read time) is the source of truth for product photos, with `image_url`
-- kept only as a transitional fallback. This drops that fallback so `image_path`
-- is the single image source: rows without an `image_path` render the
-- application's name-based placeholder.
--
-- Companion plan: .specs/product-catalogue/plan.md
-- ============================================================================

ALTER TABLE products
    DROP COLUMN IF EXISTS image_url;
