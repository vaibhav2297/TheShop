## Verdict

Emit exactly one template. No extra prose.

### Template A — Verification ran

```markdown
# E2E Verification — {arguments}

## Scope
- Spec: `.specs/{arguments}/spec.md` ✅
- Surface: user-facing ({route(s) checked})
- Driver tier: {Tier 1 — automated / Tier 2 — guided manual / Tier 1 — automated ({n} ACs) + Tier 2 — guided manual ({m} ACs)}

## App launch
- Build: ✅ clean
- Launch: {✅ ready at http://localhost:5218 / 🔴 failed to start}
- Startup errors: {None / quote the exception}

## Acceptance criteria
| AC | Result | Evidence / note |
|---|---|---|
| AC-1 | {✅ Pass / ❌ Fail / ⚠️ Unconfirmed} | {what was observed, or why unconfirmed} |
| AC-2 | {✅ Pass / ❌ Fail / ⚠️ Unconfirmed} | … |

**AC status:** {N} pass · {N} fail · {N} unconfirmed

## Teardown
- App stopped; port 5218 free.

## Verdict
{One of:}
- **✅ VERIFIED — feature works in the running app**  *(use only when the app launched with no startup errors AND every AC is ✅ Pass)*
- **🔴 NOT VERIFIED**  *(use for any startup error, any ❌ Fail AC, or any ⚠️ Unconfirmed AC)*

{One sentence justifying it. For 🔴, name the failing/unconfirmed AC(s) or the startup error.}
```

### Template B — Not applicable (backend-only)

```markdown
# E2E Verification — {arguments}

**⏭️ Skipped — backend-only feature.**

`{arguments}` has no Web-layer surface (no Phase 4 / Figma references / `.razor` files), so there's nothing to drive in a browser. Its unit and integration tests via `{{command:theshop-test}} {arguments}` are the appropriate gate.
```

### Template C — Halted before driving

```markdown
# E2E Verification — {arguments}

**🔴 Halted — could not verify.**

**Reason:** {missing spec / red build / app failed to start — quote the evidence}

**Next step:** {what the user must fix, then re-run `{{command:theshop-verify}} {arguments}`.}
```

---
