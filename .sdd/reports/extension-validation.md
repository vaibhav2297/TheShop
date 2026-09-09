# Extension support validation

Implementation delivered. Both Claude Code and Codex passed native skill and completed-delegation checks. Earlier Claude quota failure remains recorded separately.

## Delivered

- Shared extension registry merges with unchanged core catalog: 16 workflows and 9 roles remain; one skill and one agent added.
- Prepared local package import, immutable revisions, original upstream bytes/license, project wrappers, explicit capability bindings, invocation policy, and standalone/pipeline placement.
- Both client adapters generate from shared sources. Native output count: 198. No new legacy aliases required.
- Inspect/install/update/remove/version rollback, complete write-set preflight, transaction recovery, and ownership checks.
- Binary assets and multiline upstream YAML preserved. Manifest version 2 distinguishes normalized text from exact-byte vendor files. Git attributes preserve package/vendor bytes across checkouts.
- [Onboarding guide and template](../extensions/README.md), lifecycle CI check, native smoke harness, and separate phase 6 checkpoint.

## Installed examples

Both use [Caveman commit 5184b3d](https://github.com/JuliusBrussee/caveman/tree/5184b3d11ac6a1acb7d44b9bfaa31698157cff97), including original MIT license. No installers, proxy, cloud service, or account configuration added.

| Entry | Type | Package revision | Adaptation |
|---|---|---|---|
| `sdd-caveman-help` | Skill | `02de72366823a4939ea8fa823b161ac2b7e41f4509b6a255ad8b607cd2b794f1` | Explains project policy and plain-language mode requests. Upstream config/command availability is not assumed. |
| `sdd-cavecrew-reviewer` | Agent | `4f47c7db84a49ddb5d65d2a0cff7b7c3c8e6b2b320f1e899a80fd38481fbb695` | Shared mode overrides upstream ultra. Inherits authorized model instead of upstream Haiku. Read-only, standalone review. |

No pipeline stage replaced. Full remains default; explicit levels/off and auto-clarity remain authoritative. Parent saves reviewer output.

## Mechanical evidence

| Check | Result | Evidence |
|---|---|---|
| Extension lifecycle, provenance, ownership, Git checkout, trace parser, strict IDs | 34/34 passed | [extension-static.json](extension-static.json) |
| Existing portability and migration audit | 32/32 passed | [static-verification.json](static-verification.json) |
| Caveman contracts and original migration preservation | 14/14 passed | [caveman-static.json](caveman-static.json) |
| New phase rollback, preview, independent-edit rejection | 3/3 passed; 276 files restored in isolation | [extension-checkpoint.json](extension-checkpoint.json) |
| New generated skill frontmatter | Both definitions passed bundled skill-creator `quick_validate.py` | `.agents/skills/sdd-caveman-help` and `.claude/skills/sdd-caveman-help` |

Lifecycle tests exercise actual scripts and files. They include install preview, idempotence, vendor/binary hashes, agent read-only metadata, update, rollback, removal, retained revisions, caller references, unsupported runtimes, missing bindings, unadapted hooks, collisions, interrupted writes, and recovery. Windows Git checkout test uses an isolated repository with `core.autocrlf=true`; production Git index remains untouched.

Final test review corrected fixture snapshot filtering: exclusions now use paths relative to fixture, and empty snapshots fail. Preservation assertions compare real files rather than accidentally excluding temporary workspace ancestry.

Historical failing feature gates retain original outcomes. Passing parity does not repair those pre-existing failures.

## Native evidence

**Codex: passed.** [Native report](extension-native-codex.json). Final evaluated run took 172.48 seconds. Generated help produced `full.md`, `lite.md`, and `off.md`; actual child reviewer returned expiry-boundary finding. Protected fixture inputs remained unchanged.

CLI JSON omitted spawn events. First ephemeral run therefore could not prove delegation and was not accepted. Second run retained its own session. Corrected parser matched actual `spawn_agent` request selecting `sdd-cavecrew-reviewer` with `/root/extension_review` final return. Rechecking that same trace passed; no artifact edits or additional model run were used to manufacture evidence. Pre-recheck report remains in ignored log directory.

Evidence directory: `.sdd/.test-work/extension-native/codex-e129d59a5f34446d86ed6c423dbb21e8/`. `delegation-evidence.json` contains matched role, call ID, child author, and returned text. Native return identified `token.js:2`: equality must be inactive; change `now <= expiresAt` to `now < expiresAt`.

Codex custom-agent format and inheritance checked against [official OpenAI documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents). Live host permissions remain authoritative; read-only role metadata is not a guarantee against a broader host override.

**Claude: passed after quota reset.** [Native report](extension-native-claude.json). Successful run took 141.04 seconds and saved all five artifacts. Native statistics prove one `sdd-cavecrew-reviewer` spawned and completed; zero failed or refused. Read-only fixture inputs remained unchanged.

Successful evidence directory: `.sdd/.test-work/extension-native/claude-f092233aad8b4f8ab88ff39c71d2e664/`. Reviewer identified `token.js:2` and correct strict comparison. Parent saved report and recorded lite inheritance with normal-English security auto-clarity.

Earlier run took 84.40 seconds and saved three help outputs, then returned HTTP 429 and `rate_limit` before delegation. It remains a failed attempt in [quota report](extension-native-claude-quota.json), with logs under `.sdd/.test-work/extension-native/claude-2239e2403ef34f4ab5eede07584f5701/`. Retry occurred after reported September 6, 2026, 16:00 IST reset. Global trust/settings were not changed.

Repeat either native check when extension behavior changes:

```powershell
pwsh -NoProfile -File .sdd/scripts/test-extension-runtime.ps1 -Runtime claude
pwsh -NoProfile -File .sdd/scripts/test-extension-runtime.ps1 -Runtime codex
```

## Prose inspection

Codex full answer uses short fragments; lite/off use complete sentences. Mode scope, saved-artifact coverage, no invented `--caveman` flag, and auto-clarity survive. Delegated security finding uses clear normal English and exact comparison operators.

Claude's successful full answer uses fragments; lite/off use complete prose. Earlier quota-limited full answer was verbose and included an arrow despite style guidance. The successful reviewer correctly identifies equality at expiry, but its claim of a full time unit of exposure assumes clock behavior beyond the supplied function contract. Actual prose still needs review; no uniform compression or error-free reviewer guarantee. No fixed token-saving claim.

## Preservation and boundaries

New checkpoint captures 276 phase 6 files and tracks 535 application/test/spec files. Older Claude-only and Caveman checkpoint archives/seals remain unchanged. Full phase rollback was executed only inside disposable fixture; root retains extension support.

Finalization corrected generated maintenance hints to use package updates instead of editing immutable snapshots. `candidate-release.json` records final owned files; earlier seals remain unchanged. Select release seal for phase rollback, as onboarding guide specifies.

Importer accepts reviewed local packages. It does not translate arbitrary hooks, install tools, download updates, or wire pipeline callers automatically. Pipeline validation checks caller reference; native task verification proves behavior. Use one publisher at a time. Journal recovery handles interrupted writes; external concurrent edits can block recovery.

Code graph refreshed with AST extraction; no temporary fixture sources indexed. Optional SQL parser remains absent, so SQL coverage and semantic documentation refresh are not claimed.

No application feature implementation, production E2E, commit, push, or remote mutation performed. Full real-feature SDD trial remains later work.
