# Caveman communication

Default: **full** for all project prose: chat, progress, specs, plans, skills, commands, agent instructions, handoffs, reviews, reports, documentation, comments, commits, and PR text. This project explicitly extends upstream Caveman to persisted prose. Keep existing artifact schemas and workflow authority.

## Mode and language

- Supported levels only: `lite`, `full`, `ultra`, `wenyan-lite`, `wenyan-full`, `wenyan-ultra`. `off`, `normal mode`, or an explicit normal-English request disables compression for the requested scope. No custom levels.
- Change levels only on explicit user request. A scoped request affects that response or artifact; an unscoped mode change persists until changed or session end. New sessions default to `full` unless user instructions select otherwise.
- Preserve user's dominant language. Never switch to classical Chinese unless a wenyan level is explicitly selected. Keep technical terms and exact strings unchanged.
- Pass active mode, language, and any scoped exception to delegated workers. Do not assume workers inherit parent skills. Workers read this policy through the execution contract; do not recursively invoke a separate Caveman skill.

## Full

Remove filler, pleasantries, repeated facts, and articles where meaning survives. Fragments and short synonyms allowed. Use active voice, one idea per sentence, consistent terms. Target 20 words per sentence; clarity wins over length.

Never add broken grammar to sound compressed. Keep correct verb forms. Use established technical acronyms; never invent prose abbreviations such as `cfg`, `impl`, `req`, or `fn`. Do not substitute causal arrows or Cavekit symbols for prose.

Keep substantive uncertainty, negation, conditions, exceptions, causal order, numbers, units, versions, permissions, and evidence. Never turn unknown into optional, or a warning into approval. Compression changes wording, never authority, requirements, reasoning effort, task scope, or verification.

Preserve executable code, commands, paths, URLs, identifiers, API signatures, SQL, regex, JSON/YAML/TOML schemas, quoted strings, and exact errors. Newly authored explanatory comments use selected style; compression alone never rewrites existing code or literal examples. Preserve mandatory headings, IDs, Given/When/Then structure, report fields, and ledger vocabulary. Avoid decorative output; required tables and markers remain.

Before sending or saving prose, check selected mode. Trim filler and repeated explanations; preserve every requirement and fact. A spec or plan template controls structure, not verbosity. Do not copy its explanatory style over the selected mode.

## Other levels

- `lite`: remove filler; retain articles and complete sentences.
- `ultra`: remove more conjunctions only when causal order stays clear. State each fact once. No invented prose abbreviations or causal arrows.
- Wenyan levels: light, full, or extreme classical Chinese register respectively. Preserve technical literals and facts at every intensity.

## Auto-clarity

Temporarily use normal prose for security warnings, irreversible-action confirmations, multi-step sequences whose fragments risk misreading, technically ambiguous compression, or a user asking for clarification or repeating a question. Resume selected Caveman level afterward; default is `full`. Do not require a separate mode request for these cases.

## Precedence

Host instructions and current user requests prevail. Required progress updates and approval detail remain required. Existing long examples demonstrate facts and structure; their verbosity does not override this policy. Do not load Cavekit's grammar or spec format. Upstream source and project overrides: `.sdd/adapters/caveman/source.json`.
