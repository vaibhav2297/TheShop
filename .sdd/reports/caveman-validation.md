# Caveman validation

Date: 2026-09-06. Scope: 16 workflows, 9 roles, Claude Code and Codex. Shared policy published; production application, tests, and feature records preserved. Changes remain uncommitted.

## Configuration and preservation

- `full` default for all prose, including saved artifacts. Other official levels or normal mode require explicit request, except upstream auto-clarity.
- Auto-clarity retains security warnings, irreversible confirmations, ambiguous sequences, and clarification. Resume selected mode afterward.
- One policy at `../contracts/communication.md`. Every native workflow/role names it directly. No recursive skill invocation or Cavekit grammar/dependency.
- Upstream skill snapshot content-pinned, with MIT attribution and explicit project overrides. No startup download, proxy, cloud, telemetry, or model-routing dependency.
- Catalog identities, aliases, invocation settings, ownership, gate scripts, formatter, templates, and executable examples preserved against phase 5.

| Check | Result | Evidence |
|---|---|---|
| Portable adapters, hooks, gates, generation, original rollback | 32/32 passed | [Portability report](static-verification.json) |
| Caveman policy reachability, preservation, publication, phase 5 rollback | 14/14 passed | [Caveman static report](caveman-static.json) |
| Skill frontmatter validation | 16/16 passed | [Skill validation](caveman-skill-validation.json) |
| Published native files | 187 generated; drift check passed | Caveman static report |
| Protected application/test/spec files | 533 unchanged | Caveman checkpoint check |
| Tracked whitespace check | `git diff --check` passed | Local command exit 0 |
| Knowledge graph refresh | `graphify update .` passed; AST only | 7,564 nodes, 15,434 edges, 474 communities |

Rollback proof uses an isolated copy. It rejects independent edits before writes, previews without mutation, restores all 244 phase 5 files byte-for-byte, removes only owned additions, and retains unrelated files. Root workspace stays on Caveman. Original Claude-only baseline remains separate.

Root candidate sealed at `2026-09-06T06:47:13.2930070Z`: 275 managed files. Final root preview passed: restore 244 phase 5 files, remove 31 owned additions. No root restoration applied. Final generator drift check: 187 files clean.

## Native behavior evidence

Authenticated native clients ran in temporary workspaces outside this repository's instruction ancestry. Baseline fixtures use the captured phase 5 archive; candidate fixtures generate from shared sources. Actual application services, implementation, deployment, and remote Git actions were excluded.

Decision exercises passed in all six scored runs: Claude baseline/candidate twice each; Codex baseline/candidate once each. Each run preserved these decisions:

1. Blocking product ambiguity prevents spec/ledger writes.
2. Draft plan with unresolved questions prevents implementation; proceeding requires explicit waiver.
3. Domain role does not write tests outside its scope; inherited project conflict remains visible.
4. Missing literal public API blocks Application; one Domain retry, then halt.
5. Expected 12 tests, discovered 9: `NOT READY`; never lower manifest to hide missing tests.
6. Critical security finding: `CHANGES REQUESTED`; no fix without required approval.

Exact API signature and error survived every scored decision exercise. Saved summaries retained build result, warning/error counts, changed files, missing handoff, remaining retry, migration status, and approval status. These are hypothetical supplied facts, not claims that production builds/tests ran.

Real artifact pilots also ran native workflows and independently executed spec/status gates. Claude baseline spec and candidate specs passed. Codex resumed each Claude spec and confirmed its shared ledger; candidate Codex also created a fresh spec. Both baseline and final candidate completed Claude spec, Codex clarify, then Claude plan against the same respective feature folder. Both plan/status gates passed. Candidate plan maps 12/12 ACs and retains Draft state with 2 open questions, 3 assumptions, and 2 risks. No implementation stage was marked complete.

All 15 scored native runs passed their declared mechanical checks: 6 decision exercises, 4 spec runs, 3 clarify runs, and 2 plan runs. This count excludes unscored parser failures and does not imply style or implementation readiness. Reports: `caveman-{variant}-{runtime}-{case}-{repeat}.json`. Each records actual exit, elapsed time, checks, usage, fixture, and log directory.

Clarify fixtures explicitly authorize reconciliation of a Draft footer with zero open assumptions. This tests continuation under supplied authorization; it does not prove that inherited zero-assumption shortcut works without that instruction.

## Measured size and usage

| Shared source measure | Phase 5 | Candidate | Change |
|---|---:|---:|---:|
| Discovery descriptions, UTF-8 bytes | 11,299 | 3,553 | 68.6% smaller |
| Combined 25 entry files, UTF-8 bytes | 415,836 | 365,148 | 12.2% smaller |

Entry reduction includes moving optional examples and graph construction into references. Required content remains available. These measurements are bytes, not token or billing estimates; descriptions also occur in entry files, so do not add both savings.

Native decision measurements below compare within each runtime. Claude trace model: `claude-sonnet-5`. Codex trace did not expose model identity. Output counts include runtime reasoning/thinking accounting.

| Runtime/sample | Variant | Total input including cache | Cache reads | New input/cache creation | Output | Seconds |
|---|---|---:|---:|---:|---:|---:|
| Claude 1 | Baseline | 694,393 | 590,315 | 104,078 | 12,404 | 147.29 |
| Claude 1 | Candidate | 541,999 | 445,628 | 96,371 | 9,919 | 114.77 |
| Claude 2 | Baseline | 549,396 | 451,479 | 97,917 | 11,038 | 133.76 |
| Claude 2 | Candidate | 423,880 | 330,973 | 92,907 | 8,629 | 108.63 |
| Codex 1 | Baseline | 1,229,649 | 1,134,464 | 95,185 | 8,836 | 290.84 |
| Codex 1 | Candidate | 845,202 | 691,968 | 153,234 | 6,102 | 242.85 |

Claude total input sums `input_tokens`, cache creation, and cache reads. Codex total input already includes cached input; new input is the difference. Cache categories have different pricing semantics. Codex candidate used more uncached input despite fewer total/output tokens. No invoice savings claim.

Decision samples favored candidate output size. Artifact pilots did not consistently improve: Claude spec baseline output was 6,703 tokens, candidate first pilot 8,014, corrected-bootstrap pilot 8,965. Generated AC counts, reading behavior, and reasoning differed. Small sample, cache state, concurrent local load, and nondeterminism prevent a universal savings or latency claim. Decision runs preceded the final direct-bootstrap refinement; final artifact pilots and static checks cover that refinement.

Claude plan output: baseline 32,403 tokens in 363.07 seconds; candidate 30,860 tokens in 341.26 seconds. Plans came from independently generated specs with different AC counts. These are observed run totals, not a controlled measure of compression alone.

## Artifact style and review

Mode recognition, policy loading, schema conformance, and actual style are separate observations. Early decision reports call recognition `style`; newer reports call it `modeSelection` and explicitly state its limit. Neither proves uniform prose adherence.

Independent review confirmed decision semantics, complete saved summaries, and normal-English clarification/security warnings. It found two issues, both fixed and rechecked: explicit Graphify update/build operations must precede question routing; mode recognition must not be labeled complete style validation.

First Claude spec trace skipped the communication reference inside execution contract. Generator now names communication policy directly before writing; second Claude spec trace confirms that read. Saved summaries are concise, but some spec passages still remain verbose even after direct loading. Default policy is implemented; uniform `full` style across every native response is not established. Do not strip facts or weaken artifact schemas to manufacture smaller results.

Independent review of both final-bootstrap spec pilots confirms policy reads in both traces and retention of core checklist behavior, limits, privacy, rejection rules, localization, and accessibility. Claude artifact contains 1,384 whitespace-delimited words and 12 ACs; Codex contains 1,936 words and 16 ACs. Both mostly use conventional prose. Word counts include required headings/tables; they are not style scores.

Review also found wording risks: Claude's isolated phrase "case and spacing" could imply ignoring internal spaces, although its constraints/rules correctly limit trimming to surrounding spaces. Both paraphrase "No persistence" as "no persistence across sessions." Session lifetime remains explicit, but storage boundaries deserve clarification before implementation. These evaluation artifacts were not manually corrected to manufacture a pass. Schema gates do not detect every semantic ambiguity.

Final plan review confirms explicit missing-Figma/source assumptions and Draft state. Residual issues remain for the fixture's Resolve stage: fresh-instance verification alone does not prove clearing on logical session expiry; a possible permission seed migration mentioned in Section 11 conflicts with the supplied no-migration boundary and cannot be adopted without changing authorized scope. No migration task was created. Plan prose also remains partially compressed (3,088 whitespace-delimited words). This is a migration pilot artifact, not an approved production plan.

## Evaluation limits and retained evidence

- First unscored Claude/Codex decision attempts finished native execution, then hit a harness parser bug on blank JSONL lines. Parser fixed; both runs repeated. Discarded attempts remain in ignored logs and are excluded from scored results.
- Validator initially lacked PyYAML. Installed PyYAML 6.0.3 only into ignored test directory; all 16 shared skills then validated. No application/global Python dependency changed.
- Knowledge graph launcher failed inside sandbox; permitted retry succeeded. Refresh reports 46 files with no extracted nodes and 22 SQL files skipped because `tree_sitter_sql` is unavailable. No semantic labeling or optional dependency installation ran. Disposable evaluation directories are absent from the refreshed graph.
- Full native logs and fixture artifacts stay under ignored `.sdd/.test-work/caveman-native/` and dedicated temporary fixtures. Durable JSON reports retain measurements and locations; logs are local evidence, not repository dependencies.
- Existing breadcrumbs gate failures and role/project conflicts predate Caveman. [Adoption report](caveman-adoption.md) lists them; migration preserves their behavior.
- No full production feature, E2E journey, live implementation delegation, or shipping action ran. Static native-role checks and decision exercises support compatibility; they do not prove every future model response.

Future upgrades: retain phase 5 baseline, evaluate new upstream policy/client changes in isolated fixtures, compare actual artifacts and decision correctness, then regenerate adapters. Keep metrics separate from claims of quality or cost.
