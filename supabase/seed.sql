-- ============================================================================
-- seed.sql
--
-- Applied automatically by `supabase db reset` after supabase/migrations/*,
-- against the local stack only. Seeds the three E2E test personas
-- (.specs/e2e-test/e2e-automation-plan.md Phase 2.3). Every statement is
-- ON CONFLICT DO NOTHING so re-running this file (via repeated db reset) is
-- always safe.
--
-- Fixed UUIDs keep the seed idempotent and keep role grants stable across
-- resets. auth.users/auth.identities column shapes were confirmed against
-- the local Supabase Postgres instance rather than guessed. Runs as a
-- privileged role: auth.uid() is NULL in this context, which is what
-- exempts these user_roles inserts from the self-change guard
-- (0007_create_rbac.sql, guard_user_roles_self_change).
--
-- auth.identities.email is a GENERATED ALWAYS column derived from
-- identity_data->>'email' — it must never be set explicitly.
--
-- No product/brand seeding is needed here — migration 0002 already seeds
-- the catalogue (18 products, 6 brands, 5 categories) and db reset replays
-- it every session.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Persona: Admin — e2e-admin@theshop.test
-- ----------------------------------------------------------------------------
INSERT INTO auth.users (
    instance_id, id, aud, role, email, encrypted_password,
    email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
    created_at, updated_at
) VALUES (
    '00000000-0000-0000-0000-000000000000',
    'a1e2e000-0000-4000-8000-000000000001',
    'authenticated', 'authenticated', 'e2e-admin@theshop.test', '',
    now(), '{"provider":"email","providers":["email"]}'::jsonb, '{}'::jsonb,
    now(), now()
) ON CONFLICT (id) DO NOTHING;

INSERT INTO auth.identities (
    provider_id, user_id, identity_data, provider, created_at, updated_at
) VALUES (
    'a1e2e000-0000-4000-8000-000000000001',
    'a1e2e000-0000-4000-8000-000000000001',
    '{"sub":"a1e2e000-0000-4000-8000-000000000001","email":"e2e-admin@theshop.test","email_verified":true}'::jsonb,
    'email', now(), now()
) ON CONFLICT (provider_id, provider) DO NOTHING;

INSERT INTO public.customers (id, first_name, last_name, date_of_birth, email)
VALUES ('a1e2e000-0000-4000-8000-000000000001', 'E2E', 'Admin', '1990-01-01', 'e2e-admin@theshop.test')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.user_roles (user_id, role_id)
SELECT 'a1e2e000-0000-4000-8000-000000000001', r.id FROM public.roles r WHERE r.name_key = 'Admin'
ON CONFLICT (user_id, role_id) DO NOTHING;

-- ----------------------------------------------------------------------------
-- Persona: Support — e2e-support@theshop.test
-- ----------------------------------------------------------------------------
INSERT INTO auth.users (
    instance_id, id, aud, role, email, encrypted_password,
    email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
    created_at, updated_at
) VALUES (
    '00000000-0000-0000-0000-000000000000',
    'a1e2e000-0000-4000-8000-000000000002',
    'authenticated', 'authenticated', 'e2e-support@theshop.test', '',
    now(), '{"provider":"email","providers":["email"]}'::jsonb, '{}'::jsonb,
    now(), now()
) ON CONFLICT (id) DO NOTHING;

INSERT INTO auth.identities (
    provider_id, user_id, identity_data, provider, created_at, updated_at
) VALUES (
    'a1e2e000-0000-4000-8000-000000000002',
    'a1e2e000-0000-4000-8000-000000000002',
    '{"sub":"a1e2e000-0000-4000-8000-000000000002","email":"e2e-support@theshop.test","email_verified":true}'::jsonb,
    'email', now(), now()
) ON CONFLICT (provider_id, provider) DO NOTHING;

INSERT INTO public.customers (id, first_name, last_name, date_of_birth, email)
VALUES ('a1e2e000-0000-4000-8000-000000000002', 'E2E', 'Support', '1990-01-01', 'e2e-support@theshop.test')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.user_roles (user_id, role_id)
SELECT 'a1e2e000-0000-4000-8000-000000000002', r.id FROM public.roles r WHERE r.name_key = 'Support'
ON CONFLICT (user_id, role_id) DO NOTHING;

-- ----------------------------------------------------------------------------
-- Persona: Customer — e2e-customer@theshop.test (Customer role auto-assigned
-- by the on_auth_user_created trigger; no manual user_roles insert needed)
-- ----------------------------------------------------------------------------
INSERT INTO auth.users (
    instance_id, id, aud, role, email, encrypted_password,
    email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
    created_at, updated_at
) VALUES (
    '00000000-0000-0000-0000-000000000000',
    'a1e2e000-0000-4000-8000-000000000003',
    'authenticated', 'authenticated', 'e2e-customer@theshop.test', '',
    now(), '{"provider":"email","providers":["email"]}'::jsonb, '{}'::jsonb,
    now(), now()
) ON CONFLICT (id) DO NOTHING;

INSERT INTO auth.identities (
    provider_id, user_id, identity_data, provider, created_at, updated_at
) VALUES (
    'a1e2e000-0000-4000-8000-000000000003',
    'a1e2e000-0000-4000-8000-000000000003',
    '{"sub":"a1e2e000-0000-4000-8000-000000000003","email":"e2e-customer@theshop.test","email_verified":true}'::jsonb,
    'email', now(), now()
) ON CONFLICT (provider_id, provider) DO NOTHING;

INSERT INTO public.customers (id, first_name, last_name, date_of_birth, email)
VALUES ('a1e2e000-0000-4000-8000-000000000003', 'E2E', 'Customer', '1990-01-01', 'e2e-customer@theshop.test')
ON CONFLICT (id) DO NOTHING;
