## Step 4 — Unified report

Produce the report in **exactly** this structure. No prose around it.

```markdown
# Code Review Report — {arguments}

## Scope

- Files changed: {N} ({list from `git diff --name-only`})
- Spec: `.specs/{arguments}/spec.md` ✅
- Reviewers run: `shop-code-security-reviewer`, `shop-code-quality-review` (parallel)
- Localization (French): {✅ all feature keys translated / 🔴 {N} key(s) untranslated — see action plan}
- Design-rule lint: {✅ clean / 🔴 {N} violation(s) — see action plan}
- Manifest drift: {✅ matches reality / 🔴 {N} drift violation(s) — see action plan / ⏭️ no manifest yet}
- Rule 29 (new code has tests): {✅ covered / 🔴 {N} untested unit(s) — see action plan / ⏭️ no new units in diff}

---

## Security Findings

{Full output from shop-code-security-reviewer, verbatim. Keep its 🎓 / 💡 / 🌱 / ✅ sections intact.}

---

## Quality Findings

{Full output from shop-code-quality-review, verbatim. Keep its 🎓 / 💡 / 🌱 / ✅ sections intact.}

---

## Combined Action Plan

*Ordered checklist, top to bottom = highest priority to lowest. Items merged across reviewers appear once with both perspectives noted.*

### Must fix before committing

1. **🌐 [Localization – Blocking]** `Strings.fr.resx` — {N} feature key(s) untranslated: {list keys}
   - Why: bilingual (FR) coverage is a product/legal requirement; `[TODO]`/empty French strings would ship to users.
   - Action: add real French translations for the listed keys in `src/TheShop.Web/Resources/Strings.fr.resx`.
   *(Include this item only when the localization pre-flight failed. It always sits at the top of "Must fix".)*

2. **🚨 [Security – Critical]** `{file:line}` — {one-line title}
   - Why: {one sentence}
   - Action: {one sentence}

2. **⚠️ [Security – Important]** `{file:line}` — {title}
   - Why: {one sentence}
   - Action: {one sentence}

3. **💡 [Quality – Worth improving]** `{file:line}` — {title}
   - Why: {one sentence}
   - Action: {one sentence}

### Worth addressing soon

4. **[Security – Low]** `{file:line}` — {title}
   - Action: {one sentence}

5. **🌱 [Quality – Polish]** `{file:line}` — {title}
   - Action: {one sentence}

*(If a category is empty, omit its items. If both "Must fix" and "Worth addressing soon" are empty, write under the heading: "No items in the action plan — clean diff.")*

---

## Overall Verdict

**{One of three exactly}:**

- ✅ **APPROVED — ready to commit**
- 🟡 **APPROVED WITH SUGGESTIONS — can commit, address suggestions in future steps**
- 🔴 **CHANGES REQUESTED — must fix before committing, see action plan above**

{One or two sentences explaining the verdict. For 🔴, name the blocker(s). For 🟡, name the suggestions worth flagging. For ✅, briefly say what was done well.}
```
