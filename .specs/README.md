# Solo-developer SDD workflow

Use **Understand → Specify → Build → Verify → Ship** for meaningful features. Keep the process proportional: one feature document, clear behavior, small implementation steps, and evidence that the result works and matches its design.

## Location and entry point

Place this file at `.specs/README.md` and the [feature template](_template.md) at `.specs/_template.md`. Create each feature at `.specs/features/<feature-name>/spec.md`. Place the constitution at `.specs/principles.md`. Keep `.specs` tracked in Git; do not put secrets or large generated test outputs here.

Add this instruction to your repository's `AGENTS.md`, preserving its existing content:

> Before feature work or other code changes, read and follow [.specs/README.md](.specs/README.md) and [.specs/principles.md](.specs/principles.md). Use `.specs/_template.md` and keep the active feature document at `.specs/features/<feature-name>/spec.md`.

This reference instructs the AI to read another file; it is not an automatic include. If your tool does not load `AGENTS.md`, reference this file in its supported instruction file or your prompt. Keep workflow rules here instead of copying them into multiple tool files. Separate agents or skills are not required.

## Project principles

[principles.md](principles.md) is the authoritative project constitution. This README defines the process; the constitution defines mandatory constraints. Read both before specification, implementation, or resuming work. Preserve existing repository instructions and do not rely on AI memory as the source of project rules.

- Identify rules applicable to the changed layers, components, styles, tests, and public contracts. Reference their stable IDs without copying rule text into the spec.
- Apply mandatory rules to tiny edits and bug fixes too, even when no feature spec is needed. Record evidence in existing change notes for those cases.
- Use existing automated checks where available and focused code/design review where judgment is required. Passing tests alone does not establish architectural or design compliance.
- Existing violations are not permission for new violations or a requirement to refactor unrelated code.
- If principles.md is missing, unreadable, or still marked proposed with unresolved adoption decisions, report the gap. Resolve applicable adoption questions with the owner before affected implementation; continue unaffected inspection and planning.

### Conflicts, exceptions, and amendments

When a request, Figma reference, existing code, or another project rule conflicts with a mandatory principle, cite the rule ID, explain the conflict, and propose a compliant option. Obtain an explicit owner decision before implementing the conflicting portion; continue unaffected work where practical. Feature approval, an existing violation, or a screenshot baseline approval is not implicit permission to violate a rule.

Record each approved exception with its rule ID, reason, exact scope, owner approval reference, and any required follow-up. An exception applies only to that scope. Do not label an exception as ordinary compliance or silently alter the constitution. Permanent amendments require explicit owner approval and an update to the canonical principles file. Project policy never overrides higher-priority instructions or access controls.

### Use the existing four-section spec

| Section | Required principles information |
| --- | --- |
| Goal & Scope | Applicable principle IDs and any proposed or approved exceptions. |
| Acceptance Criteria | Feature-specific observable constraints where relevant; do not duplicate the constitution. |
| Implementation Plan | Consequential architecture/design choices needed to satisfy the rules. |
| Verification | Rule IDs or related groups, check performed, actual result, and evidence or approved exception reference. |

If the current template lacks these fields, add them within the existing sections when drafting the feature. No fifth section or separate compliance report is required. A compact verification row can use: `ARCH-01, ARCH-03 | Boundary review | Inspect references and changed imports | Passed: <evidence>`. Mark blocked or not-run checks honestly; document an exclusion reason only when applicability is ambiguous.

## Project context and commands

Before the first feature, inspect existing project instructions, architecture documentation, package/build configuration, test configuration, and CI. Follow applicable repository instructions. Link to existing context rather than duplicating it. Preserve unrelated changes.

The project has unit tests and Playwright E2E tests, but its exact commands have not been supplied here. Replace the entries below with repository-grounded commands. Do not guess a package manager, script name, working directory, or test-filter syntax. Mark unavailable checks as not configured or blocked.

| Check | Exact command and working directory | Prerequisites / verification status |
| --- | --- | --- |
| Run application | To discover | To discover |
| Focused unit tests | To discover | To discover |
| Unit suite required by CI | To discover | To discover |
| Focused Playwright E2E | To discover | Include server, browser, services, and test-data setup |
| Playwright suite required by CI | To discover | To discover |
| Visual regression, if configured | To discover | Include browser/platform and baseline conventions |
| Lint / type check / build, as applicable | To discover | To discover |

Record whether a command was confirmed from configuration or actually executed successfully. List required environment variable names, never secret values. Discover release rules from existing documentation and CI; do not create a new release process.

## 1. Understand

- Read the request, principles.md, relevant code, existing tests, and project instructions. Identify applicable rule IDs and any design or architecture conflicts.
- Clarify the user outcome, boundaries, and constraints. Ask only questions that affect behavior, scope, or a consequential technical choice.
- For UI changes, use the exact supplied Figma frame links. Fetch available design details, reference images, and assets through the authorized Figma MCP integration. Confirm access and capabilities rather than assuming them.
- Identify target viewport sizes and required UI states. Request missing frames or exports when needed; flag unspecified responsive behavior rather than silently inventing it.
- If feasibility is uncertain, use a bounded investigation before detailed planning.

**Exit:** Enough clarity to specify the feature. No separate discovery document is required.

## 2. Specify

- Create or update `.specs/features/<feature-name>/spec.md` using the template. Keep its four sections: Goal & Scope, Acceptance Criteria, Implementation Plan, Verification.
- Define observable behavior before implementation tasks. Use Given/When/Then when it improves clarity; a BDD framework is not required.
- Ground the plan in inspected code and applicable principles. Record relevant rule IDs under Goal & Scope, consequential compliance choices in the plan, and planned compliance checks in Verification. Usually a few tasks are sufficient.
- For UI work, record exact frame links, reference date/version when available, viewports, states, and visual criteria. Document agreed deviations.
- Map criteria to appropriate unit, functional E2E, design fidelity, visual regression, or manual checks. Every criterion needs evidence, but not every criterion needs every test type.
- Resolve material unanswered product questions before implementation. The developer reviews the spec unless approval or authorization to proceed has already been supplied.

**Exit:** Scope and success criteria are agreed; implementation uncertainty is manageable.

## 3. Build

- Implement the smallest complete feature slice following applicable principles and approved scoped exceptions. Existing conventions must not silently override mandatory rules. Avoid unrelated refactoring or speculative abstractions.
- Use **selective TDD** for important logic: write a focused test, confirm it fails for the expected missing behavior, implement, then refactor with tests passing. This is especially useful for business rules, calculations, validation, permissions, and reproducible bugs.
- Selective TDD does not waive mandatory test coverage in principles.md (including TEST-29). Do not require strict TDD for every styling or straightforward UI edit. Use behavioral acceptance criteria for user journeys and design comparison for appearance.
- Add or update meaningful tests alongside implementation and run focused checks during development. Do not delay functional testing until visual polish is complete.
- For UI work, inspect the design before coding and reuse suitable components, tokens, fonts, and assets. Resolve material conflicts with project conventions rather than silently substituting a different design.
- Keep the implementation checklist current. Handle routine technical choices autonomously; resolve material behavior or scope changes with the developer and update the spec.
- Never weaken criteria or tests merely to make the implementation pass.

**Exit:** The feature is implemented and ready for complete verification.

## 4. Verify

- Review the diff for correctness, agreed scope, unintended changes, and applicable architecture/design/testing/documentation principles. Record rule IDs, checks, and actual compliance evidence in the feature spec; distinguish compliant, approved exception, violated, and unverified outcomes. Resolve violations or obtain explicit scoped exceptions before marking Verified.
- Run relevant unit and Playwright checks plus required project gates. Broaden regression coverage when shared behavior is affected; preserve mandatory full-suite CI checks.
- Record evidence against every acceptance criterion. Distinguish passed, failed, and blocked/not-run results. Existing tests are not proof that they executed.
- Fix failures introduced by the feature. Report unrelated existing failures separately without presenting them as passing.
- For UI changes, follow the visual loop below. Passing functional tests alone does not establish visual fidelity.
- After fixes, rerun affected checks. Do not treat verification from an earlier code state as proof for new changes.
- Present the UI for the developer's visual review before shipping. If manual review is required but pending, keep that visible; do not claim it is complete.

**Exit:** Agreed criteria and required verification are satisfied. Otherwise, record the specific remaining gap.

### Three different verification targets

| Check | Reference | Purpose |
| --- | --- | --- |
| Functional Playwright E2E | Behavioral acceptance criteria | Verify controls, navigation, and user journeys. |
| Design fidelity review | Exact Figma frame/reference image | Establish that the implemented UI matches the intended design. |
| Playwright visual regression | Approved browser screenshot | Detect unexpected appearance changes afterward. |

Functional checks and screenshot assertions can share an appropriate test; separate files are not mandatory. Use visual regression selectively for valuable, stable UI states. A screenshot capture alone is not a comparison, and a browser-baseline assertion does not automatically compare against Figma.

### Visual comparison-and-fix loop

1. Fetch the supplied Figma reference through the available authorized MCP capabilities. Record the frame and capture date/version when available.
2. Render the running feature with stable representative data, intended fonts/assets, and the matching viewport, UI state, and capture region. Use Playwright or existing browser tooling to capture it.
3. Inspect the screenshot against the reference, side by side or with an overlay when useful. Check layout, alignment, spacing, dimensions, typography, colors, borders, shadows, and assets. Include supplied responsive frames and required states.
4. Fix identified discrepancies, capture again, and rerun functional checks affected by the corrections. Continue until criteria are met; stop and report specific blockers if missing references, assets, or tooling prevent progress.
5. Record evidence and remaining differences in the feature's Verification section. Obtain the developer's visual review through the normal handoff. Only explicitly accepted deviations count as resolved.
6. Once the rendering is reviewed against the intended design and approved, establish or update a browser screenshot baseline where useful. Never accept a baseline simply because it was generated from the current implementation.

Follow existing browser/OS, viewport/device-scale, font, animation, and deterministic-data conventions for regression screenshots. Mask only genuinely dynamic content outside what is being verified. Investigate differences before changing tolerances; do not hide regressions through baseline updates, masking, retries, or weaker assertions. Do not assume a raw Figma export must be pixel-identical to browser rendering.

If Figma, images, or the running UI cannot be inspected, record visual verification as blocked and request the missing reference or review. Do not claim a match. Do not install new integrations or services without applicable authorization.

## 5. Ship

- Follow the project's existing commit, review, CI, merge, and release process within user authorization. This guide itself does not authorize commits, pushes, merges, or deployment.
- Confirm applicable mandatory principles are satisfied or covered by explicit owner-approved exceptions; unresolved rule violations block release.
- Required CI must pass before the release step it gates. Do not bypass checks because local tests passed.
- Keep code, relevant tests, and the updated feature spec together in the change.
- Record the actual release reference. Mark `Shipped` only when release is confirmed, not merely when implementation is complete or committed.

## Status and definition of done

Use `Draft`, `Building`, `Verified`, and `Shipped`. Keep blocked work at its last valid status and describe the blocker in the relevant section.

A feature is **Verified** when:

- Agreed behavior is implemented and every acceptance criterion has evidence.
- Applicable mandatory principles have verification evidence or explicit approved scoped exceptions; no unresolved violation or required compliance check remains.
- Necessary tests are added or updated, and required verification checks pass.
- The final diff has been reviewed and unintended changes resolved.
- For UI changes, design fidelity is checked at agreed viewports/states, discrepancies are fixed or explicitly accepted, and the developer's visual review is complete.
- The spec reflects the final agreed behavior and actual results; no unresolved blocker prevents release.

Release-time CI gates still apply. **Shipped** additionally requires a confirmed release.

## Everyday interaction

1. **Understand + Specify:** “Read `.specs/README.md` and `.specs/principles.md`. Inspect the code and draft a spec for <feature>, using <Figma frame links, for UI work>. Wait for my review before implementation.”
2. **Build + Verify:** “The spec at `.specs/features/<feature-name>/spec.md` is approved. Implement it under the applicable principles, verify behavior and rule compliance, and complete the Figma comparison-and-fix loop where applicable. Record results and remaining review items.”
3. **Ship:** Authorize the appropriate commit, merge, or release action according to the existing process.

These are three convenient interactions covering five activities, not mandatory separate sessions. Keep Build and Verify together by default, with distinct checks. Do not ask the developer to manually repair discrepancies the AI can identify and fix. Reuse existing authorization instead of requesting redundant approvals.

When resuming, read this guide, principles.md, the active feature spec, current code, and relevant test results. Reassess affected compliance if code or principles changed since the recorded verification. Do not rely on unavailable chat history. A skill is optional later if repeated prompting becomes a real burden; it should reference this guide rather than duplicate it.

## Keep the effort proportional

| Change | Minimum process |
| --- | --- |
| Tiny copy, styling, or obvious edit | Scoped instruction, edit, and relevant verification; no separate spec. Compare design where relevant. |
| Bug fix | Reproduce, define expected behavior, fix, and verify; add a regression test where practical. Existing issue notes may suffice. |
| Meaningful feature | One four-section spec. |
| High-risk or broad change | Same spec, adding necessary failure, compatibility, migration, or recovery detail. |

Do not backfill the whole project, introduce extra documents by default, or add tests that merely mirror implementation details. Keep only the detail needed to implement and verify the next deliverable reliably.
