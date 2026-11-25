-- ============================================================================
-- 0015_manage_brands
--
-- Manage-brands admin capability (.specs/manage-brands/plan.md §10). Retires
-- Brand.Slug end to end (spec FR-9, Decision 6) and adds the two
-- SECURITY DEFINER RPCs the RLS-blind product-count/delete checks need
-- (RULE-6, RULE-13; Decisions 2/3). No RLS policy changes — all four
-- `brands` policies already exist and were verified against the live
-- database.
-- ============================================================================

-- Retire the brand's name-derived identifier (spec FR-9, Decision 6). Dropping the column
-- drops the brands_slug_key UNIQUE index with it, so a rename can no longer be refused for a
-- slug clash (AC-28). Migration 0002 seeds brands via `WHERE slug = …`, but 0002 always runs
-- before this migration on a replay, so its seed stays valid.
ALTER TABLE brands DROP COLUMN IF EXISTS slug;

CREATE INDEX IF NOT EXISTS idx_brands_name_lower ON brands (lower(name));

-- Authoritative per-brand product counts. SECURITY DEFINER because products' only SELECT
-- policy is products_public_read USING (is_published = true) — an unpublished product must
-- still count against deletion (spec RULE-6), and no client role can see it.
CREATE OR REPLACE FUNCTION public.brand_product_counts(brand_ids UUID[])
RETURNS TABLE (brand_id UUID, product_count BIGINT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('brands.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    SELECT b.id, count(p.id)
    FROM brands b
    LEFT JOIN products p ON p.brand_id = b.id
    WHERE b.id = ANY(brand_ids)
    GROUP BY b.id;
END;
$$;

-- Atomic partial-success deletion (spec RULE-13). Counting and deleting in one statement
-- closes the TOCTOU window a client-side guard would leave open.
CREATE OR REPLACE FUNCTION public.delete_brands(brand_ids UUID[])
RETURNS TABLE (id UUID, name TEXT, logo_path TEXT, product_count BIGINT, deleted BOOLEAN)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('brands.delete') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH candidates AS (
        SELECT b.id, b.name, b.logo_path,
               (SELECT count(*) FROM products p WHERE p.brand_id = b.id) AS product_count
        FROM brands b
        WHERE b.id = ANY(brand_ids)
    ),
    removed AS (
        DELETE FROM brands
        WHERE brands.id IN (SELECT c.id FROM candidates c WHERE c.product_count = 0)
        RETURNING brands.id
    )
    SELECT c.id, c.name, c.logo_path, c.product_count,
           EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id)
    FROM candidates c;
END;
$$;

REVOKE ALL ON FUNCTION public.brand_product_counts(UUID[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.delete_brands(UUID[])        FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.brand_product_counts(UUID[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_brands(UUID[])        TO authenticated;
