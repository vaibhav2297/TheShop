# Solo-developer SDD workflow

Use **Understand → Specify → Build → Verify → Ship** for big features. Process must fit size of feature: short spec + plan per feature, clear behavior, small build steps, proof result work and match design.

## Location and entry point

Put this file at `.specs/README.md`. Use [spec template](templates/spec.md) and [plan template](templates/plan.md) at `.specs/templates/spec.md` and `.specs/templates/plan.md`. Make `spec.md` and `plan.md` together in `.specs/features/<feature-name>/`. Put constitution at `.specs/principles.md`. Keep `.specs` in Git; no secrets or big generated test outputs here.

Add this line to repo's `AGENTS.md`, keep old content too:

> Before feature work or other code changes, read and follow [.specs/README.md](.specs/README.md) and [.specs/principles.md](.specs/principles.md). Use the templates in `.specs/templates/` and keep the active `spec.md` and `plan.md` in `.specs/features/<feature-name>/`.

This just tell AI go read other file; not auto-include. If tool no load `AGENTS.md`, put this ref in whatever instruction file/prompt tool do support. Keep workflow rule here, not copy into many tool files. No need separate agents or skills.

## Project principles

[principles.md](principles.md) is boss law of project. This README say how-to-do; constitution say must-do rules. Read both before spec, build, or picking up old work. Keep old repo instructions, don't trust AI memory as truth for project rules.

- Find rules that hit changed layers, parts, styles, tests, public contracts. Point to rule ID, don't copy rule text into spec.
- Mandatory rules apply even to tiny edit/bugfix, even no feature spec needed. Put proof in existing change notes for these.
- Use existing auto-checks when there; use focused code/design review when judgment needed. Tests passing alone no prove architecture/design okay.
- Old rule-breaks already there is not license for new rule-breaks, and not requirement to fix unrelated code either.
- If principles.md missing, unreadable, or still "proposed" with open adoption questions, say so. Get owner decision on affected adoption questions before build touches that part; keep doing unaffected look/plan work.

### Conflicts, exceptions, and amendments

When request, Figma ref, old code, or other project rule fights a mandatory principle: name rule ID, explain fight, offer compliant option. Get clear owner decision before building conflicting part; keep doing unaffected work meantime. Feature approval, old rule-break, or approved screenshot baseline is NOT silent permission to break rule.

Log each approved exception: rule ID, reason, exact scope, owner approval ref, needed follow-up. Exception good only for that scope. Don't call exception "normal compliance" or quietly change constitution. Permanent change need clear owner okay plus update to real principles file. Project policy never beats higher-priority instruction or access control.

### Document ownership and principle traceability

| Document | Sections | Responsibility |
| --- | --- | --- |
| `spec.md` | Problem & Outcome; Scope; Requirements & Behavior; Constraints & References; Acceptance Criteria; Assumptions & Open Questions | What and why: FR/AC IDs, Figma refs, applicable principle IDs, approved exceptions, product decisions, feature status. |
| `plan.md` | Implementation Approach; Data & Access Design (if applicable); Development Checklist; Verification; Technical Assumptions & Open Questions | How: touched parts, decisions, tasks, data/schema/RLS design, tech questions, real proof of verify and release. |

Write spec first, plan come from spec. Link plan to `spec.md`; point to FR, AC, principle IDs, don't copy rules/reqs. Keep each question in one doc: product Qs in spec, tech Qs in plan. Only one feature status, and only in `spec.md`.

Log principle IDs and scoped exception okays in spec's Constraints & References part. Explain big compliance calls in plan; log rule checks in plan's Verification part. Group like rules if helps; no need extra compliance report. Mark clear: compliant, approved exception, violated, unverified.

When reqs change: update spec first, fix touched plan tasks/checks, kill stale proof. Tech changes that keep same agreed behavior can just go in plan, no need rewrite spec. Check AC boxes in spec only when plan has proof backing it.

For old combined specs, move to new format next time that feature touched: keep reqs+status in `spec.md`, move build tasks+results to sibling `plan.md`, fix refs. Don't backfill whole project or run both template styles as default. Retire old combined templates after moving useful content out.

## Project context and commands

Before first feature: look at existing project instructions, arch docs, package/build config, test config, CI. Follow repo rules found. Link to existing context, don't copy it twice. Keep unrelated changes untouched.

Project got unit tests and Playwright E2E tests, but exact commands not given here. Swap rows below for real repo-grounded commands. Don't guess package manager, script name, working dir, or test-filter syntax. Mark unavailable checks "not configured" or "blocked."

| Check | Exact command and working directory | Prerequisites / verification status |
| --- | --- | --- |
| Run application | To discover | To discover |
| Focused unit tests | To discover | To discover |
| Unit suite required by CI | To discover | To discover |
| Focused Playwright E2E | To discover | Include server, browser, services, and test-data setup |
| Playwright suite required by CI | To discover | To discover |
| Visual regression, if configured | To discover | Include browser/platform and baseline conventions |
| Lint / type check / build, as applicable | To discover | To discover |

Note whether command confirmed from config or actually run+passed. List needed env var names, never secret values. Find release rules from existing docs and CI; don't make new release process.

## 1. Understand

- Read request, principles.md, relevant code, existing tests, project instructions. Find applicable rule IDs and any design/arch fights.
- Get clear on user outcome, boundaries, constraints. Ask only Qs that change behavior, scope, or a big tech choice.
- For UI change, use exact given Figma frame links. Pull design details, ref images, assets through allowed Figma MCP integration. Check access/capability real, don't assume.
- Find target viewport sizes and needed UI states. Ask for missing frames/exports when needed; flag unclear responsive behavior instead of guessing silent.
- If feasibility unsure, do small bounded check before deep planning.

**Exit:** Enough clear to write spec. No separate discovery doc needed.

## 2. Specify

- Draft `.specs/features/<feature-name>/spec.md` from `.specs/templates/spec.md`, then make sibling `plan.md` from `.specs/templates/plan.md`. Still just two parts of Specify step, not new stages.
- Write what should happen before how-to-build tasks. Use Given/When/Then if clearer; no need BDD framework.
- Base plan on real inspected code and applicable principles. Log rule IDs under spec's Constraints & References, big choices in plan's Implementation Approach, planned checks in plan's Verification. Usually few tasks enough.
- For UI work, log exact frame links, ref date/version if have, viewports, states, visual criteria. Write down agreed deviations.
- Match criteria to right unit, functional E2E, design fidelity, visual regression, or manual checks. Every criterion need proof, but not every criterion need every test kind.
- Settle big open product Qs before build. Dev can review spec+plan together in one go unless approval already given. Separate files don't mean separate mandatory approval gates.

**Exit:** Scope and success criteria agreed; build uncertainty small enough.

## 3. Build

- Build smallest whole feature slice, follow applicable principles and approved scoped exceptions. Old conventions must not quietly beat mandatory rules. No unrelated refactor or made-up abstractions.
- Use **selective TDD** for important logic: write focused test, check it fail for right missing-behavior reason, build, then clean up with tests still green. Good for business rules, math, validation, permissions, repeat bugs.
- Selective TDD don't cancel mandatory test coverage in principles.md (incl TEST-29). Don't force strict TDD for every style tweak or plain UI edit. Use behavior AC for user journeys, design compare for look.
- Add/update real tests alongside build; run focused checks while building. Don't wait till visual polish done to test function.
- For UI work, look at design before coding, reuse right components, tokens, fonts, assets. Fix big fights with project conventions instead of quietly swapping different design.
- Keep Development Checklist in plan.md fresh. Handle small tech calls alone; get dev decision for big behavior/scope change, update spec.md, fix touched plan.
- Never soften criteria or tests just to make build pass.

**Exit:** Feature built, ready for full verify.

## 4. Verify

- Check diff for correctness, agreed scope, stray changes, applicable arch/design/test/doc principles. Log rule IDs, checks, real compliance proof in plan.md; mark clear compliant/approved-exception/violated/unverified. Fix rule-breaks or get clear scoped exception before marking Verified.
- Run relevant unit + Playwright checks plus required project gates. Widen regression coverage when shared behavior touched; keep mandatory full-suite CI checks.
- Log proof in plan.md against every AC ID from spec.md, then check off matching AC boxes. Mark clear passed/failed/blocked-not-run. Old tests existing is not proof they ran.
- Fix breaks caused by feature. Report unrelated old failures separate, don't call them passing.
- For UI changes, do visual loop below. Function tests passing alone don't prove visual match.
- After fixes, rerun touched checks. Don't use verify from older code state as proof for new changes.
- Show UI to dev for visual review before ship. If manual review needed but not done, keep that visible, don't claim done.

**Exit:** Agreed criteria and needed verify satisfied. Else, write down exact remaining gap.

### Three different verification targets

| Check | Reference | Purpose |
| --- | --- | --- |
| Functional Playwright E2E | Behavioral acceptance criteria | Verify controls, navigation, and user journeys. |
| Design fidelity review | Exact Figma frame/reference image | Establish that the implemented UI matches the intended design. |
| Playwright visual regression | Approved browser screenshot | Detect unexpected appearance changes afterward. |

Function checks and screenshot checks can share one good test; no need separate files. Use visual regression only for valuable stable UI states. Screenshot alone ain't a compare, and browser-baseline check don't auto-compare against Figma.

### Visual comparison-and-fix loop

1. Pull given Figma ref through allowed MCP tools. Note frame and capture date/version if have.
2. Render running feature with steady sample data, right fonts/assets, matching viewport, UI state, capture area. Use Playwright or existing browser tool to snap it.
3. Compare screenshot vs ref, side-by-side or overlay if helps. Check layout, alignment, spacing, size, type, color, borders, shadows, assets. Cover given responsive frames and needed states.
4. Fix found gaps, snap again, rerun function checks touched by fixes. Keep going till criteria met; stop and report exact blocker if missing refs/assets/tools stop progress.
5. Log proof and remaining diffs in plan.md's Verification part; keep approved design deviations in spec.md. Get dev's visual review through normal handoff. Only clearly-accepted deviations count solved.
6. Once render reviewed against real design and approved, set or update browser screenshot baseline where useful. Never accept baseline just cuz made from current build.

Follow existing browser/OS, viewport/device-scale, font, animation, deterministic-data rules for regression screenshots. Mask only truly dynamic content outside what's being checked. Look into diffs before loosening tolerance; don't hide regressions via baseline updates, masking, retries, or weaker checks. Don't assume raw Figma export must pixel-match browser render.

If Figma, images, or running UI can't be checked, mark visual verify blocked and ask for missing ref/review. Don't claim match. Don't add new integrations/services without proper okay.

## 5. Ship

- Follow project's existing commit, review, CI, merge, release process within user okay. This guide alone don't grant okay to commit, push, merge, or deploy.
- Check applicable mandatory principles satisfied or covered by clear owner-approved exceptions; unresolved rule-breaks block release.
- Required CI must pass before release step it gates. Don't skip checks just cuz local tests passed.
- Keep code, relevant tests, both updated feature docs together in same change.
- Log real release ref in plan.md. Mark spec.md `Shipped` only when release confirmed, not just when build done or committed.

## Status and definition of done

Keep feature status only in spec.md: `Draft`, `Building`, `Verified`, `Shipped`. Blocked work keeps last valid status, describe blocker in relevant part.

Feature is **Verified** when:

- Agreed behavior built and every AC has proof.
- Applicable mandatory principles have proof or clear approved scoped exceptions; no unresolved rule-break or needed check left.
- Needed tests added/updated, and needed verify checks pass.
- Final diff reviewed and stray changes fixed.
- For UI changes, design fidelity checked at agreed viewports/states, gaps fixed or clearly accepted, dev's visual review done.
- Spec shows final agreed behavior and plan shows real results; no blocker stops release.

Release-time CI gates still apply. **Shipped** also need confirmed release.

## Everyday interaction

1. **Understand + Specify:** "Read `.specs/README.md` and `.specs/principles.md`. Look at code, draft spec.md, make plan.md for <feature>, using <Figma frame links, for UI work>. Wait for my review before build."
2. **Build + Verify:** "The spec and plan in `.specs/features/<feature-name>/` are approved. Build the plan under applicable principles, verify behavior and rule compliance, and finish Figma compare-and-fix loop where needed. Log results and remaining review items in plan.md; update AC and status in spec.md."
3. **Ship:** Give okay for the right commit, merge, or release action per existing process.

These three handy interactions cover five activities, not forced separate sessions. Keep Build and Verify together by default, with different checks. Don't make dev manually fix gaps AI can find and fix itself. Reuse existing okay instead of asking again for same thing.

When picking up old work, read this guide, principles.md, active spec.md and plan.md, current code, relevant test results. Recheck affected compliance if code or principles changed since last verify. Don't lean on chat history that ain't there. A skill optional later if repeat prompting gets heavy; it should point to this guide, not copy it.

## Keep the effort proportional

| Change | Minimum process |
| --- | --- |
| Tiny copy, styling, or obvious edit | Scoped instruction, edit, and relevant verification; no separate spec. Compare design where relevant. |
| Bug fix | Reproduce, define expected behavior, fix, and verify; add a regression test where practical. Existing issue notes may suffice. |
| Meaningful feature | A concise spec.md and plan.md using the two templates. |
| High-risk or broad change | The same two documents, adding necessary failure, compatibility, migration, access/RLS, or recovery detail. |

Don't backfill whole project, don't add extra docs by default, don't add tests that just mirror build details. Keep only detail needed to build and verify next deliverable reliably.