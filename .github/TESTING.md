# CI tests and deployment gates

Both Azure deployment workflows call `tests.yml` at the same commit before publishing.
Closing a PR only closes its preview environment; it does not run tests or deploy.
New PR updates cancel obsolete runs. Push runs are serialized per environment to prevent
overlapping deployments.

| Trigger | Layer tests | Browser tests |
| --- | --- | --- |
| PR to `dev`, push to `dev` | Domain, Application, Infrastructure, Web | Critical journeys |
| PR to `master`, push to `master` | All four layers | Full E2E suite |
| Nightly at 20:30 UTC (02:00 IST), manual dispatch | All four layers | Full E2E suite |

Scheduled runs use GitHub's default branch (`master`). Manual dispatch can select another branch.
The critical suite uses existing traits: `Suite=Smoke`, `Feature=auth`,
`Feature=create-product`, and `Feature=001_product-detail`. This covers app startup,
catalogue, sign-in, permissions, product creation, and product details.

## Environment

Layer tests run in separate Ubuntu jobs. Infrastructure tests start disposable PostgreSQL
containers through Testcontainers. E2E starts a separate local Supabase Docker stack on
its runner, applies repository migrations and seed, builds the Blazor app in Debug to match
`AppHostFixture`, and runs headless Chromium. Supabase CLI is pinned to `2.101.0`, matching
the local version used when adding CI. Playwright installs the browser matching the resolved
.NET package. No hosted Supabase credentials are needed.

The E2E setup script resets **local** database data. CI uses a fresh runner; running that
script on a developer machine also resets that machine's local Supabase database.

Every project must produce a TRX report with at least one test and every test passing.
Missing reports, skipped tests, zero executed tests, and runner failures fail the gate.
Local E2E fixtures can still skip when their environment is absent; CI rejects those results.
Test results and failure traces are retained for seven days. Environment keys and cached
browser sessions are not uploaded.

After deployment, an HTTP smoke check verifies the app shell, `/products` fallback,
stylesheet, and Blazor bootstrap script. It does not exercise production writes or replace
browser tests. A failure marks the deployment job failed; it does not automatically roll back.

## Required merge check

Deployment is gated by `needs: tests` in both workflows. Merge enforcement also needs a
GitHub rule after the workflow is available on PRs:

1. Open the existing **Production Ruleset**, which targets `dev` and `master`.
2. Preserve its existing rules. Add **Require status checks to pass**.
3. Select the `Required tests` check emitted by the reusable workflow (the Actions UI groups
   it under the caller's `tests` job). Require the branch to be up to date before merging.

Do not select individual matrix jobs or a scheduled run as a substitute for the aggregate
PR check. The aggregate fails if a dependency fails, is cancelled, or is skipped. Activate
the rule after the first PR run exposes the exact check name; enabling it before workflow
rollout would block unrelated PRs that cannot yet emit that check.

## Local verification

Run each non-E2E project with the same command pattern as CI:

```powershell
dotnet test tests/TheShop.Domain.Tests/TheShop.Domain.Tests.csproj --configuration Release --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/Domain
pwsh -File .github/scripts/assert-test-results.ps1 -Path TestResults/Domain/tests.trx
```

Repeat for Application, Infrastructure, and Web. Infrastructure requires Docker.
For browser tests, use the existing local E2E environment, build the E2E project in Debug,
install Chromium with its generated `playwright.ps1`, and set `E2E_HEADLESS=1`.
The workflow contains the exact critical-suite filter and full-suite commands.
