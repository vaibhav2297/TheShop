# Authentication — SDD Status

**Feature:** `authentication`
**Last updated:** 2026-06-11

| Stage | State | Gate | Evidence | Date |
|---|---|---|---|---|
| 1. Spec       | Draft   | 🔴 spec-gate fail | pre-template spec: no In/Out-of-Scope block, no Assumptions appendix, malformed footer (4 violations) | 2026-05-20 |
| 2. Plan       | Draft   | ⚠️ unresolved | plan-gate ✅ (structure + 12/12 ACs mapped) but 3 ❓ + 4 📌 open in Section 11 — `/theshop.resolve` never run | 2026-05-20 |
| 3. Implement  | Done    | ⚠️ waived: built on Draft plan (3 open ❓) | built clean at the time; scope gate did not exist yet (retro-record) | — |
| 4. Test       | Failing | 🔴 build gate | Application.Tests does not compile — `AuthErrorKeys.CodeInvalid`/`CodeExpired` renamed to `CodeInvalidOrExpired` in `d9cdd76`; manifest (194 tests) now stale vs code | 2026-06-09 |
| 5. Verify     | —       | — | — | — |
| 6. Review     | —       | — | — | — |
| 7. Document   | —       | — | — | — |

**Next step:** fix the `AuthErrorKeys` drift in the Application tests and re-run `/theshop.test authentication`; run `/theshop.resolve authentication` to clear the plan's 3 open questions. (The spec predates the current template — regenerate or hand-patch it to pass `check-sdd-gates.ps1 spec -Feature authentication` before the next clarify/plan pass.)
