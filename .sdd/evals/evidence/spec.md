# Evidence fixture

## 1. Problem Statement

Fixture user needs one session label for testing artifact continuity.

**Solution (one line):** Retain one nonempty label until session ends.

### Scope

**In scope:** Set and read one session label.

**Out of scope:** Persistence, sharing, product changes.

### Actors & Access

Fixture user may set and read own label only.

## 2. Functional Requirements

**FR-1:** Accept nonempty label; preserve previous label on empty input.

## 3. Functional Behaviors

### Behavior 1: Set label

User supplies label. Accepted label becomes current value.

## 4. Constraints

### Business Rules

**RULE-1** Empty input must not overwrite existing value.

## 5. Edge Cases & Error Handling

**Edge case:** Empty input. **User experience:** Rejection; previous label remains.

## 6. Acceptance Criteria

**AC-1:** Given current label, when empty input is submitted, then current label remains unchanged.

## Assumptions & Open Questions

None. Synthetic fixture choices confirmed for regression testing.

---

**Status:** Confirmed
**Created:** 2026-09-06
