# Third-party extensions

Add once; generate Claude Code and Codex. Core catalog stays in `.sdd/catalog.json`. Active extensions live in `registry.json`; generator merges both catalogs. Feature records stay in `.specs/`.

## Package layout

```text
my-extension/
  extension.json
  SKILL.md                  # skill wrapper, or ROLE.md for agent
  upstream/
    LICENSE
    SKILL.md                # upstream entry, original bytes
    references/...          # dependencies needed by upstream procedure
    scripts/...
    assets/...
```

Use [template.json](template.json) for manifest fields. Replace every placeholder. Keep only matching wrapper type. Skill wrapper needs ordinary single-line `name`/`description` frontmatter; upstream YAML stays opaque and may use multiline fields. Agent wrapper has Markdown body only.

Wrapper must reference `{{extension:upstream}}`. Generator resolves that token to pinned entry. Explain project adaptations in wrapper and manifest. Preserve upstream code, scripts, assets, license, and instructions byte-for-byte. Do not copy vendor instructions into global project policy.

Installed examples are complete templates: find their revisions with `List`, then copy corresponding package outside `packages/`. Remove copied `.package-lock.json` before editing. Never edit retained package revisions in place.

## Onboard

1. Inspect upstream repository and immutable commit. Download required files into `upstream/`, including license. Review referenced dependencies, hooks, model assumptions, permissions, commands, and external effects. Importer accepts prepared local packages; it does not download or execute installers.
2. Choose unique ID, `skill` or `agent`, invocation policy, and placement. Automatic discovery is default for examples. `explicit` skills get native invocation metadata; explicit agents get an instruction boundary because agent discovery lacks matching skill metadata.
3. Write wrapper and capability bindings for both clients. `supported: false` or missing bindings block activation. Bindings describe tools, not permission grants or automatic installations. List every required script interpreter/connector as capability; verify availability in native test.
4. Record upstream file SHA-256 values. `upstream.files` covers every file beneath `upstream/`, including binary assets and license. `upstream.revision` is immutable 40–64 character hexadecimal revision hash. Package revision additionally covers manifest, wrapper, and all upstream bytes.
5. Inspect, preview, and publish within authorized scope. Commands run from repository root with PowerShell 7:

```powershell
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Inspect -PackagePath ./my-extension
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Install -PackagePath ./my-extension
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Install -PackagePath ./my-extension -Apply
pwsh -NoProfile -File .sdd/scripts/test-extensions.ps1 -Published
pwsh -NoProfile -File .sdd/scripts/test-portability.ps1 -Published
pwsh -NoProfile -File .sdd/scripts/test-caveman.ps1 -Published
```

Install publishes package, registry, both native definitions, and ownership manifest. Preview prints staged workspace for content review. Complete write set is checked before publication. Independent generated edits, unowned path collisions, and duplicate core IDs block writes. No mandatory extra approval when existing task already authorizes publication; `-Apply` distinguishes execution from preview.

Manifest version 2 distinguishes normalized text from exact-byte vendor files retained by older generation. Vendor trees now stay in `.sdd/extensions/packages/{id}/{revision}/upstream/`; skill and agent wrappers reference that same pinned tree. Relative vendor references, scripts, assets, and license remain intact. Do not copy vendor `SKILL.md` files under native skill roots: recursive discovery can expose them independently of project wrappers. Package Git attributes disable line-ending conversion; keep them with retained packages.

Existing installations with generated vendor copies require deliberate retirement through the extension manager. Preview `Update` against the already active retained package, then publish with `-Apply`. Same package revision is valid: manager regenerates adapters, checks ownership, journals writes, and retires obsolete generated copies. Independently changed files block publication. Ordinary `sync-adapters.ps1` continues to reject stale paths; never delete them manually or weaken ownership checks.

## Placement and communication

`standalone` adds capability without changing SDD stages. For `pipeline`, set `caller` to existing canonical workflow/role file. Edit that caller to invoke extension, assign ownership, pass inputs/mode, await results, and preserve existing gates. Validation checks caller reference exists; behavioral tests must prove actual invocation. Registration alone never creates a pipeline stage. Update/remove a pipeline extension only after reconciling callers.

Generated entrypoints load shared execution and Caveman policy. Full remains default, including saved prose. Explicit levels/off and auto-clarity carry into delegated work. Wrappers override conflicting upstream style defaults; technical requirements remain intact. Re-test actual prose after upstream changes. Mode selection alone does not prove compression.

## Update, restore, remove

Copy retained package into working directory. Apply new upstream snapshot and hash inventory; adapt wrapper after reviewing diff. Use `Inspect` and `Update` preview to compare old/new manifests, scope, bindings, wrapper, and staged native output. No automatic upstream polling or promotion.

```powershell
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action List
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Update -PackagePath ./my-extension
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Update -PackagePath ./my-extension -Apply
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Rollback -Id my-extension -Revision <retained-package-hash> -Apply
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Remove -Id my-extension -Apply
```

Rollback selects retained package revision, including after removal. Removal deletes only owned generated files. Retained packages and unrelated files remain. Direct registry edits are unsupported for retirement; manager coordinates registry and native files.

Transactions record before/after bytes in `history/`. Failed writes trigger recovery; interrupted transactions block further changes. Recovery refuses intervening independent edits:

```powershell
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Recover -Transaction <transaction-id>
pwsh -NoProfile -File .sdd/scripts/manage-extensions.ps1 -Action Recover -Transaction <transaction-id> -Apply
```

Use one SDD publisher at a time. Mutation lock serializes manager publication; it cannot coordinate arbitrary external edits or another process writing files directly. Recovery is journaled, not a filesystem-wide atomic transaction.

## Installed pair

- Claude `/sdd-caveman-help`; Codex `$sdd-caveman-help`. Explains project modes without activating a mode or changing settings.
- Ask to delegate supplied diff/file review to `sdd-cavecrew-reviewer`. Read-only helper; parent saves report. It does not replace `theshop-review` or its independent reviewers.

Both derive from pinned [Caveman repository](https://github.com/JuliusBrussee/caveman/tree/5184b3d11ac6a1acb7d44b9bfaa31698157cff97). Project reviewer inherits authorized model and communication mode instead of upstream Haiku/ultra choices. Restart a client session if newly generated definitions are not discovered.

Native pair smoke test: `test-extension-runtime.ps1 -Runtime claude` and `-Runtime codex`. Requires authenticated clients; creates isolated temporary fixtures, delegates reviewer, saves prose and traces. Codex test retains its own session to inspect spawn events omitted by CLI JSON. No production feature execution. See [validation report](../reports/extension-validation.md).

## Migration history

Existing Claude-only and Caveman snapshots remain intact. New `baseline/extensions/phase6.zip` captures state before extension support. Current seal is `candidate-release.json`; earlier candidate seals remain historical. Preview full phase rollback with `extension-checkpoint.ps1 -Action Restore -CandidateFile candidate-release.json`; add `-Apply` only to execute that restore. Restore extension phase before attempting older Caveman rollback. Normal extension version rollback uses manager above.
