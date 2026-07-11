-- ============================================================================
-- 0004_create_product_images_bucket
--
-- Introduces Supabase Storage as the source of truth for product photos so the
-- (future) admin panel can upload images instead of relying on external URLs.
--
--   • Creates a PUBLIC bucket `product-images` — product photos are public
--     storefront content, so objects are served via stable public URLs with no
--     per-request signing.
--   • Storefront reads go through the public object URL (no RLS). SELECT on
--     storage.objects and every write (INSERT/UPDATE/DELETE) are gated to the
--     admin role only (`auth.jwt() ->> 'role' = 'admin'`), mirroring the admin
--     gate already used on `customers` (see 0001) and the write-denied posture
--     of `products` (see 0002).
--   • Adds `products.image_path` — the storage OBJECT KEY (e.g.
--     `products/{productId}/{guid}.webp`), NOT a full URL. The Infrastructure
--     mapper resolves the key to a public URL at read time, keeping rows
--     portable across environments (dev/prod project refs differ).
--
-- The legacy `products.image_url` column is retained as a fallback so existing
-- seed rows keep rendering; `image_path` takes precedence when present. It can
-- be dropped once all rows are migrated to uploaded images.
--
-- Companion plan: .specs/product-catalogue/plan.md
-- ============================================================================

-- ============================================================================
-- Storage bucket
-- ============================================================================
INSERT INTO storage.buckets (id, name, public)
VALUES ('product-images', 'product-images', true)
ON CONFLICT (id) DO NOTHING;

-- ============================================================================
-- Storage RLS — storage.objects already has RLS enabled by Supabase.
--   • Public object reads DON'T need a policy: a public bucket serves objects at
--     /storage/v1/object/public/... without RLS, which is how the storefront
--     renders them. Granting anon a broad SELECT on storage.objects would only
--     let clients ENUMERATE the whole bucket (advisor 0025), so we don't.
--   • Admin-only SELECT: scoped to role = 'admin' for the admin panel's future
--     listing/management needs — not the storefront.
--   • Admin-only write: only a caller whose JWT carries role = 'admin' may
--     upload, replace, or remove objects. auth.jwt() is wrapped in (select ...)
--     so Postgres evaluates it once per statement, not once per row.
-- ============================================================================
CREATE POLICY "product_images_admin_read" ON storage.objects
    FOR SELECT
    USING (
        bucket_id = 'product-images'
        AND (SELECT auth.jwt() ->> 'role') = 'admin'
    );

CREATE POLICY "product_images_admin_insert" ON storage.objects
    FOR INSERT
    WITH CHECK (
        bucket_id = 'product-images'
        AND (SELECT auth.jwt() ->> 'role') = 'admin'
    );

CREATE POLICY "product_images_admin_update" ON storage.objects
    FOR UPDATE
    USING (
        bucket_id = 'product-images'
        AND (SELECT auth.jwt() ->> 'role') = 'admin'
    )
    WITH CHECK (
        bucket_id = 'product-images'
        AND (SELECT auth.jwt() ->> 'role') = 'admin'
    );

CREATE POLICY "product_images_admin_delete" ON storage.objects
    FOR DELETE
    USING (
        bucket_id = 'product-images'
        AND (SELECT auth.jwt() ->> 'role') = 'admin'
    );

-- ============================================================================
-- products.image_path — storage object key (source of truth going forward)
-- ============================================================================
ALTER TABLE products
    ADD COLUMN IF NOT EXISTS image_path TEXT;
