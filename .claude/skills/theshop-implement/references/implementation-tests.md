# Tests before Implement completion

Project rule 6 and constitution rule 29 require tests alongside new handlers, repositories, value objects, and Domain methods. Layer workers remain production-only. Test writer owns test source; runner independently executes it. This verification belongs to Implement completion; separate Test stage still owns full feature coverage and its ledger row.

1. Inventory new required members from production workers' literal APIs and changed files. Pass full spec, plan, exact APIs, and this inventory to test writer. Request matching test per required member, spec-derived assertions, feature traits, and manifest. Reuse existing valid tests; never weaken them to match code.
2. Writer reports member-to-test mapping and remaining AC proof. Missing mapping blocks completion. Do not require a unit test to claim browser or human-only proof.
3. After all writers finish, orchestrator formats changed files and runs solution build, design checks, manifest gate, and compile gate. Test-source errors permit one writer correction; production errors return to owning production role. Preserve existing per-role retry bounds.
4. Runner executes manifested tests from stable post-format files. Require exact discovery reconciliation, zero failed/skipped tests, and successful mapped tests for each required member. Missing browser/manual proof remains explicit; it is not passed by this step.
5. Save runner evidence and mapping through orchestrator. Record Implement only after checks pass. Keep Test row unchanged until its workflow runs.

Both handoffs use `purpose: "implementation-tests"`; only test writer and runner may use that purpose. It requires fresh Spec/Plan evidence, so unrecorded Implement cannot deadlock its own tests. Ordinary Test-stage handoffs still require fresh Implement evidence.

Single-context `theshop-execute` performs same member mapping, test authoring, formatting, and targeted execution itself. It reads test-writing/runner contracts for assertions and reconciliation, with their delegation instructions adapted to its explicit no-subagent contract. Never invoke layered workflow implicitly.
