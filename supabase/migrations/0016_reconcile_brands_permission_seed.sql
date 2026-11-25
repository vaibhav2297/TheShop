-- ============================================================================
-- 0016_reconcile_brands_permission_seed
--
-- Reconciliation: the brands.* permission rows and their role grants were
-- applied to the hosted database by an ad-hoc migration (add_brand_management)
-- that was never checked in — 0012 depends on it but only documents the gap.
-- A clean replay of this folder therefore produced a permissions table with
-- 42 rows (no brands.*) and zero brands grants, silently locking every role
-- out of the brand admin screens.
--
-- This migration checks in that missing seed, matching the hosted database
-- exactly (verified against TheShop-Dev on 2026-07-31):
--
--   * permissions: brands.view / brands.create / brands.edit / brands.delete
--   * grants: Admin + SuperAdmin -> all four brands.* codes
--             Support            -> brands.view
--             Support            -> products.view (second undocumented delta
--                                   from the same ad-hoc migration)
--
-- Every statement is idempotent (ON CONFLICT DO NOTHING), so applying this on
-- the hosted database — where the rows already exist — is a no-op. INSERTs on
-- role_permissions are intentionally not gated by the 0007 guard triggers
-- (they fire on UPDATE/DELETE only), so this replays cleanly.
-- ============================================================================

INSERT INTO permissions (code, module) VALUES
    ('brands.view', 'brands'),
    ('brands.create', 'brands'),
    ('brands.edit', 'brands'),
    ('brands.delete', 'brands')
ON CONFLICT (code) DO NOTHING;

-- Admin + SuperAdmin: the full brands module, mirroring the 0007 grant shape
-- (Admin holds every merchandising/operations module; SuperAdmin everything).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.module = 'brands'
WHERE r.name_key IN ('Admin', 'SuperAdmin')
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Support: read-only visibility of brands and products.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('brands.view', 'products.view')
WHERE r.name_key = 'Support'
ON CONFLICT (role_id, permission_id) DO NOTHING;
