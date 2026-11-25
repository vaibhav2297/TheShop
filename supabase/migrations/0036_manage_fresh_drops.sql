BEGIN;

CREATE FUNCTION public.fresh_drops_manual_ids_valid(p_ids uuid[])
RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
  SELECT p_ids IS NOT NULL AND cardinality(p_ids) <= 3
    AND array_position(p_ids, NULL) IS NULL
    AND (SELECT count(DISTINCT x) = cardinality(p_ids) FROM unnest(p_ids) AS x);
$$;

CREATE TABLE public.fresh_drops (
  id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  title text NOT NULL CHECK (length(btrim(title)) > 0),
  button_label text,
  button_link text,
  selection_mode text NOT NULL CHECK (selection_mode IN ('newest', 'manual')),
  manual_product_ids uuid[] NOT NULL DEFAULT '{}'::uuid[]
    CHECK (public.fresh_drops_manual_ids_valid(manual_product_ids)),
  CONSTRAINT fresh_drops_newest_has_no_manual_ids CHECK (
    selection_mode <> 'newest' OR cardinality(manual_product_ids) = 0),
  is_visible boolean NOT NULL DEFAULT true,
  CONSTRAINT fresh_drops_button_pair CHECK (
    (button_label IS NULL AND button_link IS NULL) OR
    (button_label IS NOT NULL AND length(btrim(button_label)) > 0 AND
     button_link IS NOT NULL AND length(btrim(button_link)) > 0)
  )
);

REVOKE ALL ON public.fresh_drops FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.fresh_drops TO authenticated;
ALTER TABLE public.fresh_drops ENABLE ROW LEVEL SECURITY;
CREATE POLICY fresh_drops_read ON public.fresh_drops FOR SELECT TO authenticated
  USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY fresh_drops_create ON public.fresh_drops FOR INSERT TO authenticated
  WITH CHECK ((SELECT public.authorize('homepage.view')) AND
              (SELECT public.authorize('homepage.create')));
CREATE POLICY fresh_drops_edit ON public.fresh_drops FOR UPDATE TO authenticated
  USING ((SELECT public.authorize('homepage.view')) AND
         (SELECT public.authorize('homepage.edit')))
  WITH CHECK ((SELECT public.authorize('homepage.view')) AND
              (SELECT public.authorize('homepage.edit')));

CREATE FUNCTION public.get_fresh_drops_admin()
RETURNS SETOF public.fresh_drops LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT f.* FROM public.fresh_drops f WHERE f.id = 1;
END; $$;

CREATE FUNCTION public.save_fresh_drops_admin(
  p_create boolean, p_title text, p_button_label text, p_button_link text,
  p_selection_mode text, p_manual_product_ids uuid[], p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
     NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.fresh_drops
      (id, title, button_label, button_link, selection_mode, manual_product_ids, is_visible)
    VALUES (1, p_title, p_button_label, p_button_link, p_selection_mode,
            p_manual_product_ids, p_is_visible);
  ELSE
    UPDATE public.fresh_drops SET title = p_title, button_label = p_button_label,
      button_link = p_button_link, selection_mode = p_selection_mode,
      manual_product_ids = p_manual_product_ids, is_visible = p_is_visible
    WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN RAISE EXCEPTION 'fresh drops not found' USING ERRCODE = 'P0002'; END IF;
  END IF;
END; $$;

CREATE FUNCTION public.fresh_drops_product_cards(
  p_ids uuid[] DEFAULT NULL, p_search text DEFAULT NULL,
  p_limit integer DEFAULT 24, p_offset integer DEFAULT 0)
RETURNS TABLE (id uuid, name text, brand_name text, display_price numeric,
               currency text, image_key text, created_at timestamptz)
LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_limit IS NULL OR p_limit < 1 OR p_limit > 48 OR
     p_offset IS NULL OR p_offset < 0 THEN
    RAISE EXCEPTION 'invalid page' USING ERRCODE = '22023';
  END IF;
  RETURN QUERY
    SELECT p.id, p.name, b.name, p.display_price, p.currency,
           img.object_key, p.created_at
    FROM public.products p
    JOIN public.brands b ON b.id = p.brand_id
    LEFT JOIN LATERAL (
      SELECT pi.object_key FROM public.product_images pi
      WHERE pi.product_id = p.id
      ORDER BY pi.is_primary DESC, pi.position ASC, pi.id ASC LIMIT 1
    ) img ON true
    WHERE p.is_published = true
      AND (p_ids IS NULL OR p.id = ANY(p_ids))
      AND (NULLIF(btrim(p_search), '') IS NULL OR
           p.name ILIKE '%' || btrim(p_search) || '%' OR
           b.name ILIKE '%' || btrim(p_search) || '%')
    ORDER BY p.created_at DESC, p.id ASC
    LIMIT p_limit OFFSET p_offset;
END; $$;

REVOKE ALL ON FUNCTION public.fresh_drops_manual_ids_valid(uuid[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_fresh_drops_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_fresh_drops_admin(boolean,text,text,text,text,uuid[],boolean) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.fresh_drops_product_cards(uuid[],text,integer,integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.fresh_drops_manual_ids_valid(uuid[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_fresh_drops_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_fresh_drops_admin(boolean,text,text,text,text,uuid[],boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.fresh_drops_product_cards(uuid[],text,integer,integer) TO authenticated;

COMMIT;
