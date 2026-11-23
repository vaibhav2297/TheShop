BEGIN;

ALTER TABLE public.product_images
  ADD CONSTRAINT product_images_id_product_unique UNIQUE (id, product_id);
ALTER TABLE public.product_variants
  ADD CONSTRAINT product_variants_id_product_unique UNIQUE (id, product_id);

CREATE TABLE public.product_variant_images (
  product_id uuid NOT NULL REFERENCES public.products(id) ON DELETE CASCADE,
  variant_id uuid NOT NULL,
  image_id uuid NOT NULL,
  position integer NOT NULL CHECK (position >= 0),
  PRIMARY KEY (variant_id, image_id),
  UNIQUE (variant_id, position),
  FOREIGN KEY (variant_id, product_id)
    REFERENCES public.product_variants(id, product_id) ON DELETE CASCADE,
  FOREIGN KEY (image_id, product_id)
    REFERENCES public.product_images(id, product_id) ON DELETE CASCADE
);
CREATE INDEX product_variant_images_product_idx
  ON public.product_variant_images(product_id);
CREATE INDEX product_variant_images_image_idx
  ON public.product_variant_images(image_id);

CREATE TABLE public.product_routes (
  slug text PRIMARY KEY CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  product_id uuid UNIQUE REFERENCES public.products(id) ON DELETE SET NULL
);
CREATE TABLE public.product_variant_routes (
  product_slug text NOT NULL REFERENCES public.product_routes(slug),
  slug text NOT NULL CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  variant_id uuid UNIQUE REFERENCES public.product_variants(id) ON DELETE SET NULL,
  PRIMARY KEY (product_slug, slug)
);

ALTER TABLE public.product_variant_images ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.product_routes ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.product_variant_routes ENABLE ROW LEVEL SECURITY;

CREATE POLICY variant_images_read ON public.product_variant_images FOR SELECT
USING (EXISTS (SELECT 1 FROM public.products p
  WHERE p.id = product_id AND p.is_published)
  OR (SELECT public.authorize('products.view')));
CREATE POLICY product_routes_read ON public.product_routes FOR SELECT
USING (product_id IS NOT NULL AND EXISTS (SELECT 1 FROM public.products p
  WHERE p.id = product_id AND
    (p.is_published OR (SELECT public.authorize('products.view')))));
CREATE POLICY variant_routes_read ON public.product_variant_routes FOR SELECT
USING (variant_id IS NOT NULL AND EXISTS (
  SELECT 1 FROM public.product_variants v
  JOIN public.products p ON p.id = v.product_id
  JOIN public.product_routes r ON r.product_id = p.id
  WHERE v.id = variant_id AND r.slug = product_slug AND
    (p.is_published OR (SELECT public.authorize('products.view')))));

REVOKE ALL ON public.product_variant_images, public.product_routes,
  public.product_variant_routes FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.product_variant_images, public.product_routes,
  public.product_variant_routes TO anon, authenticated;

CREATE FUNCTION public.product_slug_base_007(label text, fallback text)
RETURNS text LANGUAGE sql IMMUTABLE SET search_path = public AS $$
  SELECT COALESCE(NULLIF(trim(both '-' FROM left(
    regexp_replace(lower(COALESCE(label, '')), '[^a-z0-9]+', '-', 'g'), 80)), ''), fallback);
$$;

CREATE FUNCTION public.ensure_product_routes_007(target uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
  parent_slug text;
  base text;
  candidate text;
  suffix integer;
  row_count integer;
  item record;
BEGIN
  SELECT r.slug INTO parent_slug FROM public.product_routes r WHERE r.product_id = target;
  IF parent_slug IS NULL THEN
    SELECT public.product_slug_base_007(p.name, 'product') INTO base
      FROM public.products p WHERE p.id = target;
    IF base IS NULL THEN RAISE EXCEPTION 'product not found'; END IF;
    IF base ~ '^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$' THEN
      base := base || '-product';
    END IF;
    suffix := 1;
    LOOP
      candidate := base || CASE WHEN suffix = 1 THEN '' ELSE '-' || suffix::text END;
      INSERT INTO public.product_routes(slug, product_id)
        VALUES (candidate, target) ON CONFLICT DO NOTHING;
      GET DIAGNOSTICS row_count = ROW_COUNT;
      EXIT WHEN row_count = 1;
      suffix := suffix + 1;
    END LOOP;
    parent_slug := candidate;
  END IF;
  FOR item IN
    SELECT v.id, string_agg(ov.value, '-' ORDER BY ot.position, ov.position, ov.id) AS label
    FROM public.product_variants v
    LEFT JOIN public.product_variant_option_values l ON l.variant_id = v.id
    LEFT JOIN public.product_option_values ov ON ov.id = l.option_value_id
    LEFT JOIN public.product_option_types ot ON ot.id = ov.option_type_id
    WHERE v.product_id = target AND NOT EXISTS (
      SELECT 1 FROM public.product_variant_routes r WHERE r.variant_id = v.id)
    GROUP BY v.id, v.position ORDER BY v.position, v.id
  LOOP
    base := public.product_slug_base_007(item.label, 'variant');
    suffix := 1;
    LOOP
      candidate := base || CASE WHEN suffix = 1 THEN '' ELSE '-' || suffix::text END;
      INSERT INTO public.product_variant_routes(product_slug, slug, variant_id)
        VALUES (parent_slug, candidate, item.id) ON CONFLICT DO NOTHING;
      GET DIAGNOSTICS row_count = ROW_COUNT;
      EXIT WHEN row_count = 1;
      suffix := suffix + 1;
    END LOOP;
  END LOOP;
END;
$$;

INSERT INTO public.product_variant_images(product_id, variant_id, image_id, position)
SELECT v.product_id, v.id, i.id, 0
FROM public.product_variants v
JOIN public.product_images i ON i.id = v.image_id AND i.product_id = v.product_id;

DO $$
DECLARE item record;
BEGIN
  FOR item IN SELECT id FROM public.products ORDER BY id LOOP
    PERFORM public.ensure_product_routes_007(item.id);
  END LOOP;
END;
$$;

ALTER FUNCTION public.save_product(jsonb) RENAME TO save_product_core_007;
REVOKE ALL ON FUNCTION public.save_product_core_007(jsonb) FROM PUBLIC, anon, authenticated;

CREATE FUNCTION public.save_product(payload jsonb)
RETURNS TABLE(id uuid, updated_at timestamptz)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
  target uuid := (payload->'product'->>'id')::uuid;
  mode text := payload->>'mode';
  old_routes jsonb;
  old_images jsonb;
  item jsonb;
  images jsonb;
  variant uuid;
  saved_version timestamptz;
BEGIN
  IF mode IS NULL OR mode NOT IN ('create', 'update') THEN
    RAISE EXCEPTION 'invalid product save mode' USING ERRCODE = '22023';
  END IF;
  IF (mode = 'create' AND NOT COALESCE(public.authorize('products.create'), false))
    OR (mode = 'update' AND NOT COALESCE(public.authorize('products.edit'), false)) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  -- Serialize existing-product snapshots with its optimistic save check.
  PERFORM 1 FROM public.products p WHERE p.id = target FOR UPDATE;
  SELECT COALESCE(jsonb_agg(jsonb_build_object(
    'product_slug', r.product_slug, 'slug', r.slug, 'variant_id', r.variant_id)), '[]'::jsonb)
  INTO old_routes FROM public.product_variant_routes r
  JOIN public.product_variants v ON v.id = r.variant_id WHERE v.product_id = target;
  SELECT COALESCE(jsonb_object_agg(q.variant_id::text, q.images), '{}'::jsonb)
  INTO old_images FROM (
    SELECT v.id AS variant_id, COALESCE(jsonb_agg(a.image_id ORDER BY a.position)
      FILTER (WHERE a.image_id IS NOT NULL), '[]'::jsonb) AS images
    FROM public.product_variants v
    LEFT JOIN public.product_variant_images a ON a.variant_id = v.id
    WHERE v.product_id = target GROUP BY v.id
  ) q;
  -- New clients send explicit IDs; core must not silently mint uncorrelated IDs.
  FOR item IN SELECT value FROM jsonb_array_elements(COALESCE(payload->'variants', '[]'::jsonb)) LOOP
    IF NULLIF(item->>'id', '') IS NULL THEN
      RAISE EXCEPTION 'variant id required' USING ERRCODE = '22023';
    END IF;
  END LOOP;
  SELECT c.updated_at INTO saved_version FROM public.save_product_core_007(payload) c;
  FOR item IN SELECT value FROM jsonb_array_elements(old_routes) LOOP
    UPDATE public.product_variant_routes r SET variant_id = (item->>'variant_id')::uuid
    WHERE r.product_slug = item->>'product_slug' AND r.slug = item->>'slug'
      AND EXISTS (SELECT 1 FROM public.product_variants v
        WHERE v.id = (item->>'variant_id')::uuid AND v.product_id = target);
  END LOOP;
  PERFORM public.ensure_product_routes_007(target);
  FOR item IN SELECT value FROM jsonb_array_elements(COALESCE(payload->'variants', '[]'::jsonb)) LOOP
    variant := (item->>'id')::uuid;
    IF item ? 'image_ids' THEN
      images := item->'image_ids';
      IF jsonb_typeof(images) IS DISTINCT FROM 'array' THEN
        RAISE EXCEPTION 'invalid variant image selection' USING ERRCODE = '22023';
      END IF;
    ELSE
      -- Old app saves preserve existing assignments; newly introduced legacy pins seed one image.
      images := COALESCE(old_images->(variant::text),
        CASE WHEN NULLIF(item->>'image_id', '') IS NULL THEN '[]'::jsonb
          ELSE jsonb_build_array(item->>'image_id') END);
      SELECT COALESCE(jsonb_agg(e.value ORDER BY e.ordinality), '[]'::jsonb) INTO images
      FROM jsonb_array_elements_text(images) WITH ORDINALITY e(value, ordinality)
      JOIN public.product_images i ON i.id = e.value::uuid AND i.product_id = target;
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements_text(images) e(value)
      WHERE e.value IS NULL OR NOT EXISTS (SELECT 1 FROM public.product_images i
        WHERE i.id = e.value::uuid AND i.product_id = target)) THEN
      RAISE EXCEPTION 'variant image not in gallery' USING ERRCODE = '23503';
    END IF;
    IF (SELECT count(*) <> count(DISTINCT e.value::uuid)
        FROM jsonb_array_elements_text(images) e(value)) THEN
      RAISE EXCEPTION 'duplicate variant image' USING ERRCODE = '23505';
    END IF;
    INSERT INTO public.product_variant_images(product_id, variant_id, image_id, position)
    SELECT target, variant, e.value::uuid, (e.ordinality - 1)::integer
      FROM jsonb_array_elements_text(images) WITH ORDINALITY e(value, ordinality);
    UPDATE public.product_variants v
      SET image_id = (images->>0)::uuid WHERE v.id = variant AND v.product_id = target;
  END LOOP;
  SELECT p.updated_at INTO saved_version FROM public.products p WHERE p.id = target;
  RETURN QUERY SELECT target, saved_version;
END;
$$;

REVOKE ALL ON FUNCTION public.product_slug_base_007(text, text),
  public.ensure_product_routes_007(uuid), public.save_product(jsonb)
  FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.save_product(jsonb) TO authenticated;
COMMIT;
