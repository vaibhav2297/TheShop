# Acceptance proof by stage

Unit/component Test proves its own scope. E2E owns browser proof; human checks remain explicit. Never call deferred proof passed.

Each `test-manifest.json` acceptance criterion retains `id` and `tests`. Optional `proof` selects `unit`, `e2e`, or `manual`; absent means `unit` for existing manifests. For `e2e` or `manual`, require nonempty `reason` explaining why unit/component tests cannot prove the complete criterion. Preserve mapped supporting tests; they must still run and pass. Never defer an ordinary missing unit test, failed assertion, missing production member, or unavailable environment.

- `Passed`: unit criterion has mapped tests; all discovered cases passed.
- `Deferred — E2E` or `Deferred — manual`: justified proof classification; all mapped supporting tests passed. Report exact AC IDs and reasons separately from passed count.
- `Failed`: any mapped test failed, skipped, or did not run, regardless of classification.
- `Not Covered`: unit criterion has no mapped test. Blocks Test.

Test may record `Passing` with `Ready for E2E — deferred proof remains` only when reconciliation matches, every required unit/component test passes, no skips/warnings exist, and every AC is Passed or validly Deferred. This is Test-stage success only. All-pass reports may retain their existing verdict. Add deferred count and IDs to existing report tables and ledger evidence; never include them in passed AC count.

Writer classifies from full spec and plan. Runner verifies classification against those sources before accepting it. Invalid classification blocks Test. Run manifest gate before execution; script checks shape, not the truth of a reason.

E2E reads each deferred ID and owns its completion. An `e2e` deferral requires matching E2E classification; `manual` may become automated E2E proof or remain a human check. Never turn either into `unit` merely because supporting tests exist. Backend-only skip is forbidden while deferred proof remains. Human-only checks need actual recorded human evidence or explicit waiver under existing Verify rules. Pending proof cannot become Verified or ship-ready.

These stage-specific rules govern AC status and verdict language in Test, merged Test, writer, runner, and their report templates. They add no permission to weaken tests or bypass approvals. Missing tests for newly added production members still block Implement.
