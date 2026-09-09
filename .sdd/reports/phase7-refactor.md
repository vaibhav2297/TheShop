# Phase 7 — shared instruction refactor

Date: 2026-09-06. Status: refactor implemented; scripted preservation passes. Native semantic/style acceptance remains partial. Work uncommitted.

All 16 core workflows and nine roles refactored in canonical `.sdd/` sources. Both adapters regenerated: 251 managed native files, including existing Claude aliases. Default remains Caveman `full`; explicit levels/off and upstream auto-clarity remain unchanged. No Cavekit dependency.

## Changes

- Shortened entry prose; removed repeated explanations. Preserved inputs, ownership, ordered procedures, outputs, and completion evidence.
- Added 25 focused references for status tracking, design input, report formats, layer examples, and test diagnostics. Each caller names its required read trigger.
- Rewrote Spec, Clarify, Plan, and Resolve procedures. Other workflows and roles received targeted compression and extraction; sensitive rules and literal examples remain intact.
- Preserved all 30 numbered constitution rules verbatim. Kept commands, API signatures, schemas, report fields, templates, runtime policies, gate scripts, formatter, and third-party originals unchanged.
- Strengthened verification to follow transitive instruction references. Added cycle handling and arbitrary missing-reference regression checks. Missing paths compare against original closure, without a filename whitelist.
- Added optional baseline archive/report prefix parameters to native evaluation. Existing defaults remain compatible; phase 7 evidence stays separate from earlier reports.

Entry definitions: **365,148 to 267,449 UTF-8 bytes**, approximately **27% smaller**. This measures entry files only. Extraction moves some content into references; neither this percentage nor discovery metadata proves token, billing, or total workflow savings.

## Verification

| Check | Result |
|---|---|
| Phase 7 preservation and isolated recovery | 13/13 |
| Published runtime portability | 19/19 |
| Published Caveman contracts | 10/10 |
| Published third-party extensions | 34/34 |
| Canonical and Codex skill validation | 32/32 |
| Generated adapter drift | 251 files clean |

Total: **76 scripted checks pass**, plus **32 applicable skill validations**. Final Git whitespace check passes. These counts exclude native semantic review; its failures remain below.

[Preservation evidence](phase7-static.json) records all 25 entries, reference closure, protected literals, and **543 unchanged files**: 535 application/test/feature files plus eight historical checkpoint files. [Skill validation](phase7-skills.json) covers 16 canonical and 16 Codex skills. Claude metadata receives adapter checks and native execution checks.

An initial cross-format validator attempt rejected valid Claude-only frontmatter keys. [Attempt retained](phase7-skills-cross-format-attempt.json). Correct validation scope passes; Claude metadata was not stripped to satisfy a Codex validator.

Independent read-only review compared original and refactored workflows, roles, extracted references, and representative decision paths. Preservation corrections applied: any UI feature must read design-input guidance; every non-blocking judgment must carry inline and appendix assumption markers. Spec pre-save checks now explicitly trace facts, compare limits/order/exceptions across sections, and check selected prose level. Existing permissions, schema, and gate commands remain unchanged.

No production C# or Razor edits. Application builds and feature test runs are outside this instruction-only change. PowerShell parsing, adapter generation, scope rejection, permission boundaries, reference preservation, and isolated runtime gates supply relevant verification.

## Native evidence

| Exercise | Evidence/result |
|---|---|
| Before-refactor Claude decisions | [Pass](phase7-baseline-claude-decisions-1.json) |
| Before-refactor Codex decisions | [Pass](phase7-baseline-codex-decisions-1.json) |
| Before-refactor Claude spec | [Structural pass; manual caveats below](phase7-baseline-claude-spec-1.json) |
| Refactored Claude decisions | [Pass](phase7-candidate-claude-decisions-1.json) |
| Refactored Codex decisions | [First formatting failure](phase7-candidate-codex-decisions-1.json); [repeat passes](phase7-candidate-codex-decisions-2.json) |
| Claude creates spec | [Spec and status gates pass](phase7-candidate-claude-spec-1.json) |
| Codex resumes same spec | [Clarify and status gates pass](phase7-candidate-codex-clarify-1.json) |
| Claude resumes confirmed spec to plan | [Plan and status gates pass; Draft](phase7-candidate-claude-plan-1.json) |
| Claude spec after restored assumption wording | [Structural pass; semantic failures below](phase7-candidate-claude-spec-2.json) |
| Claude spec after explicit pre-save fidelity checks | [Structural pass; remaining semantic/style limits below](phase7-candidate-claude-spec-3.json) |

First Codex candidate recognized scoped `lite` correctly but appended an explanation inside a field requiring an exact mode name. Independent review confirmed correct semantics. Evaluation prompt now explicitly requires canonical names in those fields; repeat passes. Preserve first failure. This format-only prompt difference prevents treating that repeat as an identical paired cost experiment.

Initial Claude spec retained requested limits, privacy, session clearing, accessibility, and exclusions. Actual gates passed with six FRs, 11 ACs, and zero recorded assumptions. Manual review found an unsupported current-state/cause claim, English/French coverage without an explicit bilingual AC, and mostly normal/lite artifact prose despite full policy. Broad assumption-marking instruction restored before second spec run. First pilot remains unchanged evidence.

Second candidate spec passed structural gates but failed semantic review: unsupported narrative, unconfirmed session/removal/data-access choices, and a length edge case contradicting its after-trim rule. French AC became explicit. Nine FRs, 10 ACs, zero declared assumptions. These failures motivated explicit pre-save fidelity checks; no pilot artifact was manually repaired.

Original skill's paired spec also invented memory/paper tracking history while declaring zero assumptions. Original prose likewise drifted from full. Its length rule and French AC remained consistent; nine FRs and 11 ACs. Unsupported narrative and style drift predate this refactor. Small sample cannot establish systematic regression, and original failures do not excuse candidate contradictions.

Final candidate spec fixes contradictory trim/length handling and removes prior no-confirmation/no-product-read additions. Core supplied facts remain. Fourteen FRs, 10 ACs, zero declared assumptions; gates pass. Manual review still finds unsupported workflow/cost narrative, unconfirmed browser-close session semantics, no explicit bilingual AC despite retained bilingual requirement, and mostly normal/lite prose. Pre-save wording improves this sample but does not establish reliable factual/style compliance. Do not label it a clean semantic pass.

Cross-runtime continuation completed: Claude Spec, Codex Clarify, Claude Plan in one isolated fixture. Final ledger records Confirmed Spec and Draft Plan; all 11 ACs mapped. Plan records missing Figma IDs, unverified reuse, and permission questions. No database/migration proposed. This continuation used first candidate spec, before later spec fidelity wording changes; only separate spec runs exercise those corrections.

Plan remains unsuitable for direct implementation without Resolve and review. Manual inspection finds overly broad structural isolation claims, sign-out-only clearing despite broader session-end requirements, and a downstream task listing Document despite manual-only project policy. Domain test ownership also reflects an existing workflow conflict. These are visible pilot quality limits, not approved implementation decisions. Structural gates do not resolve them.

Mode fields demonstrate recognition, not consistent prose adherence. Schema gates cannot establish factual completeness or style. These disposable pilots exercise representative decisions and persisted Spec/Clarify/Plan handoffs; they do not prove an entire production implementation or guaranteed savings. Runtime usage and elapsed time remain in individual reports; raw traces and copied artifacts remain under ignored `.sdd/.test-work/caveman-native/`.

Local AST graph refreshed: 7,806 nodes and 15,607 edges. SQL extraction remains unavailable without `tree_sitter_sql`; semantic labels were not regenerated. No cloud graph labeling run.

## Recovery and next scope

Immutable phase 7 snapshot: [inventory](../baseline/phase7/inventory.json) and [archive](../baseline/phase7/before-valid.zip). Isolated recovery recreated all 342 archived files byte-for-byte; recovered generator reported 198 clean native files. No root restoration performed. Inspect archive and current diff before any rollback; preserve independent edits and remove only reviewed phase 7 additions. Existing earlier checkpoint seals remain unchanged.

Run `pwsh -NoProfile -File .sdd/scripts/test-phase7.ps1` for this migration audit. It freezes historical application/spec bytes; keep it outside ongoing feature CI. Continue normal `test-portability.ps1 -Published`, `test-caveman.ps1 -Published`, and `test-extensions.ps1 -Published` checks for maintained definitions.

Phase 8 remains pending: explicit task routing. Source refactoring and adapter generation are implemented; reliable native semantic/style adherence remains an open acceptance item. Existing workflow conflicts need separate decisions; this phase preserves their current authority rather than silently changing behavior. No commits, pushes, service writes, or production migrations performed.
