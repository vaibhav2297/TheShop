-- ============================================================================
-- 0016_grant_support_all_view_permissions
--
-- Widen the Support role to read-only visibility across every module: grant
-- it the *.view permission of all eleven catalogue modules. Support keeps its
-- existing orders.edit grant; no other write/sensitive permission is added.
--
-- Idempotent (ON CONFLICT DO NOTHING) — the five codes Support already holds
-- (orders.view, customers.view, products.view, brands.view via 0007/0015, plus
-- orders.edit untouched) are skipped. The 0009 perm_version triggers bump
-- affected users automatically, so Support staff pick the new claims up at
-- their next token mint.
-- ============================================================================

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code LIKE '%.view'
WHERE r.name_key = 'Support'
ON CONFLICT (role_id, permission_id) DO NOTHING;
