-- ============================================================================
-- 0030_product_description_bounds  (TASK-023)
--
-- Bounds products.description to the Decision 3 whitelist grammar. Migrates
-- existing plain-text descriptions to escaped HTML BEFORE adding the CHECKs,
-- so no existing row is rejected.
--
-- This is the feature's only real trust boundary: Domain, Application, and
-- Infrastructure all execute in the browser under Blazor WebAssembly, so the
-- C# grammar in ProductDescription and the two command validators are
-- convenience, not enforcement. AC-11 holds here or nowhere.
--
-- Companion plan: .specs/product-description/plan.md §10
-- ============================================================================

-- Step 1 of 2: migrate legacy plain-text descriptions to escaped HTML paragraphs
-- (Decision 7). A row already carrying allowed markup is left alone.
UPDATE products
SET description = '<p>' || replace(
        regexp_replace(
            replace(replace(replace(btrim(description), '&', '&amp;'), '<', '&lt;'), '>', '&gt;'),
            E'\n{2,}', E'\n', 'g'),
        E'\n', '</p><p>') || '</p>'
WHERE btrim(description) <> ''
  AND description !~* '</?(p|br|h[1-6]|strong|em|ol|ul|li|a)\b';

-- Step 2 of 2: bound the column. Every tag token must match the plan's
-- Decision 3 grammar; nothing rewrites content, a bad row is rejected.
--
-- DEVIATION from plan §10, recorded here: the plan states the grammar CHECK
-- textually as "CHECK (NOT EXISTS (SELECT ... FROM regexp_matches(...)))".
-- PostgreSQL categorically forbids a sub-SELECT inside a CHECK constraint
-- ("cannot use subquery in check constraint", SQLSTATE 0A000). A CHECK may
-- call a function, so the identical predicate is wrapped in one below and the
-- constraint stays a single function-call expression. The Decision 3 token
-- list is unchanged, and matches ProductDescription.AllowedStructuralTag /
-- AllowedLinkClose / AllowedLinkOpen character for character.
-- SupabaseProductDescriptionSchemaTests already applies this same shape.

CREATE OR REPLACE FUNCTION public.description_markup_allowed(description TEXT)
RETURNS BOOLEAN
LANGUAGE sql
IMMUTABLE
SECURITY INVOKER
SET search_path = ''
AS $fn$
    SELECT NOT EXISTS (
        SELECT 1
        FROM regexp_matches(description, '<[^>]*>', 'g') AS m(tag)
        WHERE m.tag[1] !~* '^</?(p|br|h[1-6]|strong|em|ol|ul|li)\s*/?>$'
          AND m.tag[1] !~* '^</a>$'
          AND m.tag[1] !~* '^<a\s+href="(https?://|mailto:)[^"<>]*"(\s+target="_blank"|\s+rel="noopener noreferrer")*\s*>$'
    )
$fn$;

COMMENT ON FUNCTION public.description_markup_allowed(TEXT) IS
    'Decision 3 whitelist grammar for products.description. Backs the products_description_markup_allowed CHECK; changing it means changing ProductDescription and both command validators in the same deviation record.';

ALTER TABLE products
    ADD CONSTRAINT products_description_size
        CHECK (octet_length(description) <= 200000);

ALTER TABLE products
    ADD CONSTRAINT products_description_markup_allowed
        CHECK (public.description_markup_allowed(description));
