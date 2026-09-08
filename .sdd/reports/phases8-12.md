# Phases 8–12

Implementation date: 2026-09-06. Changes local and uncommitted. Phase 7 semantic/style acceptance gaps remain deferred.

Status: phases 8–10 implemented and verified; phase 11 partial, Codex execution acceptance quota-blocked; phase 12 offline evaluation concluded with no adoption.

## Phase 8: existing skill integration

Shared [task routing](../contracts/task-routing.md) preserves upstream skills and SDD authority. Six work patterns exist in Claude and Codex discovery folders with identical bytes. No copied skills, new routing agent, or upstream rewrite.

Native routing-contract decisions pass in both clients. Codex first attempt hit model capacity; repeat passed. Checks cover six work patterns, independent SDD review, missing optional helpers, missing delegation, explicit-only stages, stale handoffs, and requirement gaps. Filesystem inventory plus prompted contract decisions do not prove every spontaneous native selection.

Evidence: [discovery](phase8-discovery.json), [Claude decisions](native-claude-routing-1.json), [Codex decisions](native-codex-routing-2.json).

## Phase 9: persisted handoffs

Implemented [evidence contract](../contracts/evidence.md) and `manage-sdd-evidence.ps1`.

- Handoffs carry role, runtime, mode, language, owned paths, source sections, exact APIs, outcome, checks, unresolved items, and next action.
- Source hashes detect uncommitted changes, additions, deletions, missing artifacts, and changed instructions. Git revision adds provenance.
- Receivers reject stale or missing upstream evidence. Stale downstream tests do not deadlock upstream implementation repair.
- Original spec/plan bytes and prior evidence states remain recoverable. Workers retain existing ownership; orchestrator writes sidecars.

Core specialist roles supported by structured handoff command. Third-party extensions retain their existing wrapper reports; extending structured role lookup requires deliberate registry integration.

Evidence: [regression checks](evidence-validation.json). Tests use isolated fixtures and actual spec/plan gates. Existing production feature artifacts remain untouched.

## Phase 10: failure feedback

Implemented implementation-defect, test-defect, requirement-gap, and environment-problem records. Deliberate amendments require matching before/after hashes and retained original artifact. Amendments invalidate target and downstream evidence; failures block completion even without prior stage records.

`check-sdd-gates.ps1 evidence` checks whole-feature freshness. Ship readiness checks sidecars when present. Existing ledger schema, gate function bodies, explicit-only actions, and approval boundaries preserved. Legacy features remain readable; initial evidence requires rerunning producing checks.

Evidence records supplement workflow verification; they do not establish semantic correctness, human approval, browser execution, or doc-only scope. Regression suite added to portability CI. CI itself has not run remotely.

## Phase 11: execution evaluation

Reusable native harness and [evaluation policy](../contracts/evaluation.md) implemented. Pilots retain raw events, scope findings, behavioral oracle output, corrected-behavior checks, elapsed time, and runtime-reported usage.

| Claude pilot | Native contexts | Seconds | Output tokens | Behavioral result |
|---|---:|---:|---:|---|
| Single implementation | 1 | 40.59 | 2,248 | Pass |
| Layered implementation | 4 | 107.97 | 3,988 | Pass |
| Merged tests | 1 | 90.72 | 6,309 | Detects defect; corrected behavior passes |
| Separate writer/runner | 2 | 122.77 | 8,310 | Detects defect; corrected behavior passes |

Separate-testing original evaluator required literal `AssertionError`, although runner caught assertions and reported failures. Reassessment uses nonzero faulty run plus passing corrected run. Original report retained; no new model answer substituted.

Codex routing passed. Execution pilots then hit account usage limit; single, layered, and merged-test runs are blocked environment results. Separate-testing Codex case remains unrun after quota exhaustion. Do not count these as architecture failures or passing comparisons.

**Acceptance remains partial:** four-boundary CommonJS fixture is synthetic; layered contexts run sequentially. Full SDD specialist execution, parallel Infrastructure/Web, real .NET feature gates, repeated representative tasks, and completed Codex comparisons remain needed before promoting task-class defaults. Existing production workflows unchanged.

Evidence: [aggregate measurements](execution-evaluation.json). Cached, uncached, and output fields retained separately. No cross-runtime cost or production token-saving claim.

## Phase 12: optional optimization evaluation

Offline candidates tested separately through `test-optimization.ps1`:

- Repeated synthetic tool log: 3,307 bytes to 189 bytes; 94.28% byte reduction. Exact expansion preserves rejection text, counts, line endings, and order.
- Shared execution memory: run-length representation increased size in initial trial. Candidate rejected.
- Local HTTP passthrough: 40 direct/proxy requests preserved exact response bytes. Timing is localhost transport data, not provider or Cloud performance.

Empty input and mixed-line-ending cases also roundtrip exactly. Original and restored bytes retained with matching SHA-256 hashes. No live memory, model route, credentials, proxy configuration, or upstream skill changed.

**Decision: adopt none.** Offline evaluation complete; model-level token/quality comparison and real streaming/authentication/cancellation behavior remain unproven. Live Caveman Cloud/proxy adoption remains optional future work with explicit configuration and measured benefit.

Evidence: [optimization trials](optimization-validation.json).

## Verification and continuation

Published adapters: **245 files clean**. **102 regression checks pass:** portability 19, Caveman preservation 10, extensions 44, evidence 29. Discovery and offline optimization checks also pass. Original gate functions and formatter remain preserved. Whitespace check passes; Git reports no changes under `src/`, `tests/`, or `.specs/`. No application C# or Razor changed, so application formatting/build tests are outside this change.

AST-only Graphify update completed: 7,849 nodes, 15,658 edges, 567 communities. Existing missing SQL parser still excludes 22 SQL files; no dependency installed or semantic model call made.

Resume blocked native comparisons after Codex quota becomes available:

```powershell
pwsh -NoProfile -File .sdd/scripts/test-sdd-native.ps1 -Runtime codex -Case single -Repeat 2
pwsh -NoProfile -File .sdd/scripts/test-sdd-native.ps1 -Runtime codex -Case layered -Repeat 2
pwsh -NoProfile -File .sdd/scripts/test-sdd-native.ps1 -Runtime codex -Case test-merged -Repeat 2
pwsh -NoProfile -File .sdd/scripts/test-sdd-native.ps1 -Runtime codex -Case test-separate
pwsh -NoProfile -File .sdd/scripts/summarize-sdd-evaluations.ps1
```

Preserve failed attempts. Passing synthetic retries still does not establish production execution defaults.
