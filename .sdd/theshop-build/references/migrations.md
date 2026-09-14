# Supabase migrations

Load only for database changes. SQL stays in `supabase/migrations/`; feature record links files and execution evidence. One feature can need several migrations. Feature ID never determines migration version.

## 1. Inspect and prepare

Identify affected tables/functions/policies, current schema, and applied migration history. Inspect existing SQL before generating changes. Confirm local test database is disposable or preserve its data. Inspect remote history read-only when remote access is available; missing remote access does not block local work.

Preserve repository's existing migration-version convention. Allocate next unused version after checking files and known history; currently files use numeric prefixes such as `0032_...sql`. Never rename existing files to adopt timestamps or repair drift. Preserve SQL already applied to shared/remote databases; corrections use new migrations. Draft SQL may iterate in disposable local development, with fresh verification afterward.

Prefer additive compatible changes. Include required constraints, indexes, grants, RLS, and permission seeds. Plan backfills for existing rows. Avoid references to generated IDs from another environment. Record recovery approach for compatibility/data risk; dropped data is not restored merely by reversing schema SQL.

## 2. Apply and test locally

Inspect installed CLI/help and existing local setup before using commands. Start local Supabase through existing project tooling. Follow [database fixture and cleanup rules](verification.md#database-test-lifecycle). Verify actual local endpoint and pending files; never reuse remote connection arguments for local tests.

First establish preceding schema and small representative existing rows in a disposable fixture. Apply pending feature migrations with explicit local target: `supabase migration up --local`. Verify existing data, backfills, constraints, permissions, and affected application behavior. Resetting directly to latest schema before this case would erase upgrade evidence.

Then prove full migration replay into an empty disposable database, using separate fixture or restoring test setup after upgrade results are recorded. `supabase db reset --local` can supply this clean replay, subject to disposable-target checks in verification policy. Run relevant tests against resulting schema and seeds. Reset once for this replay case, not before each test. Keep upgrade and replay results distinct; never reset a remote environment as feature validation.

Run affected integration tests, constraints, permitted/denied RLS cases, and application tests. Include user-facing E2E where behavior crosses UI/database. Test compatibility and backfill results, not merely successful SQL execution. Resolve applicable independent security review before remote deployment. Record local results for exact SQL files; changed SQL invalidates those results.

Local failure or missing prerequisite blocks remote application. Do not bypass failures, apply remotely to test, or label skipped database tests as passed.

## 3. Select remote execution method

Prefer Supabase MCP for scoped schema/history inspection and advisors when connected. Check actual project identifier, permissions, tool schemas, and endpoint. Hosted MCP is not evidence that a local test database exists. MCP authentication does not automatically authenticate CLI.

Use CLI to deploy repository files with their recorded versions: `supabase db push --linked --dry-run`, then authorized `supabase db push --linked`. Both refer to verified linked project. Dry run lists pending files; it does not prove SQL succeeds. Never print connection credentials.

MCP `apply_migration` may accept only project, name, and SQL, without repository version. A matching name does not establish matching migration version. Inspect current schema before considering it. If version cannot be preserved, use CLI; unavailable CLI access blocks dependent remote work. Do not substitute raw `execute_sql` for migration application, invent a version parameter, or automatically repair history afterward.

If a future MCP capability preserves explicit versions, it may apply the same locally tested SQL with identical recorded versions after authorization. Verify resulting history. CLI and MCP are execution choices, not separate copies of migrations.

## 4. Authorize and apply remotely

After local checks and applicable review pass, prepare exact project/environment, pending migration list, affected data/permissions, test results, and recovery approach. Inspect local/remote history with `supabase migration list` or corresponding read-only MCP capability. Investigate drift before writes; never automatically run history repair, squash, `--include-all`, seed, or reset to make deployment proceed.

Remote deployment requires explicit authorization covering target and exact pending changes. Existing authorization covering those changes satisfies this requirement; do not ask again. Local success alone grants no remote permission. New target, additional pending migrations, or materially changed SQL requires resolved authorization before application. `db push` can apply unrelated pending files; include every pending file in review and authorization or stop.

Apply only tested, authorized migrations in recorded order. One deployer at a time. On error, inspect history/schema and determine what applied before any retry. Do not assume full rollback or blindly repeat non-idempotent data changes.

## 5. Verify and record

Verify remote migration history, expected schema, RLS/constraints, and relevant non-destructive smoke checks. Use synthetic/approved data; destructive tests remain local. Record advisor findings when available; permission to deploy is not permission for unrelated cleanup.

Feature record includes filenames/versions, local target and tests, remote project/environment, authorization, application results, post-apply proof, and recovery approach. Record exact CLI commands or MCP tool/arguments with secrets removed; retain actual logs. Do not rewrite previously recorded local proof as remote proof.

Remote status: `Not requested`, `Pending authorization`, `Blocked`, `Failed`, or `Applied and verified`. No success claim when post-apply checks fail; report actual partial state and recovery action. When deployment is outside agreed scope, locally verified feature may be `Done` with remote status pending/not requested. When deployment is in scope, required remote verification gates completion.

## Sources

Supabase documents file/history tracking and local testing in [Database migrations](https://supabase.com/docs/guides/deployment/database-migrations). Explicit target and dry-run flags appear in [CLI reference](https://supabase.com/docs/reference/cli/introduction). Inspect current [MCP database tool schemas](https://github.com/supabase/mcp/blob/main/packages/mcp-server-supabase/src/tools/database-operation-tools.ts) rather than assuming version support.
