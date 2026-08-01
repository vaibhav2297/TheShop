-- ============================================================================
-- 0017_add_dashboard_view_permission
--
-- Introduce dashboard.view — the admin console screen's own permission. The
-- former client-side "AdminArea" policy (derived from the screen registry) is
-- retired: the /admin dashboard is now gated on this permission exactly like
-- every other admin screen is gated on its own view permission, and the admin
-- nav link shows whenever the user holds it.
--
-- Granted to every staff role (Support, Admin, SuperAdmin) so all of them keep
-- their entry point into the admin console. Customer receives nothing.
--
-- Idempotent (ON CONFLICT DO NOTHING). The 0009 perm_version triggers bump
-- affected users automatically, so staff pick the new claim up at their next
-- token mint.
-- ============================================================================

INSERT INTO permissions (code, module) VALUES
    ('dashboard.view', 'dashboard')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code = 'dashboard.view'
WHERE r.name_key IN ('Support', 'Admin', 'SuperAdmin')
ON CONFLICT (role_id, permission_id) DO NOTHING;
