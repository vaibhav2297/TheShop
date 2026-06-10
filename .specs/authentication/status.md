# Authentication — SDD Status

**Feature:** `authentication`
**Last updated:** 2026-06-09

| Stage | State | Date |
|---|---|---|
| 1. Spec       | Draft    | 2026-05-20 |
| 2. Plan       | Draft    | 2026-05-20 |
| 3. Implement  | Done     | — |
| 4. Test       | Failing  | 2026-06-09 |
| 5. Verify     | Pending  | — |
| 6. Review     | Pending  | — |
| 7. Document   | Pending  | — |

> Test note: the Application test project does not currently compile — `VerifyOtp*` tests reference `AuthErrorKeys.CodeInvalid` / `CodeExpired`, but the source now exposes a single `CodeInvalidOrExpired` key (renamed in commit `d9cdd76`). Pre-existing drift, unrelated to the `.specs/` migration.

**Next step:** `/theshop.test authentication` (fix the `AuthErrorKeys` drift first)
