BEGIN;

CREATE TABLE public.featured_sections (
  id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  title text NOT NULL CHECK (length(btrim(title)) > 0),
  tile1_label text,
  tile1_title text NOT NULL CHECK (length(btrim(tile1_title)) > 0),
  tile1_button_label text,
  tile1_button_link text,
  tile1_image_key text NOT NULL CHECK (
    tile1_image_key ~ '^featured/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
  tile2_label text,
  tile2_title text NOT NULL CHECK (length(btrim(tile2_title)) > 0),
  tile2_button_label text,
  tile2_button_link text,
  tile2_image_key text NOT NULL CHECK (
    tile2_image_key ~ '^featured/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
  is_visible boolean NOT NULL DEFAULT true,
  CONSTRAINT featured_tile1_button_pair CHECK (
    (tile1_button_label IS NULL AND tile1_button_link IS NULL) OR
    (tile1_button_label IS NOT NULL AND length(btrim(tile1_button_label)) > 0
      AND tile1_button_link IS NOT NULL AND length(btrim(tile1_button_link)) > 0)),
  CONSTRAINT featured_tile2_button_pair CHECK (
    (tile2_button_label IS NULL AND tile2_button_link IS NULL) OR
    (tile2_button_label IS NOT NULL AND length(btrim(tile2_button_label)) > 0
      AND tile2_button_link IS NOT NULL AND length(btrim(tile2_button_link)) > 0))
);

REVOKE ALL ON public.featured_sections FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.featured_sections TO authenticated;
ALTER TABLE public.featured_sections ENABLE ROW LEVEL SECURITY;
CREATE POLICY featured_read ON public.featured_sections
  FOR SELECT TO authenticated
  USING ((SELECT public.authorize('homepage.view')));
CREATE POLICY featured_create ON public.featured_sections
  FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));
CREATE POLICY featured_edit ON public.featured_sections
  FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
  WITH CHECK ((SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

INSERT INTO storage.buckets
  (id, name, public, file_size_limit, allowed_mime_types)
VALUES ('featured-images', 'featured-images', false, 5242880,
  ARRAY['image/png', 'image/jpeg', 'image/webp']);

CREATE POLICY featured_images_read ON storage.objects
  FOR SELECT TO authenticated USING (
    bucket_id = 'featured-images' AND
    (SELECT public.authorize('homepage.view')));
CREATE POLICY featured_images_create ON storage.objects
  FOR INSERT TO authenticated WITH CHECK (
    bucket_id = 'featured-images' AND
    name ~ '^featured/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    (SELECT public.authorize('homepage.view')) AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))));
-- No UPDATE/DELETE policies for Featured objects; uploads are immutable.

CREATE FUNCTION public.featured_require_images()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER
SET search_path = public AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM storage.objects o
      WHERE o.bucket_id = 'featured-images' AND o.name = NEW.tile1_image_key)
     OR NOT EXISTS (SELECT 1 FROM storage.objects o
      WHERE o.bucket_id = 'featured-images' AND o.name = NEW.tile2_image_key) THEN
    RAISE EXCEPTION 'image unavailable' USING ERRCODE = '22023';
  END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER featured_images_required
  BEFORE INSERT OR UPDATE ON public.featured_sections
  FOR EACH ROW EXECUTE FUNCTION public.featured_require_images();

CREATE FUNCTION public.get_featured_admin()
RETURNS SETOF public.featured_sections
LANGUAGE plpgsql STABLE SECURITY INVOKER SET search_path = public AS $$
BEGIN
  IF NOT public.authorize('homepage.view') THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  RETURN QUERY SELECT f.* FROM public.featured_sections f WHERE f.id = 1;
END;
$$;

CREATE FUNCTION public.save_featured_admin(
  p_create boolean, p_title text,
  p_tile1_label text, p_tile1_title text, p_tile1_button_label text,
  p_tile1_button_link text, p_tile1_image_key text,
  p_tile2_label text, p_tile2_title text, p_tile2_button_label text,
  p_tile2_button_link text, p_tile2_image_key text, p_is_visible boolean)
RETURNS void LANGUAGE plpgsql SECURITY INVOKER SET search_path = public AS $$
DECLARE affected integer;
BEGIN
  IF p_create IS NULL OR NOT public.authorize('homepage.view') OR
    (p_create IS TRUE AND NOT public.authorize('homepage.create')) OR
    (p_create IS FALSE AND NOT public.authorize('homepage.edit')) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  IF p_create THEN
    INSERT INTO public.featured_sections
      (id, title, tile1_label, tile1_title, tile1_button_label, tile1_button_link,
       tile1_image_key, tile2_label, tile2_title, tile2_button_label,
       tile2_button_link, tile2_image_key, is_visible)
    VALUES (1, p_title, p_tile1_label, p_tile1_title, p_tile1_button_label,
      p_tile1_button_link, p_tile1_image_key, p_tile2_label, p_tile2_title,
      p_tile2_button_label, p_tile2_button_link, p_tile2_image_key, p_is_visible);
  ELSE
    UPDATE public.featured_sections SET title = p_title,
      tile1_label = p_tile1_label, tile1_title = p_tile1_title,
      tile1_button_label = p_tile1_button_label,
      tile1_button_link = p_tile1_button_link, tile1_image_key = p_tile1_image_key,
      tile2_label = p_tile2_label, tile2_title = p_tile2_title,
      tile2_button_label = p_tile2_button_label,
      tile2_button_link = p_tile2_button_link, tile2_image_key = p_tile2_image_key,
      is_visible = p_is_visible WHERE id = 1;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected = 0 THEN
      RAISE EXCEPTION 'featured not found' USING ERRCODE = 'P0002';
    END IF;
  END IF;
END;
$$;

REVOKE ALL ON FUNCTION public.featured_require_images() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.get_featured_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_featured_admin(
  boolean, text, text, text, text, text, text,
  text, text, text, text, text, boolean) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_featured_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_featured_admin(
  boolean, text, text, text, text, text, text,
  text, text, text, text, text, boolean) TO authenticated;

COMMIT;
