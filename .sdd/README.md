# TheShop portable SDD

This directory owns the shared workflows, role instructions, templates, verification scripts, and runtime adapters. Feature records remain in `.specs/{feature}/`.

## Authoring and generation

Edit `.sdd/skills/`, `.sdd/roles/`, `.sdd/contracts/`, or adapter configuration. Native files in `.claude/`, `.agents/skills/`, and `.codex/agents/` are generated. Existing dotted Claude commands remain supported aliases.

Use PowerShell 7 from the repository root:

```powershell
pwsh -NoProfile -File .sdd/scripts/sync-adapters.ps1 -Check
pwsh -NoProfile -File .sdd/scripts/sync-adapters.ps1
pwsh -NoProfile -File .sdd/scripts/test-portability.ps1
```

The generator validates all output paths before writing. It leaves unrelated skills, agents, user settings, credentials, and MCP connections alone. It refuses to overwrite independently edited generated files. Regenerate after editing the shared sources, then run the portability suite.

## Workflow entry points

Install, upgrade, clean-checkout validation, and rollback: [release operation](release.md). Current blockers and measured proof: [release readiness](reports/release-readiness.md).

Task helpers follow [routing contract](contracts/task-routing.md). Existing upstream skills stay installed once; adapters add no duplicate work patterns. Verify local discovery with `check-skill-discovery.ps1`; native decision pilots use `test-sdd-native.ps1 -Case routing`.

Feature handoffs and continuation follow [evidence contract](contracts/evidence.md). `manage-sdd-evidence.ps1` records source hashes, gate receipts, failures, and deliberate amendments. `test-evidence.ps1` exercises stale inputs and recovery in isolation. Legacy features remain readable; establish evidence by rerunning checks before trusting old success labels.

Execution and optimization pilots follow [evaluation contract](contracts/evaluation.md). Trials retain raw evidence and do not switch production defaults. See [phases 8–12 report](reports/phases8-12.md) for implemented scope, results, and remaining acceptance work.

Third-party skills/agents use versioned packages and project wrappers. Read [extension onboarding](extensions/README.md). `manage-extensions.ps1` inspects, installs, updates, removes, and restores extensions across both clients. Core catalog identities remain stable; active extension registry extends discovery.

Claude: `/theshop-spec add-brand` (legacy `/theshop.spec add-brand` also works).
Codex: `$theshop-spec add-brand --desc Admins can maintain product brands.`

Every workflow names its canonical source and runtime adapter. Both runtimes must load the shared execution contract before working. The native adapter controls invocation, delegation, and capability resolution. It does not change feature scope, approval requirements, or gate outcomes.

## Migration scope

Phases 1–5 preserve the existing workflows and add portability. They do not introduce Caveman compression, new task routing, optimization proxies, or changes to existing feature behavior. `baseline/inventory.json` records the original revision, hashes, and observed gate results. `baseline/originals.zip` contains the original tracked definitions for controlled rollback. Existing baseline failures are reported, not silently repaired.

See `reports/compatibility.md` for measured results and any unavailable runtime checks. Passing static tests is not a claim that a live client executed a workflow.

## Caveman phase

Shared policy: `contracts/communication.md`. Default `full` for all prose, including persisted specs, plans, and instructions. Other official levels require explicit request. Keep auto-clarity for warnings, confirmations, ambiguous sequences, and clarification; resume selected mode afterward. Technical literals, artifact schemas, role scopes, and gate evidence remain exact.

Both adapters load policy directly at every workflow/role entry point and through execution contract; pass active mode/language to workers. Ordinary project chat loads policy from `AGENTS.md`. No extra Caveman invocation, installation, proxy, cloud account, or Cavekit dependency. `adapters/caveman/source.json` pins upstream snapshot by content hash and records project overrides. Snapshot is provenance; execution uses the compact shared policy. Review upstream changes deliberately before updating it.

Discovery descriptions and role introductions are concise. Graph construction and invocation examples load only when needed. Required procedures, templates, and literal API handoffs remain available through native rendered references. Existing application files and feature artifacts are not rewritten by this migration.

Run `pwsh -NoProfile -File .sdd/scripts/test-caveman.ps1 -Published`. Add `-MigrationAudit` during this rollout for historical gate-function comparisons, protected-file checks, and isolated phase 5 rollback proof. Ongoing CI checks current gate behavior through `test-portability.ps1`; historical comparisons reject later deliberate gate changes. Read `reports/caveman-adoption.md` for scope and `reports/caveman-validation.md` for measured evidence and limits. Byte counts are not token counts; native usage is recorded separately.

Native paired evaluation: `test-caveman-runtime.ps1 -Variant baseline|candidate -Runtime claude|codex -Case decisions|spec|clarify|plan`. Requires authenticated clients. Fixtures live in a dedicated temporary directory outside project instruction ancestry; logs remain under ignored `.sdd/.test-work/caveman-native/`. For clarify/plan, pass `-ContinueFixture` from the previous stage. Reports record actual process exit, artifact checks, runtime usage, and elapsed time. No evaluation touches production services.

Policy selection and prose adherence are separate checks. Native models can recognize `full` yet write verbose artifact passages. Inspect actual artifacts for retained facts, compression, and auto-clarity; do not treat mode fields or schema gates as a style guarantee. Runtime token counts include differing cache and reasoning behavior; no fixed savings promise.

**Caveman rollback:** `pwsh -NoProfile -File .sdd/scripts/caveman-checkpoint.ps1 -Action Restore` previews restoration of phase 5 shared sources and native adapters. Add `-Apply` only to execute that reviewed restore. This uses `baseline/caveman/phase5.zip` and sealed candidate hashes; independently changed files block restoration before writes. Original migration rollback below instead restores the older Claude-only state.

## Verification and rollback

Phase 7 refactors all 16 core workflows and nine roles. Edit authoritative `.sdd/skills/` and `.sdd/roles/`; generated commands, aliases, skills, and agents follow both adapters. Keep inputs, scope, ordered procedure, outputs, and completion evidence explicit. Use existing headings where they aid callers; never rename artifact schema fields for style.

Conditional reports, layer examples, and diagnostics live in linked references. Read each at its stated trigger; moving instructions never makes required behavior optional. Status ledger template now lives at `.sdd/skills/theshop-spec/references/status-tracker.md`. Literal commands, API blocks, report schemas, templates, numbered constitution rules, ownership, and approval boundaries remain preserved. Third-party originals remain immutable.

`test-caveman.ps1` follows instruction references when checking preserved literals. `test-phase7.ps1` is a one-time migration audit against `.sdd/baseline/phase7/before-valid.zip`; it also checks original application/feature bytes. Do not add that historical byte audit to ongoing CI: legitimate feature work changes those files. Read `reports/phase7-refactor.md` for measured results and verification scope.

Run `test-portability.ps1 -Published` after generation. CI runs the same check. During this migration only, add `-MigrationAudit` to compare historical gate results and prove existing feature artifacts were not edited. That historical audit is intentionally separate from ongoing checks: normal feature development changes those artifacts.

`capture-baseline.ps1` and `initialize-core.ps1` are one-time migration tools and refuse to replace their outputs. `baseline/extraction.json` records the initial extraction, not hashes of subsequently maintained shared sources. `baseline/preservation.md` explains the deliberate portability changes.

To inspect rollback, run `pwsh -NoProfile -File .sdd/scripts/rollback-adapters.ps1`. After reviewing its restore/remove list, add `-Apply`. It restores the 57 original files byte-for-byte and removes only generated files whose ownership hashes still match. It refuses independent edits, leaves unrelated content and the shared core in place, and never resets Git. Re-run the generator to enable the adapters again.

Native checks require authenticated clients and available quota. `prepare-runtime-fixture.ps1` returns a disposable workspace path. Run `test-runtime.ps1 -Runtime codex -FixtureRoot <path>` first, then `-Runtime claude` against that same path. Use `-Executable <installed-launcher>` if discovery cannot find a client. These checks create a disposable spec, exercise the legacy Claude clarify alias, and verify the shared artifacts mechanically. Native logs remain in the ignored fixture. A zero process exit alone is not success.
