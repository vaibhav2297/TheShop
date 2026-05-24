-- ============================================================================
-- 0001_create_customers
--
-- Creates the `customers` profile table that mirrors auth.users and stores the
-- first name / last name / DOB / email captured at sign-up. RLS is the only
-- real security boundary, per ARCHITECTURE.md §Security.
--
-- Companion plan: .claude/plans/authentication.md §10
-- ============================================================================

CREATE TABLE IF NOT EXISTS customers (
    id              UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    first_name      TEXT NOT NULL CHECK (length(trim(first_name)) > 0),
    last_name       TEXT NOT NULL CHECK (length(trim(last_name))  > 0),
    date_of_birth   DATE NOT NULL CHECK (date_of_birth < CURRENT_DATE),
    email           TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Enforces single-account-per-email at the storage layer (FR-6).
CREATE UNIQUE INDEX IF NOT EXISTS idx_customers_email ON customers (lower(email));

-- ============================================================================
-- Row-Level Security
-- ============================================================================
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;

-- A signed-in customer or admin can read rows:
--   • self  — own row only        (id = caller's auth.uid())
--   • admin — all rows            (role claim on JWT = 'admin')
--
-- Both auth calls are wrapped in (select ...) so Postgres evaluates them
-- once per query rather than once per row (fixes auth_rls_initplan advisory).
-- Merged into one policy to avoid the multiple_permissive_policies advisory.
CREATE POLICY "customers_select" ON customers
    FOR SELECT USING (
        (SELECT auth.uid()) = id
        OR (SELECT auth.jwt() ->> 'role') = 'admin'
    );

-- A signed-in customer can insert their own row (used immediately after
-- VerifyOTP succeeds, when auth.uid() has just been minted).
CREATE POLICY "customers_self_insert" ON customers
    FOR INSERT WITH CHECK ((SELECT auth.uid()) = id);

-- A signed-in customer can update only their own row.
CREATE POLICY "customers_self_update" ON customers
    FOR UPDATE
    USING     ((SELECT auth.uid()) = id)
    WITH CHECK ((SELECT auth.uid()) = id);

-- ============================================================================
-- customer_exists(p_email) — SECURITY DEFINER
--
-- Sign-in must check "does an account exist for this email?" BEFORE the user
-- has authenticated, but RLS blocks anonymous SELECT on `customers`. This
-- SECURITY DEFINER function performs the existence check inside a privileged
-- context and returns only a boolean — no PII is leaked.
--
-- search_path is locked to '' (empty) to prevent search_path injection.
-- The customers table is therefore referenced with its full schema qualifier.
-- ============================================================================
CREATE OR REPLACE FUNCTION public.customer_exists(p_email TEXT)
RETURNS BOOLEAN
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
    SELECT EXISTS (
        SELECT 1 FROM public.customers WHERE lower(email) = lower(p_email)
    )
$$;

GRANT EXECUTE ON FUNCTION public.customer_exists(TEXT) TO anon, authenticated;
