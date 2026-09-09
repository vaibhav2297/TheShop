# Phases 1–5 compatibility report

The shared core and both native adapters are implemented. Phase 5 includes deterministic generation checks, migration preservation checks, rollback tests, CI, and an isolated native-client test harness. Native execution evidence is recorded separately from static validation.

| Phase | Implemented result |
|---|---|
| 1. Baseline | Immutable archive of 57 original files; source mapping; feature hashes; 12 baseline gate observations |
| 2. Shared core | 16 workflows, nine specialist role contracts, references, templates, project rules, and shared scripts under `.sdd/` |
| 3. Claude adapter | Generated skills and roles, all original entry-point aliases, native argument handling, project import, design hook bridge |
| 4. Codex adapter | Generated `.agents/skills/`, explicit invocation policies, nine `.codex/agents/*.toml` roles, and `AGENTS.md` |
| 5. Compatibility | Generation/drift checks, gate parity, rejection cases, hook payload parity, safe rollback, CI, and native fixture runner |

There are 169 generated files. Edit their shared sources and regenerate; independent edits block publication. The generator leaves unrelated skills and user configuration untouched. See `../baseline/preservation.md` for the source mapping and deliberate changes.

## Verification evidence

`static-verification.json` records **32/32 passing checks** against the published adapters. The migration audit compares stdout, stderr, and exit codes of all 12 captured gate cases, including the two existing failures in `breadcrumbs`. It also checks existing feature artifacts byte-for-byte. Ongoing CI excludes the historical feature snapshot audit so normal feature development can continue.

Rollback is exercised in an isolated workspace: preview is non-mutating, independent edits block restoration, all 57 originals return with their exact hashes, and unrelated files remain.

The forward check found and corrected three portability regressions: a missing explicit design gate in Codex, whole-diff formatting by parallel workers, and templates that bypassed native token rendering. Known pre-existing Domain/test and Web/resource ownership conflicts remain documented in the preservation record.

## Native-client scope

Installed versions: Codex CLI `0.153.4`; Claude Code `2.1.220`. Discovery uses the existing npm launchers; no client, model, connector, or credential is installed or changed.

The disposable native scenario invokes Codex's `theshop-spec`, then Claude's legacy `theshop.clarify` alias against the same spec and ledger. The harness verifies actual spec/status gates and the expected artifact state. A zero client process exit alone does not pass verification. Detailed process logs remain in the ignored fixture; `codex-live.json` and `claude-live.json` record the available evidence.

Early native attempts encountered environment limits: the sandbox Codex copy lacked its code-mode host, the complete npm Codex runtime encountered DNS failure, and Claude returned a session-quota HTTP 429. Retries used the complete existing clients after connectivity and quota became available.

Observed continuation sequence:

1. Codex loaded its generated spec skill, native template, shared contract, and adapter. It created a spec with 12 FRs, 16 ACs, and zero assumptions, plus the shared ledger. Its actual spec gate and independent spec/status gates passed. The process timed out after its final response; `codex-create.json` deliberately records that attempt as failed despite valid artifacts.
2. Claude loaded the legacy `.claude/skills/theshop.clarify/SKILL.md` alias and canonical `theshop-clarify` skill. It confirmed the existing spec and ledger. Process exit: **0**. Independent spec/status gates: **0/0**. `claude-confirm.json` records the result.
3. Codex loaded its generated clarify skill and resumed the same confirmed artifacts without rewriting them. Process exit: **0**. Independent spec/status gates: **0/0**. `codex-live.json` records the successful return handoff.

This proves the selected artifact handoff in both directions. Codex used the existing Windows PowerShell 5.1 host for the unchanged spec/status gates when its sandbox blocked nested PowerShell 7. The external harness independently checked both artifacts with PowerShell 7. That fallback is limited to those verified modes and is documented in the Codex adapter.

A final Claude trace also confirms native discovery of all **16 canonical skills, 16 legacy aliases, and nine specialist roles**. It records the actual alias/canonical file reads and a successful spec-gate tool result. `native-discovery.json` contains the filtered evidence; the full trace remains in the ignored fixture. This last run also exited 0 and passed both independent artifact gates. The published generator drift check remained clean for all 169 files.

These checks do not establish full application implementation, live Figma/Supabase operations, browser E2E behavior, or exhaustive specialist-role behavior. Those depend on the host's available capabilities and the feature being executed. Read-only Codex roles request a native read-only sandbox; other role scope restrictions remain instructions enforced within the host's actual permissions.

## Maintainer commands

```powershell
pwsh -NoProfile -File .sdd/scripts/sync-adapters.ps1
pwsh -NoProfile -File .sdd/scripts/test-portability.ps1 -Published
# Only for the original migration preservation audit:
pwsh -NoProfile -File .sdd/scripts/test-portability.ps1 -Published -MigrationAudit
```

For a controlled rollback, preview `rollback-adapters.ps1` and then use its `-Apply` option. For a new agent platform, add an adapter and generation mapping around the existing shared contracts; feature schemas and workflow logic stay in the core.

## Format references

Native formats were checked against official [Codex skills](https://learn.chatgpt.com/docs/build-skills), [Codex custom agents](https://learn.chatgpt.com/docs/agent-configuration/subagents), [Codex project instructions](https://learn.chatgpt.com/docs/agent-configuration/agents-md), [Claude skills](https://code.claude.com/docs/en/skills), and [Claude subagents](https://code.claude.com/docs/en/sub-agents) documentation. Runtime adapters resolve the tools exposed by the active host rather than assuming identical APIs across clients.

Phases 6 onward, including Caveman compression and optimization, are not part of this change.
