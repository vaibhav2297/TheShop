## 10. Persist the report and the ledger

Get today's date and `git rev-parse --short HEAD` (`(unknown)` if unreadable). Overwrite `.specs/{arguments}/e2e-report.md`. The file and your final response must be identical:

```markdown
# E2E report — {feature}

_Run: {date} · commit `{sha}` · verdict {verdict}_
_Snapshot of one run — regenerate with `$theshop-e2e {feature}`._

## Coverage classification

| Bucket | ACs | Count |
|---|---|---|
| Browser-proven (`e2e`) | AC-6, AC-8, … | {n} |
| Proven below the browser (`unit`) | … | {n} |
| Human-only (`manual`) | … | {n} |

{One line per `manual` AC with its reason.}

## Journey run

{Metrics: expected · discovered · passed · failed · skipped · repair rounds used · duration. Or an explicit gate-halt state with "the journey did not run".}

## Acceptance criteria

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | unit | ✅ Passed | {test FQN, from the unit manifest} |
| AC-6 | e2e | ✅ Passed | {test FQN} |
| AC-9 | e2e | ❌ Failed | {symptom + trace path} |
| AC-26 | manual | ⚠️ Unverified | {reason — needs a human} |

**AC status:** {n} passed · {n} failed · {n} unverified

## Failures and findings

{Per failure: FQN, classification from step 8's table, exact symptom, trace path, evidence-based root-cause hypothesis labelled as a hypothesis, and the concrete file/component to inspect. Missing `data-testid` hooks listed as their own finding. "None" when clean.}

## Environment

- Stack: {started / reused} · app: launched by `AppHostFixture` · port 5218 {free after run}
- Supabase stack left running — `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1` to stop it.

## Verdict

**{verdict}**

{One sentence of justification and the exact next step.}
```
