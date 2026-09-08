### Pre-write halt

Read `.sdd/contracts/test-proof.md` before classification or reporting. It defines Passed, Deferred, Failed, and Not Covered, including the stage-specific `Ready for E2E — deferred proof remains` verdict. Deferred ACs stay outside passed counts; supporting tests still must pass.


If input/spec validation halted before test files were written, do not write `test-report.md` or change `status.md`. Emit only:

```markdown
# Test report — {feature}

**Status:** ⛔ Blocked — tests were not written.

**Reason:** {specific reason}

**Next step:** {specific resolution, then rerun `/theshop-test-merged {feature}`}
```

### Artifact gate, build gate, or completed run

For every run that wrote tests and reached a gate, overwrite `.specs/$ARGUMENTS/test-report.md`. The file and final response must be identical and use this structure:

```markdown
# Test report — {feature}

_Run: {date} · commit `{sha}` · verdict {verdict}_
_Snapshot of one run — regenerate with `/theshop-test-merged {feature}`._

## Tests written

{each changed/listed file and its feature-case count}

## Tests run

{completed metrics table, or an explicit Artifact gate / Build failed state with exact errors and "tests did not run"}

## Failures and warnings

{each actionable failure/warning with evidence; "None" when clean}

## Acceptance criteria

{one id-only row per AC with Passed, Failed, Not Covered, or Unverified}

**AC status:** {counts}

## Verdict

**{verdict}**

{one concise justification and exact next step}
```

For a completed run, the metrics table must include expected, discovered/run, reconciliation, passed, failed, skipped, pass rate, and status. For an artifact-gate failure, quote the violations and state tests were not run. For a build failure, quote compiler errors and state tests did not run. Put the complete useful diagnosis in this report; never refer to hidden agent output.
