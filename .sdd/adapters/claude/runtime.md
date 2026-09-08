# Claude Code adapter

Use the generated `.claude/skills/` definitions and `.claude/agents/` roles. Invoke a workflow as `/theshop-plan add-brand`; the dotted legacy aliases remain supported. `$ARGUMENTS` is native invocation text. Do not treat an unresolved placeholder as a feature name.

Use Claude's currently available delegation tool to select the named agent. Older workflow references to `Task` and newer clients' `Agent` tools describe the same delegation capability; inspect the available schema. Start independent sibling tasks before awaiting their results. Read the shared role contract before interpreting a child's output.

Pass active communication mode, language, and scoped exceptions in each child prompt. Each native role loads `.sdd/contracts/execution.md`, which loads shared communication policy. Full applies to saved prose too; preserve auto-clarity. No Caveman plugin, hook, or Cavekit skill is required.

The role frontmatter preserves the existing Claude tools, model, and color. It never grants authority beyond the host's permissions. MCP tool names in generated bodies are the existing Claude bindings; if a binding is unavailable, discover the corresponding capability from the connected tools and validate its schema and target. Report a missing capability instead of fabricating a result.

Existing permissions and exploration reminders are preserved. The PostToolUse design hook converts Claude's edit payload to explicit paths for the shared checker and maps a violation to hook exit code 2. Whole-diff formatting is removed from PostToolUse because parallel workers must not modify each other's files. The orchestrator runs the shared formatter after workers finish, then repeats affected checks. The legacy `format-on-stop.ps1` path remains callable explicitly. Both runtimes require explicit verification even when hooks are unavailable.

Shared scripts use PowerShell 7 and accept repository paths independently of `$CLAUDE_PROJECT_DIR`. Hooks may use that environment variable to locate their adapter entry point.
