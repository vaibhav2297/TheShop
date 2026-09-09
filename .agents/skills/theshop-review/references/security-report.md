## Output format

```markdown
Security Review — {Feature/Step Name}

🎓 **What I checked**

- Scope: `git diff {range}` — {N} files changed
- Files reviewed: `{path}`, `{path}`, `{path}`
- I looked at: secrets handling, Row-Level Security & authorization, input validation, sensitive data leakage, auth flow correctness, and transport/CORS/dependencies.

---

💡 **Worth improving**

### 1. 🚨 {Critical finding title}  *(or ⚠️ Important / no marker)*

- **Where:** `path/to/File.cs:42-58`
- **What it is:** {Plain-language description — e.g., "the Stripe secret key is being read into a constant in the Web project, which ships to the browser"}
- **Why it matters:** {One or two sentences. Tie it to the actual risk. Be honest about severity without being scary.}
- **How to improve it:**

  ```csharp
  // Concrete suggestion in TheShop style — show the shape
  ```

### 2. {...}

*(One section per finding. If you grouped multiple similar issues, list the locations under "Also at".)*

---

🌱 **Polish ideas**

- `path/to/File.cs:104` — {one-line observation with a brief suggestion}
- `path/to/Other.cs:22` — {...}

*(If there are none, write "Nothing pressing — clean diff from a security angle.")*

---

✅ **Doing well**

- **{Specific thing}** in `path/to/File.cs` — {why it's good, in one short sentence}
- **{Specific thing}** in `path/to/Other.cs` — {why it's good}
- *(Aim for 3–4. Always specific. If nothing genuinely stands out, write "Nothing jumped out yet — keep going.")*
```

---
