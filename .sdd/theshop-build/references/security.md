# Sensitive changes

Read for identity, permissions, data access, payments, or migrations. Inspect current implementation and affected migrations before changes; this file does not authorize production operations.

## Authorization

Gate capabilities through `PermissionCatalogue` codes, not role names. Admin routes use `Pages/Admin/`, `MainLayout`, `Routes.Admin`, and inherited `PolicyNames.AdminArea`. Per-capability UI uses `PolicyNames.Permission(...)`; hide unauthorized actions. Application requests declare existing `[RequiresPermission]` mechanism.

Client policies and Application code execute in browser; neither protects direct database access. Supabase RLS enforces permissions. User-scoped data filters by `auth.uid()`; admin operations use `(SELECT public.authorize('module.action'))`. Sensitive role/refund/export policies use existing `authorize_fresh()` convention. New tables need enabled RLS and explicit policies for intended access.

Match catalogue and database seed changes. No privileged checks using editable `user_metadata`. Client permission claims mirror access; backend checks remain authoritative. Preserve system-role immutability, self-assignment restrictions, and last-Super-Admin protection when touching those operations.

## Payments, secrets, and data

Never put service-role keys, Stripe secrets, or Resend secrets in WASM, resources, logs, or client configuration. Use established trusted backend for privileged operations. Public client identifiers are not secret keys.

Payment work verifies backend-calculated amounts, ownership, provider signature validation, and duplicate-event/idempotency behavior when relevant. Do not create charges or send transactional messages merely to test. Use existing sandbox/local fixtures and scoped authorization.

Minimize personal data in evidence. Use synthetic test identities. Never print credentials while diagnosing connectivity.

## Migrations

Follow [migration lifecycle](migrations.md) for schema, function, trigger, policy, or versioned data changes. It owns local validation, migration history, remote authorization, recovery, and deployment evidence. Security requirements above remain applicable.

## Independent review

Changes to enforcement, identity flows, payment execution, sensitive-data access, or destructive migration require independent review of affected diff and acceptance before `Done`. Read-only inspection by a reviewer agent is authorized by this workflow when available; delegate only this bounded review, with relevant rules and paths. Do not send external messages.

Reviewer reports severity, path, problem, consequence, and correction. Implementer fixes in-scope findings; rerun affected checks. If independent agent unavailable, record review pending for user or another reviewer. Self-review is not independent proof. Cosmetic changes on a sensitive page do not automatically trigger this gate.
