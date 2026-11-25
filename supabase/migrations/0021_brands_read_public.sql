-- ============================================================================
-- 0021_brands_read_public
--
-- brands_read has been USING (is_active OR authorize('brands.view')) since migration 0013. That
-- predicate nulls out ProductRecord's embedded BrandRecord for any published product whose brand
-- is Inactive, and ProductMapper.ToDomain throws on a null embed — so deactivating a brand breaks
-- catalogue rendering for its products. The customer-facing goal 0013 stated ("Inactive brands
-- must be invisible to customers") is already met by get_catalogue_filters()' `WHERE b.is_active`
-- brand facet, added in 0012. Revert the predicate; keep the policy name.
--
-- Companion plan: .specs/manage-categories/plan.md §5 Decision 13, §10.
-- ============================================================================

DROP POLICY IF EXISTS "brands_read" ON brands;

CREATE POLICY "brands_read" ON brands
    FOR SELECT USING (true);
