BEGIN;

CREATE FUNCTION public.homepage_brand_ids_valid(p_ids uuid[], p_visible boolean)
RETURNS boolean LANGUAGE sql IMMUTABLE SET search_path = '' AS $$
  SELECT p_ids IS NOT NULL AND p_visible IS NOT NULL
    AND cardinality(p_ids) <= 6
    AND (NOT p_visible OR cardinality(p_ids) >= 1)
    AND array_position(p_ids, NULL) IS NULL
    AND NOT ('00000000-0000-0000-0000-000000000000'::uuid = ANY(p_ids))
    AND (SELECT count(DISTINCT selected_id) = cardinality(p_ids)
         FROM unnest(p_ids) AS selected(selected_id));
$$;

CREATE TABLE public.homepage_brand_section (
  id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  title text NOT NULL CHECK (length(btrim(title)) > 0),
  button_label text NOT NULL CHECK (length(btrim(button_label)) > 0),
  button_link text NOT NULL CHECK (
    length(btrim(button_link)) > 0
    AND button_link !~ '[[:space:]]'
    AND position(chr(92) in button_link) = 0
    AND ((left(button_link, 1) = '/' AND left(button_link, 2) <> '//')
      OR left(lower(button_link), 7) = 'http://'
      OR left(lower(button_link), 8) = 'https://')),
  brand_ids uuid[] NOT NULL,
  is_visible boolean NOT NULL DEFAULT true,
  CONSTRAINT homepage_brand_selection_valid CHECK (public.homepage_brand_ids_valid(brand_ids, is_visible))
);

CREATE FUNCTION public.homepage_brand_selection_active()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF (SELECT count(*) FROM (
      SELECT b.id FROM public.brands b
      WHERE b.id = ANY(NEW.brand_ids) AND b.is_active
      FOR SHARE
  ) selected) <> cardinality(NEW.brand_ids) THEN
    RAISE EXCEPTION 'selected brand unavailable' USING ERRCODE = 'P0001';
  END IF;
  RETURN NEW;
END; $$;

CREATE TRIGGER homepage_brand_selection_active
BEFORE INSERT OR UPDATE ON public.homepage_brand_section
FOR EACH ROW EXECUTE FUNCTION public.homepage_brand_selection_active();

REVOKE ALL ON public.homepage_brand_section FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.homepage_brand_section TO authenticated;
ALTER TABLE public.homepage_brand_section ENABLE ROW LEVEL SECURITY;
CREATE POLICY homepage_brand_admin_read ON public.homepage_brand_section
  FOR SELECT TO authenticated USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY homepage_brand_create ON public.homepage_brand_section
  FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));
CREATE POLICY homepage_brand_edit ON public.homepage_brand_section
  FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
  WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

CREATE FUNCTION public.get_homepage_brand_admin()
RETURNS SETOF public.homepage_brand_section LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT s.* FROM public.homepage_brand_section s WHERE s.id = 1;
END; $$;

CREATE FUNCTION public.save_homepage_brand_admin(
  p_create boolean, p_title text, p_button_label text, p_button_link text,
  p_brand_ids uuid[], p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
     NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.homepage_brand_section
      (id, title, button_label, button_link, brand_ids, is_visible)
    VALUES (1, p_title, p_button_label, p_button_link, p_brand_ids, p_is_visible);
  ELSE
    UPDATE public.homepage_brand_section SET title = p_title,
      button_label = p_button_label, button_link = p_button_link,
      brand_ids = p_brand_ids, is_visible = p_is_visible WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN RAISE EXCEPTION 'section not found' USING ERRCODE = 'P0002'; END IF;
  END IF;
END; $$;

REVOKE ALL ON FUNCTION public.homepage_brand_ids_valid(uuid[],boolean) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.homepage_brand_selection_active() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_homepage_brand_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_homepage_brand_admin(boolean,text,text,text,uuid[],boolean) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.homepage_brand_ids_valid(uuid[],boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_homepage_brand_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_homepage_brand_admin(boolean,text,text,text,uuid[],boolean) TO authenticated;

COMMIT;
