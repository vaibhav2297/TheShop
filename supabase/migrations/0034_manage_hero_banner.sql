BEGIN;

INSERT INTO public.permissions (code, module) VALUES
    ('homepage.view', 'homepage'),
    ('homepage.create', 'homepage'),
    ('homepage.edit', 'homepage')
ON CONFLICT (code) DO NOTHING;

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM public.roles r
JOIN public.permissions p ON p.module = 'homepage'
WHERE r.name_key IN ('Admin', 'SuperAdmin')
   OR (r.name_key = 'Support' AND p.code = 'homepage.view')
ON CONFLICT (role_id, permission_id) DO NOTHING;

CREATE TABLE public.hero_banners (
    id SMALLINT PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    heading TEXT NOT NULL CHECK (length(btrim(heading)) > 0),
    description TEXT,
    button_label TEXT,
    button_link TEXT,
    desktop_image_key TEXT NOT NULL CHECK (
        desktop_image_key ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
    mobile_image_key TEXT CHECK (mobile_image_key IS NULL OR
        mobile_image_key ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$'),
    is_visible BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT hero_banner_button_pair CHECK (
        (button_label IS NULL AND button_link IS NULL) OR
        (button_label IS NOT NULL AND length(btrim(button_label)) > 0 AND
         button_link IS NOT NULL AND length(btrim(button_link)) > 0))
);

REVOKE ALL ON public.hero_banners FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON public.hero_banners TO authenticated;
ALTER TABLE public.hero_banners ENABLE ROW LEVEL SECURITY;

CREATE POLICY hero_banners_read ON public.hero_banners
FOR SELECT TO authenticated
USING ((SELECT public.authorize('homepage.view')));

CREATE POLICY hero_banners_create ON public.hero_banners
FOR INSERT TO authenticated WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.create')));

CREATE POLICY hero_banners_edit ON public.hero_banners
FOR UPDATE TO authenticated USING (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')))
WITH CHECK (
    (SELECT public.authorize('homepage.view')) AND
    (SELECT public.authorize('homepage.edit')));

INSERT INTO storage.buckets
    (id, name, public, file_size_limit, allowed_mime_types)
VALUES ('hero-banner-images', 'hero-banner-images', FALSE, 5242880,
        ARRAY['image/png', 'image/jpeg', 'image/webp'])
ON CONFLICT (id) DO UPDATE SET public = FALSE,
    file_size_limit = EXCLUDED.file_size_limit,
    allowed_mime_types = EXCLUDED.allowed_mime_types;

CREATE POLICY hero_images_read ON storage.objects
FOR SELECT TO authenticated USING (
    bucket_id = 'hero-banner-images' AND
    (SELECT public.authorize('homepage.view')));

CREATE POLICY hero_images_create ON storage.objects
FOR INSERT TO authenticated WITH CHECK (
    bucket_id = 'hero-banner-images' AND
    name ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    (SELECT public.authorize('homepage.view')) AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))));

CREATE POLICY hero_images_discard_uncommitted ON storage.objects
FOR DELETE TO authenticated USING (
    bucket_id = 'hero-banner-images' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    (SELECT public.authorize('homepage.view')) AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))) AND
    NOT EXISTS (SELECT 1 FROM public.hero_banners h
        WHERE h.desktop_image_key = storage.objects.name
           OR h.mobile_image_key = storage.objects.name));

CREATE FUNCTION public.get_hero_banner_admin()
RETURNS SETOF public.hero_banners
LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('homepage.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN QUERY SELECT h.* FROM public.hero_banners h WHERE h.id = 1;
END;
$$;

CREATE FUNCTION public.save_hero_banner_admin(
    p_create BOOLEAN, p_heading TEXT, p_description TEXT,
    p_button_label TEXT, p_button_link TEXT,
    p_desktop_image_key TEXT, p_mobile_image_key TEXT,
    p_is_visible BOOLEAN)
RETURNS VOID LANGUAGE plpgsql SECURITY INVOKER
SET search_path = public AS $$
DECLARE affected INTEGER;
BEGIN
    IF NOT public.authorize('homepage.view') OR
       (p_create IS TRUE AND NOT public.authorize('homepage.create')) OR
       (p_create IS FALSE AND NOT public.authorize('homepage.edit')) OR
       p_create IS NULL THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM storage.objects o
        WHERE o.bucket_id = 'hero-banner-images'
          AND o.name = p_desktop_image_key) OR
       (p_mobile_image_key IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM storage.objects o
        WHERE o.bucket_id = 'hero-banner-images'
          AND o.name = p_mobile_image_key)) THEN
        RAISE EXCEPTION 'image unavailable' USING ERRCODE = '22023';
    END IF;
    IF p_create THEN
        INSERT INTO public.hero_banners
            (id, heading, description, button_label, button_link,
             desktop_image_key, mobile_image_key, is_visible)
        VALUES (1, p_heading, p_description, p_button_label, p_button_link,
                p_desktop_image_key, p_mobile_image_key, p_is_visible);
    ELSE
        UPDATE public.hero_banners SET heading = p_heading,
            description = p_description, button_label = p_button_label,
            button_link = p_button_link,
            desktop_image_key = p_desktop_image_key,
            mobile_image_key = p_mobile_image_key,
            is_visible = p_is_visible
        WHERE id = 1;
        GET DIAGNOSTICS affected = ROW_COUNT;
        IF affected = 0 THEN
            RAISE EXCEPTION 'hero banner not found' USING ERRCODE = 'P0002';
        END IF;
    END IF;
END;
$$;

CREATE FUNCTION public.count_hero_banner_admin()
RETURNS BIGINT LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('homepage.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN (SELECT count(*) FROM public.hero_banners);
END;
$$;

REVOKE ALL ON FUNCTION public.get_hero_banner_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.count_hero_banner_admin() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.save_hero_banner_admin(
    BOOLEAN, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, BOOLEAN) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_hero_banner_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.count_hero_banner_admin() TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_hero_banner_admin(
    BOOLEAN, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, BOOLEAN) TO authenticated;

COMMIT;
