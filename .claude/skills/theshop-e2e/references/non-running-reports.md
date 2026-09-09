## Templates for the non-running outcomes

Both templates are **persisted, not just printed**: write the filled template to `.specs/$ARGUMENTS/e2e-report.md` (overwriting), update the `5. Verify` row per the step 10 table, and emit the identical text as your response. The ledger rows for `Skipped` and `Halted` both link to `e2e-report.md`, so skipping the write leaves a dangling link — and a reader six weeks later has no record of why the journey never ran.

### Template B — backend-only

```markdown
# E2E report — {feature}

**⏭️ Skipped — backend-only feature.**

`{feature}` has no Web-layer surface (no Phase 4, no Figma references, no `.razor` files), so there is nothing to drive in a browser. Its unit and integration tests via `/theshop-test {feature}` are the appropriate gate.
```

### Template C — halted before the journey ran

```markdown
# E2E report — {feature}

**⛔ Halted — the journey did not run.**

**Stage:** {upstream manifest gate / e2e gate / E2E build / environment}

**Reason:** {quote the exact violations, compiler errors, or environment failure}

**Next step:** {the specific fix — e.g. "add `data-testid=\"category-row-delete\"` to ManageCategories.razor via /theshop-implement", then re-run `/theshop-e2e {feature}`}
```

Emit the canonical report with no prose before or after it.
