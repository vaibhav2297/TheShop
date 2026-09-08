# Codex adapter

Codex loads project instructions through `AGENTS.md`, skills from `.agents/skills/`, and native custom agents from `.codex/agents/`. Invoke a workflow by mentioning its skill, for example `$theshop-plan Create a plan for add-brand.` Keep the feature and options in the prompt. `{arguments}` in the procedure denotes that input; Codex does not perform Claude's `$ARGUMENTS` substitution.

The generated `agents/openai.yaml` preserves explicit-only invocation for workflows that already require it. It is skill metadata, not a subagent definition. Custom role TOML files contain `name`, `description`, and `developer_instructions`; they inherit the authorized model configuration rather than guessing an equivalent to Claude's model. Read-only review roles request a read-only sandbox. The host's live permissions remain authoritative, and an instruction cannot grant new tools or writes.

Use the actual delegation tools exposed by the running Codex client. When it supports selecting a custom role, select the generated role by name. If the host exposes generic worker spawning instead, read `.codex/agents/<role>.toml` and give the child its complete decoded `developer_instructions`, this adapter, and the exact task inputs; do not assume the child received the parent's skills. This uses rendered instructions, not raw generator tokens from `.sdd/roles/`. Preserve every scope, independence requirement, dependency, and retry bound. An unavailable required delegation capability is a blocked workflow, not permission to perform it inline.

Include active communication mode, language, and scoped exceptions in each child prompt. Each role loads `.sdd/contracts/execution.md`, which loads shared communication policy. Full applies to saved prose too; preserve auto-clarity. No Caveman plugin, hook, or Cavekit skill is required.

Resolve capabilities using the tools actually exposed by the host:

| Capability | Resolution |
|---|---|
| File reading and search | Native file/search tools or a permitted shell search |
| File editing | Native patch/edit tools within the role's scope |
| Shell execution | Native execution tool; PowerShell 7 for `.ps1` scripts |
| Parallel workers | Native spawn/delegate, messaging, and await tools |
| Figma node data, variables, typography, screenshots | Connected Figma tools with the matching function and correct file/node |
| MudBlazor documentation | Connected component-documentation tools; use an existing workflow's documented fallback only |
| Supabase schema, migrations, SQL, advisors, logs | Connected Supabase tools; verify the project and operation schema before mutation |
| Browser verification | The project's Playwright runner and browser dependencies, as specified by the E2E workflow |

Generated bodies identify MCP operations as `capability:<provider>.<operation>`. These are semantic labels, not callable tool names. Discover the real tool and translate its parameter schema. If no matching capability is exposed, report the exact blocked step. Do not copy Claude tool names into Codex calls, install an integration, or change credentials merely to satisfy a capability check.

Run all required gate and formatting scripts explicitly. Codex support does not depend on Claude hook names, hook payloads, or environment variables. Optional host hooks may supply earlier feedback, but no required check is skipped when a hook is absent.

On Windows, a restricted command host may block launching a nested `pwsh.exe`. The unchanged `spec` and `status` gate modes have also been verified in Windows PowerShell 5.1 with UTF-8 file decoding. For these two modes only, invoking the canonical script directly in that host is a supported fallback; record the host and actual exit code. Adapter generation, migration utilities, and other workflow steps still require PowerShell 7 where specified. Request the host's normal permission for a required executable rather than assuming the fallback covers every script.
