# Evidence and continuation

Feature sidecars live under `.specs/{feature}/evidence/`. Existing spec, plan, report formats, and ledger columns remain unchanged. One orchestrator writes evidence; workers report within existing ownership. JSON records preserve exact APIs and command evidence; selected Caveman mode applies only to prose fields.

## Stage evidence

After workflow checks pass and output files stabilize, create request JSON with `outcome`, `checks`, and `sourcePaths`. `checks` must contain a nonempty array of objects: unique string `id`, exact string `command`, integer `exitCode: 0`, and workspace-relative string `logPath`. Each log must exist and contain actual captured output. Recorder automatically hashes explicitly named logs, including files under `bin`; never overwrite retained logs after recording. Include every required workflow check. Failed, missing, or unexecuted checks block success recording; use failure feedback instead.

`sourcePaths` lists extra dependencies absent from default snapshot, including shared configuration, migrations outside `supabase`, and external evidence files. Preserve uncertainty and permitted skipped-check reasons in `outcome`; never summarize them as success. Example request shape:

```json
{
  "outcome": "Plan structural check passed; unresolved decisions remain listed in plan.",
  "checks": [
    {
      "id": "plan-structure",
      "command": "pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 plan -Feature wishlist",
      "exitCode": 0,
      "logPath": ".specs/wishlist/evidence/logs/plan-structure-001.log"
    }
  ],
  "sourcePaths": []
}
```

Example is schema only; run checks and retain their actual output before using a request. Structured caller receipts are assertions backed by retained logs. Recorder verifies shape, successful exit codes, and log existence; it cannot authenticate that a caller actually ran the claimed command. Its own structural gate receipts remain separately captured. Independent review and workflow-specific result reconciliation remain mandatory.

Run:

```powershell
pwsh -NoProfile -File .sdd/scripts/manage-sdd-evidence.ps1 -Action record -Feature wishlist -Stage plan -RequestPath .specs/wishlist/evidence-request.json
```

Stages: `spec`, `plan`, `implement`, `test`, `verify`, `review`, `document`. Recording runs relevant structural gates; test also compiles manifest projects. These receipts supplement workflow-specific checks. They do not prove behavioral correctness, review approval, browser execution, or doc-only scope. Record only after those checks pass. Then update ledger using existing workflow contract.

Backend-only Verify may use `disposition: "skipped"` plus `skipReason`, only when existing E2E workflow permits skipping. Recorder runs spec/plan gates and preserves skip reason; it creates no browser proof. Keep ledger state `Skipped`.

Hashes cover exact source bytes, including uncommitted edits, additions, deletions, contracts, and role definitions. Git HEAD supplies provenance; unrelated commits alone do not invalidate matching inputs. Implement snapshots production sources/configuration; Test also snapshots unit/component tests, excluding `tests/TheShop.E2E.Tests`; Verify and later stages snapshot all tests. Thus normal test writing preserves Implement evidence, and journey writing preserves Test evidence. Explicit file dependencies and check logs remain hashed even under excluded directories. New custom test locations require explicit `sourcePaths` and routing review.

XML documentation edits still change production bytes and invalidate Implement through Review. After doc-only gate passes, retain snapshot and gate log. Rerun affected build, tests, browser checks, and independent review against final bytes; record fresh evidence in pipeline order, then record Document. Use existing workflow permissions and independent roles. This is explicit revalidation, never automatic freshness transfer or rerunning the documenter. If required checks cannot run, leave affected evidence stale and report blocked completion. Successful doc-only scope proof alone cannot clear upstream failures.

Spec/plan recording retains exact artifact bytes under `evidence/artifacts/{sha256}/`. Amendments require matching retained original. Never edit those copies.

At stage entry, check each recorded upstream stage with `-Action check -Stage <upstream>`. Missing evidence is unknown, not passing. For legacy features, inspect artifacts and rerun upstream checks before recording initial evidence; never backfill from old success labels alone. Existing structural `status` gate remains separate, so stale downstream evidence cannot deadlock upstream repair.

Before shipping or claiming whole-feature completion, run:

```powershell
pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 evidence -Feature wishlist
```

Ship readiness also rejects stale sidecars when present. Older features remain readable without automatic migration. A hash match never creates an approval. Manual-only Document and explicit Ship remain manually invoked.

## Handoffs

Create request JSON with these fields:

| Field | Required content |
|---|---|
| `role`, `runtime` | Existing core role; `claude` or `codex`. |
| `mode`, `language` | Active official mode or `off`; dominant language. Include scoped exception in `scopedException` when applicable. |
| `ownedPaths` | Exact authorized worker paths. Existing scope gates remain authoritative. |
| `sourcePaths`, `planSections` | Upstream source files and designated plan sections. Spec, plan, role, contracts, runtime adapter are always included. |
| `literalApis` | Complete required signatures verbatim; explicit `none` only where workflow needs no upstream API. |
| `outcome`, `changedFiles`, `checks`, `unresolved`, `nextAction` | Existing handoff facts and evidence; explicit `none` for empty facts. Never infer absent checks passed. |

```powershell
pwsh -NoProfile -File .sdd/scripts/manage-sdd-evidence.ps1 -Action handoff -Feature wishlist -RequestPath .specs/wishlist/handoff-request.json
pwsh -NoProfile -File .sdd/scripts/manage-sdd-evidence.ps1 -Action check-handoff -Feature wishlist -Id <returned-id>
```

Pass generated path and literal API blocks to receiver. Receiver checks hashes before first dependent read or edit. After upstream files change, reread them, reconcile interfaces, rerun affected gates, and create fresh handoff. Do not overwrite old handoff or substitute its summary for full spec. Workers may change their owned files during work; orchestrator validates upstream dependencies again before accepting output. A stale handoff requires reconciliation, not automatic rejection of valid work.

Baseline tests within Implement use optional `purpose: "implementation-tests"` on `shop-test-writer` and `shop-test-runner` handoffs only. This purpose requires fresh Spec/Plan evidence and permits tests before Implement evidence exists. Pass current production source dependencies explicitly. Other roles and purpose values are rejected. Omit purpose during separate Test workflow: its handoffs require fresh Implement evidence. A baseline handoff grants no Test-stage completion; `record` rejects requests carrying this purpose. Separate Test still executes its own manifest, compile, run, and reconciliation checks.

## Failure feedback

Use `-Action failure` with JSON fields `classification`, `stage`, `evidence`, `reason`, `nextAction`. Classifications:

- `implementation-defect`: behavior violates confirmed requirements; fix responsible production layer.
- `test-defect`: assertion, fixture, or discovery violates confirmed requirements; fix tests, preserve coverage.
- `requirement-gap`: expected behavior absent, contradictory, or requires a new product decision; stop dependent work and resolve decision.
- `environment-problem`: runtime, service, credentials, dependency, or tooling blocks proof; report actual blocker without weakening acceptance.

Record strongest supported classification. If uncertain, state competing hypotheses in `reason`; investigate before choosing a fix. Recording failure invalidates affected stage and descendants. It changes no requirement and authorizes no edit.

Rerecording never silently resolves failures. For every unresolved failure in the stage, include `resolveFailures: [{"id":"<failure-id>","reason":"<actual correction and proof>","checkIds":["<successful-check-id>"]}]`. IDs must identify distinct unresolved failures in that stage; each check ID must reference a successful receipt in the same request. Explain how those checks address that specific failure. Missing resolutions, failed checks, or failing structural gates leave original state unchanged. Successful publication retains resolution reason, linked check IDs, timestamp, and historical failure. Other stages' failures remain unresolved. Recording cannot infer approval or product decisions from a successful test.

## Deliberate amendments

Resolve product or technical decisions through existing Clarify/Resolve process and current user authorization. Before editing, preserve artifact and record its SHA-256. Edit authoritative spec/plan, retain IDs, and reconcile footer/ledger state through producing workflow. Never change spec solely to make a failing test pass.

Use `-Action amend` with `target` (`spec` or `plan`), `reason`, exact `decision`, `changedIds`, `beforeHash`, `afterHash`, and `evidence` (decision source plus preserved original path). Script requires before hash to match recorded artifact and after hash to match current file. It records amendment and marks target plus downstream stages stale. It never edits requirements or asserts user consent.

Revalidate target first, then affected downstream stages in pipeline order. Preserve old tests and logs as historical evidence; regenerate coverage when requirements change. Record fresh stage evidence only after actual checks. State history remains recoverable in `evidence/history/`; rollback means restore deliberately selected artifacts and rerun checks, never copy old success labels onto changed files.
