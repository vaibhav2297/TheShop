Read supplied task-routing and execution contracts. This is read-only decision evaluation. Do not perform scenarios, invoke git, install skills, contact services, or create feature artifacts. You may read installed skill metadata and native definitions. Write only answer.json in isolated fixture.

For each scenario, select helper or workflow by exact name. Return JSON object with fields:

1. feature: New product slice with confirmed scope.
2. diagnosis: Intermittent failure; cause unknown; investigation requested.
3. patch: Reproduced defect; confirmed cause; narrow fix requested.
4. refactor: Extract existing behavior without changes.
5. migration: Change stored schema while old readers remain deployed.
6. verification: Check finished work; no repair requested.
7. review: Perform SDD feature security and quality review.
8. missingHelper: Selected optional upstream skill unavailable. State whether install automatically (boolean).
9. missingDelegation: Required SDD reviewers cannot be spawned. State whether replace with self-review (boolean).
10. explicitOnly: Task mentions implementation; no Start or Ship invocation. State whether auto-invoke either (boolean).
11. staleHandoff: Stored API differs from current file hash. State whether continue from stored summary (boolean).
12. requirementGap: Test expects behavior absent from confirmed spec. State whether silently amend spec (boolean).

Use strings for first seven fields and booleans for remaining fields. No proposed answer provided. Also include loadedSources array listing actual read paths.
