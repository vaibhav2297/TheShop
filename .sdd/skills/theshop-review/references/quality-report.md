## Output format

```markdown
Quality Review — {Feature/Step Name}

🎓 **What I checked**

- Scope: `git diff {range}` — {N} files changed
- Files reviewed: `{path}`, `{path}`, `{path}`
- I looked at: architectural compliance (SKILL.md Rules 1–10; `checklists/code-generation.md`), design system compliance (Rules 11–28; `checklists/design.md`), and everyday code craft (function size, duplication, leftover cruft, formatting).

---

💡 **Worth improving**

### 1. {Short title for the finding}

- **Where:** `path/to/File.cs:42-58`
- **What it is:** {Plain-language description — e.g., "this handler is calling Supabase directly instead of going through the repository interface"}
- **Why it matters:** {One or two sentences. Tie it to the rule or the maintenance pain.}
- **How to improve it:**

  ```csharp
  // Concrete suggestion in TheShop style — show the shape, not pseudocode
  ```

### 2. {...}

*(One section per finding. If you grouped multiple similar issues, list the locations: "Also at `File.cs:88`, `OtherFile.cs:12`".)*

---

🌱 **Polish ideas**

- `path/to/File.cs:104` — {one-line observation with a brief suggestion}
- `path/to/Other.cs:22` — {...}

*(Bullets are fine here — these are smaller. If there are none, write "Nothing pressing — nice clean diff.")*

---

✅ **Doing well**

- **{Specific thing}** in `path/to/File.cs` — {why it's good, in one short sentence}
- **{Specific thing}** in `path/to/Other.cs` — {why it's good}
- *(Aim for 3–4. Always specific, never generic. If you genuinely couldn't find anything, write "Nothing jumped out yet — keep going, more to celebrate once the feature lands.")*
```

---
