# Git commit messages

> Rule 36. Based on [Conventional Commits Cheatsheet](https://gist.github.com/qoomon/5dfcdf8eec66a051ecd85625518cfd13).

Use this format:

```text
<type>(<optional scope>)<optional !>: <description>

<optional body>

<optional footer>
```

Separate subject, body, and footer with blank lines. Omit unused optional parts.

## Type

- `feat`: Add, change, or remove API or UI behavior.
- `fix`: Fix an API or UI bug.
- `refactor`: Restructure feature code without changing API or UI behavior.
- `perf`: Improve performance without changing API or UI behavior. Special `refactor` type.
- `style`: Change formatting or code style only; no behavior change.
- `test`: Add or correct tests.
- `docs`: Change documentation only.
- `build`: Change build tools, dependencies, or project version.
- `ops`: Change infrastructure, deployment, CI/CD, backups, monitoring, or recovery.
- `chore`: Maintenance outside types above, such as `.gitignore` changes.

Choose type by primary change. Do not use `refactor` or `perf` for test-only changes; use `test`.

## Subject

- Scope optional. Use project area for context; never use issue ID as scope.
- Description required. Write concise imperative, present tense: `add`, not `added` or `adds`.
- Start description with lowercase letter. Do not end with period.
- Add `!` immediately before `:` for breaking changes: `feat(api)!: remove status endpoint`.

## Body and footer

- Body optional. Explain motivation and previous behavior when extra context helps. Use imperative, present tense.
- Footer optional except for breaking changes. Put issue references here, such as `Closes #123`.
- For every breaking change, add `BREAKING CHANGE: <description>` in footer. For multiline description, put two newlines after `BREAKING CHANGE:`.

## Special commits

- Initial commit: `chore: init`.
- Merge commit: keep Git default `Merge branch '<branch name>'`.
- Revert commit: keep Git default `Revert "<reverted commit subject line>"`.

For release versioning, breaking API or UI changes increment major; `feat` increments minor; other changes increment patch.
