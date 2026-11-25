-- 0027_add_products_display_price.sql
--
-- The storefront filters, sorts, and reports its price bounds from a single column on `products`.
-- That column was `effective_price`, which only knows the product's own price — and a product with
-- variants has none, because customers pay the price of the variant they choose (RULE-19). Those
-- products therefore fell out of every price-range filter, sorted as NULL under "Price low to high",
-- and never counted toward the filter panel's min/max.
--
-- `display_price` closes that by falling back to `min_variant_price`, the trigger-maintained lowest
-- effective variant price the catalogue tile already displays. The expression spells out
-- `sale_price` and `original_price` rather than reusing `effective_price`: Postgres does not allow a
-- generated column to reference another generated column. `min_variant_price` is an ordinary column
-- maintained by `refresh_product_variant_rollups`, so each rollup UPDATE recomputes this alongside.
--
-- `effective_price` is left in place — it still correctly answers "what does this product itself
-- cost", which is the right question everywhere outside the catalogue's price axis.

alter table public.products
    add column if not exists display_price numeric(10, 2)
        generated always as (coalesce(sale_price, original_price, min_variant_price)) stored;

comment on column public.products.display_price is
    'The price the catalogue filters, sorts, and bounds on: the product''s own effective price, or its lowest variant price when it has variants.';

create index if not exists products_display_price_idx
    on public.products (display_price);

-- The filter panel's bounds have to span variant products too, or the slider cannot reach them.
create or replace function public.get_catalogue_filters()
returns jsonb
language sql
stable
set search_path to ''
as $function$
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
        'price_min', COALESCE((SELECT min(display_price) FROM public.products WHERE is_published = true), 0),
        'price_max', COALESCE((SELECT max(display_price) FROM public.products WHERE is_published = true), 0)
    );
$function$;
