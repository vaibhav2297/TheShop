# <Feature name> — Implementation Plan

Specification: [spec.md](spec.md)

<!-- Template location: .specs/templates/plan.md
Copy beside the feature spec to .specs/features/<feature-name>/plan.md.
Read the feature spec, .specs/README.md, and .specs/principles.md first.
Inspect relevant code and configuration before filling in technical choices.
This document owns HOW, task progress, and actual verification evidence.
Keep feature status in spec.md; do not create a second status here. -->

## 1. Implementation Approach

**Approach:** <Smallest suitable implementation grounded in the existing code.>

**Affected components and boundaries:**

| Component / actual path | Change and responsibility | Applicable principle IDs |
| --- | --- | --- |
| <Existing component or proposed new path, clearly labeled> | <What changes and why it belongs here> | <IDs> |

**Core decisions:**

- <Decision, short rationale, and meaningful trade-off. Reference a principle or spec constraint where relevant.>

**Technical flow — when helpful:** <Request/event → processing → persistence or external interaction → response; include meaningful failure paths.>

**Stack changes — only if needed:** <New or changed dependency and reason; otherwise omit. Reference existing project documentation instead of restating the stack.>

<!-- Product behavior stays in spec.md. Explain how this plan satisfies it.
Reference approved exceptions in spec.md rather than duplicating approvals.
Do not introduce new infrastructure or abstractions without a concrete need. -->

## 2. Data & Access Design — If Applicable

<!-- Remove this section when the feature has no relevant data/access changes. -->

**Model / schema changes:** <Affected entities, DTOs, tables, columns, relationships,
constraints, and indexes as needed; distinguish domain models from persistence.>

**Access and RLS:** <Who can read/write which records; relevant roles, ownership,
tenant boundaries, and enforcement location. State whether existing policies
suffice or need changes, including corresponding allowed/denied checks.>

**Migration and compatibility:** <Migration/backfill approach, existing-data impact,
deployment order, and compatibility with running versions where relevant.>

**Recovery — when needed:** <Rollback or forward-fix approach and any limitations.>

<!-- Use repository-grounded details. Never weaken access policies merely to
make the feature work. Avoid unnecessary SQL dumps when migration references suffice. -->

## 3. Development Checklist

- [ ] <First complete implementation step, with relevant FR/AC IDs.>
- [ ] <Next implementation or integration step.>
- [ ] <Necessary tests, including mandatory constitution coverage.>
- [ ] <UI only: implement referenced states using project components/tokens/assets.>
- [ ] <UI only: compare rendered screenshots with Figma, fix discrepancies, and rerun affected checks.>
- [ ] <Review final diff and record verification evidence below.>

<!-- Keep tasks small and ordered without enumerating microtasks.
Use selective TDD for important logic and reproducible bugs: expected failing
test → implementation → refactor. Selective TDD does not waive TEST-29 or other
mandatory coverage. Run focused tests while building, not only after styling.
Tasks being checked does not itself satisfy acceptance criteria. -->

## 4. Verification

**Commands and prerequisites:** <Reference the project command guide; record exact
feature-specific commands and working directories below. Note necessary services,
test data, browsers, and environment-variable names without secret values.>

### Behavior and appearance

| Criteria | Check | Command + working directory, or procedure | Actual result / evidence |
| --- | --- | --- | --- |
| <AC/FR IDs> | Unit | <Logic and relevant edge-case checks> | Not run |
| <AC/FR IDs> | Playwright functional E2E | <User journey and command> | Not run |
| <Visual AC IDs> | Design fidelity | <Figma frame versus rendered screenshot at matched viewport/state> | Not run; link evidence and discrepancies |
| <Visual AC IDs> | Playwright visual regression, where useful | <Assertion against an approved browser baseline> | Not run; baseline approval reference |
| <AC IDs> | Manual, if needed | <Steps and expected result> | Not run |
| Project gates | Required lint / type check / build / CI | <Applicable commands or CI run> | Not run |

### Principle compliance

| Applicable rule IDs | Check performed | Outcome and evidence / approved exception reference |
| --- | --- | --- |
| <ID or related group> | <Automated check or focused code/design review> | Unverified |

<!-- Outcomes: compliant, approved exception, violated, or unverified.
Cover applicable rules and reference exception decisions in spec.md. Do not
claim compliance from test success alone or mark unexecuted checks as passing. -->

**UI visual review — if applicable:** <Pending / approved, reviewer decision reference,
screenshots, and accepted deviations recorded in spec.md.>

**Remaining failures / blockers / unverified items:** <Specific gap and next action;
distinguish unrelated existing failures from feature-introduced failures.>

**Verified code state:** <Commit or other relevant run/code-state reference and date,
when available. Do not invent a commit or reuse stale evidence after changes.>

**Release evidence — after shipping:** <Actual release/deployment reference or not shipped.>

<!-- Capture → compare with Figma → fix → recheck before visual approval.
A screenshot alone is not verification. Approve browser baselines only after
the rendering matches the intended design; never update them to hide failures.
Remove inapplicable rows. Every AC needs evidence, but not every AC needs both
unit and E2E tests. Store large test artifacts in existing artifact locations.
Mark spec.md Verified only after required evidence and review are complete.
Mark Shipped only after confirmed release under applicable authorization. -->

## 5. Technical Assumptions & Open Questions

**Technical assumptions:**

- <Assumption, repository evidence, impact if wrong, and validation; otherwise none.>

| ID | Technical question / dependency | Blocks what? | Resolution / owner |
| --- | --- | --- | --- |
| TQ-01 | <Unresolved implementation choice> | <Affected task or non-blocking> | <Pending or resolved decision> |

<!-- Product questions belong in spec.md. Record each question in one place.
Escalate choices that change agreed behavior or conflict with principles.
Continue routine implementation decisions autonomously within approved scope. -->
