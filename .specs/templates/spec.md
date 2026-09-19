# <Feature name> — Specification

Status: Draft

<!-- Template location: .specs/templates/spec.md
Copy to: .specs/features/<feature-name>/spec.md
Read .specs/README.md and .specs/principles.md first.
Replace placeholders and remove inapplicable guidance. Keep this concise.
This document owns WHAT and WHY; plan.md owns HOW and verification evidence.
Feature status lives here only: Draft → Building → Verified → Shipped.
Advance only when the workflow's criteria are satisfied. -->

## 1. Problem & Outcome

**Problem:** <What is difficult or missing, and for whom?>

**Intended outcome:** <What can the user accomplish after this feature?>

<!-- Add a measurable success target only if it helps make a decision. -->

## 2. Scope

**Included:**

- <Behavior delivered in this feature.>

**Excluded:**

- <Relevant boundary that prevents scope expansion.>

## 3. Requirements & Behavior

| ID | Situation / user action | Required behavior |
| --- | --- | --- |
| FR-01 | <Starting state and action> | <Observable response and business rule> |
| FR-02 | <Relevant edge case or failure> | <Expected outcome, feedback, and recovery behavior> |

<!-- Include relevant loading, empty, success, error, permission, or retry
behavior. Add a short user flow only when the table cannot explain it clearly.
Do not prescribe classes, database tables, or technical implementation here.
Use Given/When/Then where useful; a BDD framework is not required. -->

## 4. Constraints & References

**Applicable principles:** <Exact IDs from .specs/principles.md; do not copy their rule text.>

**Other mandatory constraints:** <Relevant compatibility, security, accessibility,
performance, or integration constraints; otherwise none.>

**Design references — UI only:**

| UI / state | Exact Figma frame link | Viewport | Reference version/date, if available |
| --- | --- | --- | --- |
| <Page and state> | <Frame link/node> | <Width × height> | <Version or capture date> |

**Agreed visual deviations — if any:** <Difference, reason, and explicit approval reference.>

**Principle exceptions — only if proposed or approved:**

| Rule ID | Exception and exact scope | Reason | Decision / owner approval reference | Required follow-up |
| --- | --- | --- | --- | --- |
| <ID> | <Specific deviation> | <Reason> | <Proposed or explicitly approved> | <Action or none> |

<!-- Resolve missing UI states/viewports and material conflicts before affected
implementation. A Figma reference or general feature approval does not waive a
principle. Do not treat a proposed exception as approved. Remove unused tables. -->

## 5. Acceptance Criteria

- [ ] AC-01: <Observable success condition; reference FR IDs where helpful.>
- [ ] AC-02: <Relevant edge case, failure, or permission outcome.>
- [ ] AC-03 — UI only: <Named states match the referenced Figma frames at the agreed viewports for layout, spacing, typography, colors, and assets, subject to explicitly agreed deviations.>

<!-- Usually 3–7 criteria, adjusted to the feature. Use a concise completion
checklist rather than repeating every requirement. Check only after supporting
evidence is recorded in plan.md. Functional tests alone do not prove visual fidelity. -->

## 6. Assumptions & Open Questions

**Product assumptions:**

- <Assumption, its impact if wrong, and validation needed; otherwise none.>

| ID | Product question | Blocks what? | Decision / owner |
| --- | --- | --- | --- |
| Q-01 | <Unresolved scope or behavior decision> | <Affected work or non-blocking> | <Pending or resolved decision> |

<!-- Keep technical questions in plan.md. Do not silently turn material
unknowns into requirements. Retain important resolved decisions briefly. -->
