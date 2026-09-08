# Status tracker template

Every feature carries a `.specs/{feature_name}/status.md` — a one-glance view of where it sits in the SDD pipeline **and the ledger of every verification-gate outcome**. This skill **creates** it; each later step (`/theshop-clarify`, `/theshop-plan`, `/theshop-resolve`, `/theshop-implement`, `/theshop-test`, `/theshop-verify`, `/theshop-review`, `/theshop-document`) **updates its own row** plus the **Last updated** and **Next step** lines. Use this exact structure:

```markdown
# {Feature Title} — SDD Status

**Feature:** `{feature_name}`
**Last updated:** {YYYY-MM-DD}

| Stage | State | Gate | Evidence | Date |
|---|---|---|---|---|
| 1. Spec       | Draft | ✅ spec-gate pass | {N} FRs · {M} ACs · {K} open assumption(s) | {YYYY-MM-DD} |
| 2. Plan       | —     | — | — | — |
| 3. Implement  | —     | — | — | — |
| 4. Test       | —     | — | — | — |
| 5. Verify     | —     | — | — | — |
| 6. Review     | —     | — | — | — |
| 7. Document   | —     | — | — | — |

**Next step:** `/theshop-clarify {feature_name}`
```

**Gate column vocabulary** — every step records the outcome of its verification gate(s) in its own row:

| Cell | Meaning |
|---|---|
| `✅ {gate} pass` | The step's gate(s) passed — `check-sdd-gates.ps1` modes, build gates, lint, reconciliation. |
| `🔴 {gate} fail` | A gate failed and the step ended in that state (the State cell should reflect it too). |
| `⚠️ waived: {reason}` | The step proceeded past a warning gate on the user's explicit go-ahead. Waivers are **always recorded, never silent**. |
| `—` | Stage not reached yet. |

The **Evidence** cell is one line of mechanical fact — counts, migration names, failing project, gate-script summary — never a prose claim like "looks good".

**Two pipeline-wide rules every step follows:**

1. **Read the tracker on entry.** Before doing anything, check the upstream rows are in the state you expect (e.g. `/theshop-implement` expects Plan `Resolved`). If they aren't, warn the user; if the user says proceed, record `⚠️ waived: {reason}` in your own Gate cell so the skip stays visible downstream.
2. **Record your gate on exit.** State + Gate + Evidence + Date, refresh **Last updated** and **Next step**. A step only ever writes its own row.

Stage state vocabulary (a step only ever writes its own row):

| Stage | Set by | State transition |
|---|---|---|
| 1. Spec | `/theshop-spec` → `/theshop-clarify` | `Draft` → `Confirmed` |
| 2. Plan | `/theshop-plan` → `/theshop-resolve` | `Draft` → `Resolved` |
| 3. Implement | `/theshop-implement` | `Pending` → `Done` |
| 4. Test | `/theshop-test` | `Pending` → `Passing` / `Failing` |
| 5. Verify | `/theshop-verify` | `Pending` → `Verified` / `Skipped` |
| 6. Review | `/theshop-review` | `Pending` → `Approved` / `Changes requested` |
| 7. Document | `/theshop-document` | `Pending` → `Done` |

A step handling a legacy feature may recover Spec/Plan state labels from actual footers. Never backfill passed gates or approvals from those labels. Record missing proof as unknown; run producing checks and establish evidence before continuation. Clarify writes Spec row; Resolve writes Plan row. Authorized spec/plan amendments may reset dependent rows under evidence contract.
