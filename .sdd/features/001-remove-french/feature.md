# Feature: remove-french

ID: 001
State: Done
Updated: 2026-09-14

## Expected outcome

The application ships only English interface resources. French browser/UI culture no longer selects French text or French Canadian money formatting.

Excluded: Removing the resource/localizer architecture, translating non-user-facing technical culture operations, changing database data, and rewriting inactive `.specs/` history.

## Acceptance

| ID | Concrete example and expected result | Proof | Result / evidence |
|---|---|---|---|
| AC-1 | Given a production build, no `Strings.fr.resx` source or The Shop French satellite resource ships. | build output inspection | Pass; source deleted and Web Debug output contained no French The Shop satellite resource. |
| AC-2 | Given English or French browser/UI culture, interface resource lookup resolves English text only. | unit tests; browser E2E | Pass; resource-fallback tests and French-locale Playwright journey rendered English catalogue heading. |
| AC-3 | Given English, French, or another UI culture, CAD and other-currency amounts use `en-CA` separators and symbol placement. | unit/component tests | Pass; formatter and money-field tests cover English, French, and German UI cultures. |
| AC-4 | Future resource changes require English keys only; active workflow guidance and localization tests contain no French parity requirement. | source inspection; focused tests | Pass; active SDD references require English only; parity tests removed. |
| AC-5 | Existing application builds and affected Web tests pass after French support removal. | build; test suite | Pass; solution build and focused Web suite passed. Full Web suite had one unrelated failure recorded below. |

## Decisions

Expectation confirmation: Confirmed by user instruction `$theshop-build Mode: implement Resume 001` on 2026-09-14. Approved scope: delete French resource, force English Canadian money formatting, retain English resource/localizer infrastructure, update active workflow guidance and tests, preserve `.specs/`.

Design references: Reuse current UI. No layout or visual design change.

Recommended scope: delete French resource, make money formatting English Canadian regardless UI culture, retain `Strings.resx` plus localization services for typed/runtime resource keys, update active SDD guidance and affected tests, preserve `.specs/` unchanged.

## Implementation

- [x] Delete `src/TheShop.Web/Resources/Strings.fr.resx` and remove French-specific project comments.
- [x] Make `CurrencyFormatter` use English Canadian formatting only.
- [x] Replace parity tests with English-resource fallback, formatter, money-field, and browser coverage.
- [x] Remove French parity requirements from active `.sdd/theshop-build` references.
- [x] Run scoped formatting, design checks, affected tests, build, browser proof, output inspection, and Graphify refresh.

## Verification

Revision / dirty files: Branch `refactor/sdd-flow`; feature verified with uncommitted changes. Pre-existing user changes remain in `.sdd/README.md`, `.sdd/theshop-build/SKILL.md`, `.sdd/theshop-build/assets/feature.md`, and `AGENTS.md`. Git used a command-local safe-directory override under sandbox.

Browser E2E: Headed (`E2EEnvironment.Headless = false`); French-locale journey passed. Execution context did not expose window visibility, so this is not claimed as observed visible-desktop proof.

Database test lifecycle: Not applicable; no persistence or migration change.

| Check / exact command | Result / exit code / test counts | Evidence |
|---|---|---|
| Focused baseline inspection | Pass; French support found in one satellite resource, `CurrencyFormatter`, seven resource/sort test files, two money test files, project/application comments, and three active workflow references. No language selector or culture-switch registration found. | Graphify queries and source inspection in current session |
| `dotnet format TheShop.slnx --no-restore --include <owned C#/Razor paths>` | Pass; exit 0. | Current session output |
| `pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-design-rules.ps1 -Path src/TheShop.Web/Common/CurrencyFormatter.cs` | Pass; exit 0, no violations. | Current session output |
| `pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-design-rules.ps1 -Path src/TheShop.Application/Common/Behaviors/ExceptionHandlingBehavior.cs` | Pass; exit 0, no violations. | Current session output |
| `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --filter "Feature=remove-french"` | Pass; exit 0, 12 passed, 0 failed, 0 skipped. | Current session output |
| `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore` | Fail; exit 1, 566 passed, 1 failed, 0 skipped. Failure: `ProductVariantsCardTests.GalleryImagesChanged_WhenAPinnedImageIsRemoved_UnpinsThatVariant`; unrelated to feature-owned code, not changed. | Current session output |
| `dotnet build TheShop.slnx --nologo` | Pass; exit 0, 0 errors, 49 existing dependency/analyzer warnings. | Current session output |
| `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-restore --filter "Feature=remove-french"` | Pass; exit 0, 1 passed, 0 failed, 0 skipped; headed configuration. | Current session output |
| Web output satellite inspection under `src/TheShop.Web/bin/Debug/net10.0` | Pass; exit 0, no French The Shop satellite resource found. | Current session output |
| `graphify update .` | Pass; exit 0; graph refreshed. | Current session output |

Review: Self-review passed: one English resource remains, `CurrencyFormatter` no longer branches on UI culture, active guidance no longer requires parity, and French browser fallback has unit and browser coverage. Independent review not required; no authorization, identity, payment, sensitive access, or destructive database change.

## Resume / delivery

Completed: French resource and translation-parity tests removed; English fallback and fixed English Canadian money formatting implemented and verified.
Remaining: No feature work. Full Web suite has an unrelated failing test; not changed or repaired in this feature.
Blocker / next action: None.
