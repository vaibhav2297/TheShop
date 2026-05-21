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

-- A signed-in customer can read only their own row.
CREATE POLICY "customers_self_select" ON customers
    FOR SELECT USING (id = auth.uid());

-- A signed-in customer can insert their own row (used immediately after
-- VerifyOTP succeeds, when auth.uid() has just been minted).
CREATE POLICY "customers_self_insert" ON customers
    FOR INSERT WITH CHECK (id = auth.uid());

-- A signed-in customer can update only their own row.
CREATE POLICY "customers_self_update" ON customers
    FOR UPDATE USING (id = auth.uid()) WITH CHECK (id = auth.uid());

-- Admins (role claim on JWT) can read all rows for support.
CREATE POLICY "customers_admin_select" ON customers
    FOR SELECT USING ((auth.jwt() ->> 'role') = 'admin');

-- ============================================================================
-- customer_exists(p_email) — SECURITY DEFINER
--
-- Sign-in must check "does an account exist for this email?" BEFORE the user
-- has authenticated, but RLS blocks anonymous SELECT on `customers`. This
-- SECURITY DEFINER function performs the existence check inside a privileged
-- context and returns only a boolean — no PII is leaked.
-- ============================================================================
CREATE OR REPLACE FUNCTION public.customer_exists(p_email TEXT)
RETURNS BOOLEAN
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT EXISTS (
        SELECT 1 FROM customers WHERE lower(email) = lower(p_email)
    )
$$;

GRANT EXECUTE ON FUNCTION public.customer_exists(TEXT) TO anon, authenticated;
