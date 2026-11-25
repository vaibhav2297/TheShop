BEGIN;

-- View controls management reads. Each write requires only its action permission.
-- Save preparation and PostgreSQL UPDATE visibility need read access for writers.
ALTER POLICY hero_banners_read ON public.hero_banners USING (
    (SELECT public.authorize('homepage.view')) OR
    (SELECT public.authorize('homepage.create')) OR
    (SELECT public.authorize('homepage.edit')));

ALTER POLICY hero_banners_create ON public.hero_banners
WITH CHECK ((SELECT public.authorize('homepage.create')));

ALTER POLICY hero_banners_edit ON public.hero_banners
USING ((SELECT public.authorize('homepage.edit')))
WITH CHECK ((SELECT public.authorize('homepage.edit')));

-- Writers must see objects for existence checks and guarded compensation.
ALTER POLICY hero_images_read ON storage.objects USING (
    bucket_id = 'hero-banner-images' AND
    ((SELECT public.authorize('homepage.view')) OR
     (SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))));

ALTER POLICY hero_images_create ON storage.objects WITH CHECK (
    bucket_id = 'hero-banner-images' AND
    name ~ '^hero/[0-9a-f-]{36}/[0-9a-f]{32}[.](png|jpg|webp)$' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))));

ALTER POLICY hero_images_discard_uncommitted ON storage.objects USING (
    bucket_id = 'hero-banner-images' AND
    (storage.foldername(name))[2] = (SELECT auth.uid())::text AND
    ((SELECT public.authorize('homepage.create')) OR
     (SELECT public.authorize('homepage.edit'))) AND
    NOT EXISTS (SELECT 1 FROM public.hero_banners h
        WHERE h.desktop_image_key = storage.objects.name
           OR h.mobile_image_key = storage.objects.name));

CREATE OR REPLACE FUNCTION public.get_hero_banner_for_write(p_create BOOLEAN)
RETURNS SETOF public.hero_banners
LANGUAGE plpgsql STABLE SECURITY INVOKER
SET search_path = public AS $$
BEGIN
    IF p_create IS NULL OR
       NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
    END IF;
    RETURN QUERY SELECT h.* FROM public.hero_banners h WHERE h.id = 1;
END;
$$;

CREATE OR REPLACE FUNCTION public.save_hero_banner_admin(
    p_create BOOLEAN, p_heading TEXT, p_description TEXT,
    p_button_label TEXT, p_button_link TEXT,
    p_desktop_image_key TEXT, p_mobile_image_key TEXT,
    p_is_visible BOOLEAN)
RETURNS VOID LANGUAGE plpgsql SECURITY INVOKER
SET search_path = public AS $$
DECLARE affected INTEGER;
BEGIN
    IF p_create IS NULL OR
       NOT public.authorize(CASE WHEN p_create THEN 'homepage.create' ELSE 'homepage.edit' END) THEN
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

REVOKE ALL ON FUNCTION public.get_hero_banner_for_write(BOOLEAN) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.get_hero_banner_for_write(BOOLEAN) TO authenticated;

COMMIT;
