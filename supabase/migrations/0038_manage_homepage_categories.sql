BEGIN;

CREATE FUNCTION public.homepage_category_ids_valid(p_ids uuid[])
RETURNS boolean LANGUAGE sql IMMUTABLE SET search_path = '' AS $$
  SELECT p_ids IS NOT NULL
    AND cardinality(p_ids) = 3
    AND array_position(p_ids, NULL) IS NULL
    AND NOT ('00000000-0000-0000-0000-000000000000'::uuid = ANY(p_ids))
    AND (SELECT count(DISTINCT selected_id) = 3
         FROM unnest(p_ids) AS selected(selected_id));
$$;

CREATE TABLE public.homepage_category_section (
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
  category_ids uuid[] NOT NULL CHECK (public.homepage_category_ids_valid(category_ids)),
  is_visible boolean NOT NULL DEFAULT true
);

CREATE FUNCTION public.homepage_category_selection_active()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
BEGIN
  -- Lock selected rows through the insert/update transaction so a concurrent
  -- category deactivation or deletion cannot pass between validation and save.
  IF (SELECT count(*) FROM (
      SELECT c.id FROM public.categories c
      WHERE c.id = ANY(NEW.category_ids) AND c.is_active
      FOR SHARE
  ) selected) <> 3 THEN
    RAISE EXCEPTION 'selected category unavailable' USING ERRCODE = 'P0001';
  END IF;
  RETURN NEW;
END; $$;

CREATE TRIGGER homepage_category_selection_active
BEFORE INSERT OR UPDATE OF category_ids ON public.homepage_category_section
FOR EACH ROW EXECUTE FUNCTION public.homepage_category_selection_active();

REVOKE ALL ON public.homepage_category_section FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.homepage_category_section TO authenticated;

ALTER TABLE public.homepage_category_section ENABLE ROW LEVEL SECURITY;
CREATE POLICY homepage_category_admin_read ON public.homepage_category_section
  FOR SELECT TO authenticated USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY homepage_category_create ON public.homepage_category_section
  FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));
CREATE POLICY homepage_category_edit ON public.homepage_category_section
  FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
  WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

CREATE FUNCTION public.get_homepage_category_admin()
RETURNS SETOF public.homepage_category_section LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT s.* FROM public.homepage_category_section s WHERE s.id = 1;
END; $$;

CREATE FUNCTION public.save_homepage_category_admin(
  p_create boolean, p_title text, p_button_label text, p_button_link text,
  p_category_ids uuid[], p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
     NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.homepage_category_section
      (id, title, button_label, button_link, category_ids, is_visible)
    VALUES (1, p_title, p_button_label, p_button_link, p_category_ids, p_is_visible);
  ELSE
    UPDATE public.homepage_category_section SET title = p_title,
      button_label = p_button_label, button_link = p_button_link,
      category_ids = p_category_ids, is_visible = p_is_visible
    WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN RAISE EXCEPTION 'section not found' USING ERRCODE = 'P0002'; END IF;
  END IF;
END; $$;

REVOKE ALL ON FUNCTION public.homepage_category_ids_valid(uuid[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.homepage_category_selection_active() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_homepage_category_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_homepage_category_admin(boolean,text,text,text,uuid[],boolean) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.homepage_category_ids_valid(uuid[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_homepage_category_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_homepage_category_admin(boolean,text,text,text,uuid[],boolean) TO authenticated;

COMMIT;
