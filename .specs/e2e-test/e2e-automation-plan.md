# E2E Test Automation — Implementation Plan (AI Handoff Document)

**Goal:** Integrate Playwright-based end-to-end test automation into The Shop, running against
a **local Supabase stack** (Docker), wired into the existing SDD pipeline
(`/theshop.verify` Tier 1) and CI, without breaking any existing test project or workflow.

**Audience:** An AI coding agent executing this plan autonomously. Follow it phase by phase.

> **Decision history (2026-08-01):** the E2E backend went local → hosted → **back to local**,
> deliberately. A hosted test project (`TheShop-Test`) was provisioned and evaluated, but it
> makes the passwordless-OTP flow expensive to automate (real emails need a captive inbox —
> free tiers are capped well below nightly-CI volume; Brevo has no capture sandbox) and it
> sacrifices determinism (no `db reset`, shared mutable state, dashboard-only hook config,
> free-tier auto-pause). The local stack solves all of that natively: bundled mail catcher
> (Mailpit) with an HTTP API for OTP capture at zero cost, `supabase db reset` for pristine
> per-session state, and the access-token hook enabled in versioned `config.toml`. Full
> rationale in Appendix B.
>
> **TheShop-Test is not deleted — it is repurposed as the staging environment.** All 18 repo
> migrations (0001–0018) were applied to it on 2026-08-01 and verified. It plays no role in
> this plan's automated runs.

---

## Execution contract (read first, non-negotiable)

1. **Execute phases strictly in order** (0 → 5). Each phase ends with a **GATE** — a concrete
   verification command with an expected result. Do not start the next phase until the gate
   passes. If a gate fails twice after your best fix, **stop and report** the gate output.
2. **Steps marked `DISCOVERY`** require you to read a repo file or command output and adapt the
   code that follows. Never skip a DISCOVERY step and never guess its answer.
3. **Steps marked `CHECKPOINT`** flag a known environment variance (e.g., CLI version
   differences). Verify which variant applies before using the corresponding code.
4. **Do not modify** any existing test project, any file under `src/TheShop.Domain`,
   `src/TheShop.Application`, or `src/TheShop.Infrastructure`, or the two Azure SWA workflows.
   Allowed production-adjacent edits: the `data-testid` additions in Phase 3 (scoped to
   `src/TheShop.Web/**/*.razor`) and the `supabase/config.toml` + `supabase/seed.sql` changes
   in Phase 2 (local-stack config, not app code).
5. **Never touch the remote projects from E2E work.** `TheShop-Dev` (`uwfltzgecsepvlaplktv`)
   is the shared dev DB; `TheShop-Test` (`wuoyiwyvhsfrzsjpxpsy`) is staging. E2E runs use only
   the local stack. In particular, never run `supabase db reset --linked`.
6. **Obey the repo's CLAUDE.md rules**, in particular: no AI/agent attribution in any commit
   message, PR title, or PR body; no hardcoded user-facing strings; XML doc comments
   (`/// <summary>`) on public types/members you create.
7. After each phase that changes code, run `graphify update .` (skip silently if unavailable).
8. Commit at the end of each phase (small, phase-scoped commits).

---

## Phase 0 — Preconditions and ground truth

### 0.1 Verified repo facts (do not re-derive; trust these, spot-check if something fails)

| Fact | Value | Evidence |
|---|---|---|
| App type | **Standalone** Blazor WebAssembly (no server host) | `src/TheShop.Web/TheShop.Web.csproj` → `Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"` |
| Dev URL | `http://localhost:5218` via `dotnet run --project src/TheShop.Web --launch-profile http` | `src/TheShop.Web/Properties/launchSettings.json` |
| Test stack | xUnit **v3** (`xunit.v3` 3.2.2), `Microsoft.NET.Test.Sdk` 18.7.0, FluentAssertions 8.10.0, NSubstitute, bUnit 2.7.2 | `tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj` |
| Solution file | `TheShop.slnx` (XML solution format; `dotnet sln` supports it) | repo root |
| Config source | Fetched over HTTP at WASM boot from `wwwroot/appsettings.json` + `wwwroot/appsettings.{Environment}.json`; keys `Supabase:Url`, `Supabase:PublishableKey` | `src/TheShop.Web/Program.cs`, `src/TheShop.Web/wwwroot/appsettings*.json` |
| Auth | **Passwordless email OTP**: `/Pages/Auth/SignIn.razor` (email) → `SignInVerify.razor` (custom `OtpInput` component, `Length="@OtpLength"`). Sign-in checks `customer_exists(email)` first, so a persona must have a `customers` row | those files, `supabase/migrations/0001` |
| Session storage | localStorage key **`shop.auth.session`** (serialized `Supabase.Gotrue.Session`) | `src/TheShop.Infrastructure/Auth/LocalStorageSessionPersistence.cs` |
| Local Supabase | Already initialized: `supabase/config.toml` + migrations 0001–0018 in `supabase/migrations/` | `supabase/` |
| RBAC claims | A **custom access-token hook** (`0010_custom_access_token_hook.sql`) bakes `app_roles`/`perms`/`perm_v` into the JWT. Locally it is enabled via `config.toml` (`[auth.hook.custom_access_token]`) — the migration's header documents the exact snippet. **Until enabled, tokens carry no role claims and every RBAC journey fails** | migration 0010 |
| Remote projects | `TheShop-Dev` = shared dev DB (off-limits); `TheShop-Test` = staging, schema current through 0018 as of 2026-08-01; `TheShop` = prod | Supabase MCP, 2026-08-01 |
| Route constants | `public static class Routes` (e.g. `Routes.Auth.SignIn`) | `src/TheShop.Web/Common/Routes.cs` |
| Localized strings | Generated public `Strings` class from `Resources/Strings.resx` (`PublicClass=true`, explicitly so other test assemblies can use it) | `src/TheShop.Web/TheShop.Web.csproj` comments |
| CI | Only two Azure SWA deploy workflows; **no test workflow exists** | `.github/workflows/` |
| SDD hook | `/theshop.verify` (`.claude/commands/theshop.verify.md`) defines a currently-unreachable "Tier 1 — automated" path; tests are filtered by `[Trait("Feature","{name}")]` across the pipeline | that file |

### 0.2 Architectural decisions (already made — do not revisit)

- **Tool:** Playwright for .NET (`Microsoft.Playwright`), Chromium only for now.
- **Test framework:** xUnit v3 with hand-rolled fixtures. Do **not** add
  `Microsoft.Playwright.Xunit` — it targets xUnit v2 and conflicts with `xunit.v3`.
- **Backend for E2E runs:** the **local Supabase stack** (`supabase start`), because
  (a) the repo is already initialized for it, (b) `supabase db reset` gives deterministic
  per-session state, (c) the bundled mail catcher (Mailpit) exposes OTP emails over HTTP —
  free and unlimited, which is the only sustainable way to automate the passwordless login
  (hosted alternatives were evaluated and rejected — Appendix B), (d) the access-token hook
  is enabled in versioned `config.toml` instead of a forgettable dashboard checkbox, and
  (e) nothing can ever touch the shared remote databases.
- **App config for E2E:** Playwright **route interception** of `**/appsettings*.json`,
  fulfilled with local-stack values. Zero production changes; no reliance on
  `WasmApplicationEnvironmentName` build plumbing.
- **Authentication in tests:** drive the real OTP UI **once per persona per run**, read the
  code from Mailpit, capture Playwright `storageState` (which snapshots localStorage exactly
  as the app wrote it — no guessing at the `Session` JSON shape), and reuse that state for
  all other tests.
- **Black-box with one exception:** the E2E project references `TheShop.Web` **only** for the
  generated `Strings` class and `Routes` constants (locale-safe selectors, no duplicated
  route literals). It must never instantiate app services, components, or handlers.
- **Graceful degradation:** when the local stack is not running (no `.e2e-env` file), every
  E2E test **skips** (xUnit v3 `Assert.Skip`) instead of failing, so a plain solution-level
  `dotnet test` stays green for anyone who hasn't started Supabase.

### 0.3 Tooling preflight

Run and confirm each:

```powershell
dotnet --version          # must be 10.x
pwsh --version            # PowerShell 7+
docker --version          # required by supabase start
supabase --version        # Supabase CLI; install via `scoop install supabase` or winget if missing
```

If Docker or the Supabase CLI is missing, stop and report — Phase 2 cannot run without them.
(Phase 1 can proceed regardless.)

**GATE 0:** all four commands print versions; `dotnet build TheShop.slnx --nologo` succeeds on
the current checkout.

---

## Phase 1 — Scaffold `TheShop.E2E.Tests` + boot smoke test

Outcome: a fifth test project that launches the real app and proves it boots in a real browser.

### 1.1 Create the project file

Create `tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- Constants-only reference: Routes + generated Strings class. Never call app services. -->
    <ProjectReference Include="..\..\src\TheShop.Web\TheShop.Web.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" PrivateAssets="all" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.Playwright" Version="1.*" />
  </ItemGroup>

</Project>
```

Add it to the solution:

```powershell
dotnet sln TheShop.slnx add tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj
```

### 1.2 Assembly configuration

Create `tests/TheShop.E2E.Tests/AssemblyInfo.cs`:

```csharp
// E2E journeys share one app process and one browser; run them serially for determinism.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

### 1.3 Environment gate helper

Create `tests/TheShop.E2E.Tests/Fixtures/E2EEnvironment.cs`. This is the single source of
truth for "is the E2E environment available", and the reader for the env file Phase 2's
script writes.

```csharp
namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Resolves the local E2E environment: repo root, app URL, and the Supabase keys exported by
/// <c>tools/start-e2e-env.ps1</c>. When the env file is absent, tests skip rather than fail.
/// </summary>
public static class E2EEnvironment
{
    public const string AppBaseUrl = "http://localhost:5218";

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string EnvFilePath => Path.Combine(RepoRoot, "tests", "TheShop.E2E.Tests", ".e2e-env");

    public static bool IsAvailable => File.Exists(EnvFilePath);

    /// <summary>Reads a KEY="value" line from .e2e-env; throws with a clear message if missing.</summary>
    public static string Get(string key)
    {
        var line = File.ReadAllLines(EnvFilePath)
            .FirstOrDefault(l => l.StartsWith(key + "=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Key '{key}' not found in {EnvFilePath}. Re-run tools/start-e2e-env.ps1.");
        return line[(key.Length + 1)..].Trim('"');
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TheShop.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate TheShop.slnx above the test bin directory.");
    }
}
```

### 1.4 App host fixture

Create `tests/TheShop.E2E.Tests/Fixtures/AppHostFixture.cs`. It owns the `dotnet run` process —
same launch and teardown contract `/theshop.verify` uses today (poll until ready, never orphan
the process).

```csharp
using System.Diagnostics;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Launches the Blazor WASM dev server (<c>dotnet run --launch-profile http</c>) once per test
/// collection, polls <see cref="E2EEnvironment.AppBaseUrl"/> until it serves 200, and kills the
/// entire process tree on disposal so no orphaned process holds port 5218.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private Process? _app;

    public async ValueTask InitializeAsync()
    {
        if (!E2EEnvironment.IsAvailable)
            return; // Tests will Assert.Skip; don't burn 30s launching an app nobody will use.

        _app = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project src/TheShop.Web --launch-profile http",
            WorkingDirectory = E2EEnvironment.RepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Failed to start the app process.");

        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (_app.HasExited)
                throw new InvalidOperationException(
                    $"App process exited during startup:\n{await _app.StandardError.ReadToEndAsync()}");
            try
            {
                var res = await http.GetAsync(E2EEnvironment.AppBaseUrl);
                if (res.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* not listening yet */ }
            await Task.Delay(2000);
        }
        throw new TimeoutException($"App did not become ready at {E2EEnvironment.AppBaseUrl} within 120s.");
    }

    public ValueTask DisposeAsync()
    {
        if (_app is { HasExited: false })
            _app.Kill(entireProcessTree: true);
        _app?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

> If `InitializeAsync` times out, check port 5218 for a developer-launched instance and
> report — do not add auto-kill logic for processes this fixture didn't start.

### 1.5 Playwright fixture

Create `tests/TheShop.E2E.Tests/Fixtures/PlaywrightFixture.cs`:

```csharp
using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// One Playwright driver and one Chromium instance per test collection. Contexts (and their
/// route interception + storage state) are created per test via <see cref="ShopBrowser"/>.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        if (!E2EEnvironment.IsAvailable) return;

        _playwright = await Playwright.CreateAsync();
        _playwright.Selectors.SetTestIdAttribute("data-testid");
        Browser = await _playwright.Chromium.LaunchAsync(new()
        {
            // Set E2E_HEADED=1 locally to watch the run.
            Headless = Environment.GetEnvironmentVariable("E2E_HEADED") is null,
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
```

### 1.6 Collection definition + test base class

Create `tests/TheShop.E2E.Tests/Fixtures/E2ECollection.cs`:

```csharp
namespace TheShop.E2E.Tests.Fixtures;

[CollectionDefinition(Name)]
public sealed class E2ECollection
    : ICollectionFixture<AppHostFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "E2E";
}
```

Create `tests/TheShop.E2E.Tests/Fixtures/E2ETestBase.cs`:

```csharp
using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Base class for all E2E journeys: skips when the local environment is down, creates a fresh
/// browser context per test with app-config interception installed, and saves a Playwright
/// trace + screenshot on failure under <c>bin/.../playwright-traces/</c>.
/// </summary>
[Collection(E2ECollection.Name)]
public abstract class E2ETestBase(PlaywrightFixture playwright) : IAsyncLifetime
{
    /// <summary>Exposed for subclasses (e.g. the authenticated base) that need the browser.</summary>
    protected PlaywrightFixture Playwright { get; } = playwright;

    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    /// <summary>Storage-state file to preload (set by authenticated journeys); null = anonymous.</summary>
    protected virtual string? StorageStatePath => null;

    public virtual async ValueTask InitializeAsync()
    {
        Assert.SkipUnless(E2EEnvironment.IsAvailable,
            "E2E environment not running — execute tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1 first.");

        Context = await ShopBrowser.NewContextAsync(Playwright.Browser, StorageStatePath);
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        Page = await Context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Context is null) return;
        var failed = TestContext.Current.TestState?.Result is TestResult.Failed;
        var tracePath = failed
            ? Path.Combine(AppContext.BaseDirectory, "playwright-traces",
                $"{TestContext.Current.Test?.TestDisplayName ?? "unknown"}.zip")
            : null;
        await Context.Tracing.StopAsync(new() { Path = tracePath });
        await Context.DisposeAsync();
    }
}
```

> **CHECKPOINT (xUnit v3 API):** `Assert.SkipUnless`, `TestContext.Current.TestState`, and
> `TestResult.Failed` are xUnit v3 APIs; exact member names have shifted between 3.x minors.
> If compilation fails on any of them, check the installed `xunit.v3` package's API and
> adapt — the *intent* is: skip when env missing; detect failure in cleanup; save trace only
> on failure. If failure detection proves brittle, fall back to always writing the trace file.

### 1.7 Context factory with config interception

Create `tests/TheShop.E2E.Tests/Fixtures/ShopBrowser.cs`:

```csharp
using System.Text.Json;
using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Creates browser contexts pre-wired for The Shop: every request for
/// <c>appsettings*.json</c> is fulfilled with local-Supabase values, so the app under test
/// talks to the E2E stack without any production config change — and can never accidentally
/// hit the shared TheShop-Dev database.
/// </summary>
public static class ShopBrowser
{
    public static async Task<IBrowserContext> NewContextAsync(IBrowser browser, string? storageStatePath = null)
    {
        var context = await browser.NewContextAsync(new()
        {
            BaseURL = E2EEnvironment.AppBaseUrl,
            StorageStatePath = storageStatePath is not null && File.Exists(storageStatePath)
                ? storageStatePath
                : null,
        });

        var e2eConfig = JsonSerializer.Serialize(new
        {
            Supabase = new
            {
                Url = E2EEnvironment.Get("API_URL"),
                PublishableKey = E2EEnvironment.Get("ANON_KEY"),
            },
        });

        await context.RouteAsync("**/appsettings*.json", route =>
            route.FulfillAsync(new() { ContentType = "application/json", Body = e2eConfig }));

        return context;
    }
}
```

> The local stack issues legacy anon-key JWTs; the app passes `Supabase:PublishableKey`
> straight through as the `apikey` header, which the local stack accepts. No app change
> needed.

### 1.8 Boot smoke test

Create `tests/TheShop.E2E.Tests/Journeys/AppBootTests.cs`:

```csharp
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Suite", "Smoke")]
public sealed class AppBootTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task App_boots_and_renders_the_layout()
    {
        await Page.GotoAsync("/");
        // The host shell serves instantly; .mud-layout appears only after the WASM runtime
        // boots and MainLayout renders — this is the real "app is alive" signal.
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
    }
}
```

### 1.9 Build, install browsers, first run

```powershell
dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj
pwsh tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E"
```

At this point `.e2e-env` does not exist yet, so the expected result is **1 test SKIPPED** with
the "environment not running" message — that is the correct Phase 1 outcome and proves the
graceful-degradation gate works.

### 1.10 Gitignore

Append to the repo `.gitignore`:

```
# E2E automation — per-run artifacts, never committed
tests/TheShop.E2E.Tests/.e2e-env
tests/TheShop.E2E.Tests/.auth-states/
**/playwright-traces/
```

**GATE 1:**
1. `dotnet build TheShop.slnx --nologo` → green (whole solution still compiles).
2. `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E"` → 1 skipped, 0 failed.
3. `dotnet test tests/TheShop.Web.Tests` → unchanged from before this phase (no regressions).

---

## Phase 2 — Local E2E environment: stack config, seeding, OTP login, storage state

Outcome: a one-command environment script; the access-token hook enabled locally; three
authenticated personas whose sessions are captured as reusable storage states; the smoke
tests pass for real (not skipped).

### 2.1 Local stack configuration (`supabase/config.toml`)

`DISCOVERY` — read `supabase/config.toml` fully, then make these changes (preserving
everything else):

1. **Enable the custom access-token hook** — migration 0010's header documents the exact
   snippet:

   ```toml
   [auth.hook.custom_access_token]
   enabled = true
   uri = "pg-functions://postgres/public/custom_access_token_hook"
   ```

   Without this, local JWTs carry no `app_roles`/`perms` claims and every RBAC journey
   fails. This is versioned config — commit it.

2. **Site URL:** set `site_url = "http://localhost:5218"` under `[auth]` (and add the same to
   `additional_redirect_urls` if the key exists), matching the app's dev URL.

3. Leave SMTP alone — the local stack routes all auth email to its bundled mail catcher
   (Mailpit) automatically; nothing to configure and nothing is ever delivered.

### 2.2 Environment scripts

Create `tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1`:

```powershell
#Requires -Version 7
# Starts the local Supabase stack, applies all migrations + seed, and exports the keys the
# E2E fixtures read. Idempotent — safe to re-run.
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path

Push-Location $repoRoot
try {
    supabase start                       # no-op if already running
    supabase db reset                    # re-applies supabase/migrations/* + supabase/seed.sql
    supabase status -o env | Out-File -Encoding utf8 "$repoRoot/tests/TheShop.E2E.Tests/.e2e-env"
    Write-Host "E2E environment ready. Keys written to tests/TheShop.E2E.Tests/.e2e-env"
}
finally { Pop-Location }
```

And the mirror `stop-e2e-env.ps1`:

```powershell
#Requires -Version 7
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path
Push-Location $repoRoot
try {
    supabase stop
    Remove-Item -Force -ErrorAction SilentlyContinue "$repoRoot/tests/TheShop.E2E.Tests/.e2e-env"
}
finally { Pop-Location }
```

> **CHECKPOINT (status output keys):** run `supabase status -o env` once and inspect the key
> names. Recent CLI versions emit `API_URL`, `ANON_KEY`, `SERVICE_ROLE_KEY`, and a mail-catcher
> URL (commonly still exported as `INBUCKET_URL` even now that the catcher is Mailpit). If the
> names differ from what `E2EEnvironment.Get(...)` call sites use in this plan (`API_URL`,
> `ANON_KEY`, `INBUCKET_URL`), update the constants at the call sites — not the script.

> **CHECKPOINT (`supabase db reset`):** `db reset` with no flags targets the **local** stack —
> that is the only form ever allowed here. If it fails because the local migration history
> conflicts, follow the error's `supabase migration repair` suggestion for the local stack
> only.

### 2.3 Persona seed data

The three personas, with **fixed UUIDs** so the seed is idempotent and role grants are
stable:

| Persona | Email | Role(s) |
|---|---|---|
| Admin | `e2e-admin@theshop.test` | Customer (auto) + Admin |
| Support | `e2e-support@theshop.test` | Customer (auto) + Support |
| Customer | `e2e-customer@theshop.test` | Customer (auto) |

`DISCOVERY` — read these files completely before writing any seed SQL:

- `supabase/migrations/0001_create_customers.sql` — the `customers` columns the sign-in
  precheck (`customer_exists`) needs: seed a `customers` row per persona (first/last name,
  DOB, email). **Without this row the sign-in page refuses the email before any OTP is
  sent.**
- `supabase/migrations/0007_create_rbac.sql` — `user_roles` shape and role keys; the
  `on_auth_user_created` trigger auto-assigns Customer on `auth.users` INSERT, and the guard
  triggers exempt `auth.uid() IS NULL` (seed context), so seeding Admin/Support rows is
  allowed. Use the **post-0009** role keys (`Customer`, `Support`, `Admin`, `SuperAdmin`).
- After a first `supabase start`, inspect the local `auth.users` / `auth.identities` column
  lists (`select column_name from information_schema.columns where table_schema='auth' and
  table_name='users'`) before writing the inserts — do not trust remembered column sets.

Then create `supabase/seed.sql` (applied automatically by `supabase db reset`; if one already
exists, **append** a clearly-delimited E2E section instead of overwriting). Per persona:
an `auth.users` insert (fixed UUID, `email_confirmed_at = now()`, empty `encrypted_password`,
`raw_app_meta_data = '{"provider":"email","providers":["email"]}'`), a matching
`auth.identities` insert (`provider = 'email'`, `provider_id` = the user UUID,
`identity_data` with `sub`/`email`/`email_verified`), a `customers` profile row, and the
Admin/Support `user_roles` grants — every statement `ON CONFLICT DO NOTHING`.

No product/brand seeding is needed — migration 0002 already seeds the catalogue (18 products,
6 brands, 5 categories), and `db reset` replays it every session.

Verify: `supabase db reset` completes without error; a `psql`/Studio query shows the three
personas in `auth.users` and the Admin/Support grants in `user_roles`.

### 2.4 OTP inbox reader (local mail catcher)

Create `tests/TheShop.E2E.Tests/Auth/OtpInbox.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Auth;

/// <summary>
/// Reads the local Supabase mail catcher over HTTP and extracts the most recent OTP code sent
/// to a given address. Local-stack only — no real email is ever sent, and capture is free and
/// unlimited.
/// </summary>
public static partial class OtpInbox
{
    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex OtpCode();

    public static async Task<string> WaitForOtpAsync(string email, TimeSpan? timeout = null)
    {
        var baseUrl = E2EEnvironment.Get("INBUCKET_URL").TrimEnd('/');
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            // Mailpit API (current Supabase CLI). For older Inbucket stacks see CHECKPOINT below.
            var search = await http.GetFromJsonAsync<JsonElement>(
                $"{baseUrl}/api/v1/search?query=to:{Uri.EscapeDataString(email)}");
            if (search.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
            {
                var id = messages[0].GetProperty("ID").GetString();
                var message = await http.GetFromJsonAsync<JsonElement>($"{baseUrl}/api/v1/message/{id}");
                var body = message.GetProperty("Text").GetString() ?? "";
                var match = OtpCode().Match(body);
                if (match.Success)
                {
                    // Delete after reading so a stale code can never match a later search.
                    await http.DeleteAsync($"{baseUrl}/api/v1/messages");
                    return match.Groups[1].Value;
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"No OTP email for {email} arrived within the timeout.");
    }
}
```

> **CHECKPOINT (mail catcher API):** open the URL exported by `supabase status` in a browser.
> If the UI is **Mailpit**, the code above is correct. If it is **Inbucket** (older CLI),
> replace the two requests with `GET {base}/api/v1/mailbox/{local-part-of-email}` (list; take
> newest by date) and `GET {base}/api/v1/mailbox/{local-part}/{id}` (read `body.text`), and
> drop the delete call. Decide by inspecting, not by CLI version string.

> **CHECKPOINT (OTP in the email body):** send one manual OTP from the sign-in page in a
> headed run and open the captured email. If it contains only a confirmation *link* and no
> 6-digit code, add the `{{ .Token }}` placeholder to the local magic-link template via
> `config.toml` (`[auth.email.template.magic_link]`) and restart the stack.

### 2.5 Persona sign-in and storage-state capture

Create `tests/TheShop.E2E.Tests/Auth/AuthStateFactory.cs`. This performs the **real UI login**
once per persona per run and snapshots the browser storage — the app itself writes
`shop.auth.session`, so the captured state is by construction the exact shape the app expects.

```csharp
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Auth;

/// <summary>
/// Signs each test persona in through the real OTP UI once per run and caches the resulting
/// Playwright storage state under <c>.auth-states/</c>. All authenticated journeys start from
/// these files instead of repeating the login flow.
/// </summary>
public static class AuthStateFactory
{
    public const string AdminEmail = "e2e-admin@theshop.test";
    public const string SupportEmail = "e2e-support@theshop.test";
    public const string CustomerEmail = "e2e-customer@theshop.test";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string StatePath(string email) =>
        Path.Combine(E2EEnvironment.RepoRoot, "tests", "TheShop.E2E.Tests", ".auth-states",
            email.Split('@')[0] + ".json");

    public static async Task<string> EnsureSignedInAsync(IBrowser browser, string email)
    {
        var path = StatePath(email);
        await Gate.WaitAsync();
        try
        {
            if (File.Exists(path)) return path; // minted earlier in this run
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var context = await ShopBrowser.NewContextAsync(browser);
            var page = await context.NewPageAsync();

            await page.GotoAsync(WebRoutes.Auth.SignIn);
            await page.Locator(".mud-layout, .mud-container, input").First
                .WaitForAsync(new() { Timeout = 30_000 }); // WASM boot

            await page.GetByTestId("signin-email").Locator("input").FillAsync(email);
            await page.GetByTestId("signin-submit").ClickAsync();

            var otp = await OtpInbox.WaitForOtpAsync(email);

            // OtpInput auto-advances between boxes: focus the first input, type the code.
            await page.GetByTestId("otp-input").Locator("input").First.ClickAsync();
            await page.Keyboard.TypeAsync(otp, new() { Delay = 50 });
            await page.GetByTestId("otp-submit").ClickAsync();

            // Signed-in landing: wait for a post-auth route, then confirm the session exists.
            await page.WaitForURLAsync(url => !url.Contains("sign-in") && !url.Contains("verify"),
                new() { Timeout = 30_000 });
            await Assertions.Expect(page.Locator(".mud-layout")).ToBeVisibleAsync();

            await context.StorageStateAsync(new() { Path = path });
            await context.DisposeAsync();
            return path;
        }
        finally { Gate.Release(); }
    }
}
```

`DISCOVERY` steps required before this compiles and passes:

1. Read `src/TheShop.Web/Common/Routes.cs` — use the real constant names (`Routes.Auth.SignIn`
   is confirmed to exist; check the verify route too).
2. Read the `OtpInput` component (`Glob src/TheShop.Web/**/OtpInput.razor`) — adapt the
   "focus first box, type code" interaction to its rendered structure.
3. The `data-testid` hooks (`signin-email`, `signin-submit`, `otp-input`, `otp-submit`) do
   not exist yet — adding them is Phase 3.1, which must land **before** running this factory.
   (Sequencing note: implement 2.5 and 3.1 together; GATE 2 runs after both.)
4. Read `SignInVerify.razor.cs` — confirm the post-sign-in destination and tighten the
   `WaitForURLAsync` predicate to that route.
5. The local stack enforces GoTrue's per-address OTP request cooldown (default 60s). With one
   login per persona per run this is invisible; a rapid restart may hit it — surface GoTrue's
   error message in the failure, don't blind-retry. (`supabase db reset` also clears it.)

### 2.6 Authenticated test base + sign-in journey

Create `tests/TheShop.E2E.Tests/Fixtures/AuthenticatedE2ETestBase.cs`:

```csharp
using TheShop.E2E.Tests.Auth;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>Base for journeys that start signed in as a given persona.</summary>
public abstract class AuthenticatedE2ETestBase(PlaywrightFixture playwright, string personaEmail)
    : E2ETestBase(playwright)
{
    private string? _statePath;

    protected override string? StorageStatePath => _statePath;

    public override async ValueTask InitializeAsync()
    {
        Assert.SkipUnless(E2EEnvironment.IsAvailable,
            "E2E environment not running — execute tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1 first.");
        _statePath = await AuthStateFactory.EnsureSignedInAsync(Playwright.Browser, personaEmail);
        await base.InitializeAsync();
    }
}
```

Create `tests/TheShop.E2E.Tests/Journeys/SignInJourneyTests.cs` (proves the whole Phase 2
chain):

```csharp
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Suite", "Smoke")]
[Trait("Feature", "auth")]
public sealed class SignInJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task Signed_in_customer_lands_on_an_authenticated_page()
    {
        await Page.GotoAsync("/");
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
        var session = await Page.EvaluateAsync<string?>("() => localStorage.getItem('shop.auth.session')");
        session.Should().NotBeNullOrEmpty("the OTP sign-in must persist a Supabase session");
    }
}
```

**GATE 2** (run after Phase 3.1's `data-testid` edits are also in place):
1. `pwsh tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1` → completes; `.e2e-env` exists.
2. Manual probe in a headed run: send an OTP from the sign-in page → the email (with a
   6-digit code) appears in the local mail catcher UI (proves 2.1/2.4 wiring before blaming
   test code).
3. `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E"` → **all tests PASS (0
   skipped, 0 failed)** — boot smoke + sign-in journey — twice in a row (storage-state reuse
   and seed idempotency hold).
4. Decode a persona's JWT from the captured storage state and confirm `app_roles`/`perms`
   claims are present (proves the config.toml hook enablement took effect).
5. `pwsh tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`, then re-run the tests → all SKIP.
   Restart the env afterwards.

---

## Phase 3 — Selectors, page objects, and feature journeys

Outcome: stable `data-testid` hooks in the UI, a page-object layer, and smoke journeys for
the already-shipped features.

### 3.1 `data-testid` convention (the only production edit)

Rule: every element an E2E test interacts with gets a `data-testid` via MudBlazor's
`UserAttributes`, kebab-case, `{page}-{element}`:

```razor
<MudTextField T="string"
              @bind-Value="_email"
              UserAttributes="@(new Dictionary<string, object?> { ["data-testid"] = "signin-email" })"
              ... />
```

Add hooks now for: `signin-email`, `signin-submit` (SignIn.razor), `otp-input`, `otp-submit`
(SignInVerify.razor / OtpInput.razor). Add further ids only as journeys need them — never
speculatively.

Constraints:
- `UserAttributes` renders on the component's root element; for `MudTextField` the attribute
  may land on a wrapper `div`, not the `<input>`. That is fine — page objects query
  `GetByTestId("signin-email").Locator("input")` when they need the input itself. Verify
  where each attribute lands with one headed run (`$env:E2E_HEADED=1`) the first time.
- These edits must not change any user-visible rendering: no new strings, no style changes.
- For the custom `OtpInput` component, add an attribute pass-through only if it doesn't
  already splat attributes; keep the change additive.

### 3.2 Page objects

Create `tests/TheShop.E2E.Tests/Pages/ShopPage.cs`:

```csharp
using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Base page object: navigation with WASM-boot wait, shared snackbar helpers.</summary>
public abstract class ShopPage(IPage page)
{
    protected IPage Page { get; } = page;

    /// <summary>Route of this page, from <c>TheShop.Web.Common.Routes</c> — never a literal.</summary>
    protected abstract string Route { get; }

    public async Task GotoAsync()
    {
        await Page.GotoAsync(Route);
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
    }

    /// <summary>MudBlazor snackbar text, for toast assertions.</summary>
    public ILocator Snackbar => Page.Locator(".mud-snackbar");
}
```

Then one class per route the journeys touch (`Pages/CataloguePage.cs`,
`Pages/Admin/ManageBrandsPage.cs`, `Pages/Admin/AdminConsolePage.cs`, …) — each exposing
*intent-level* methods (`AddBrandAsync(name)`, `SearchAsync(term)`), never raw locators, with
routes taken from `TheShop.Web.Common.Routes`.

`DISCOVERY` per page object: read the corresponding `.razor` file (and `.razor.cs`
code-behind) under `src/TheShop.Web/Pages/` first; derive locators in priority order:
1. `Page.GetByTestId(...)` (adding the testid per 3.1 where interaction is needed),
2. `Page.GetByRole(...)` with the accessible name from the `Strings` class
   (e.g. `new() { Name = Strings.SignUp }`) — locale-safe by construction,
3. never CSS classes of MudBlazor internals except the two blessed ones (`.mud-layout`,
   `.mud-snackbar`).

### 3.3 Feature journeys

One journey file per shipped feature, stamped for the pipeline:

| File | Traits | Journeys (adapt after reading the feature's spec) |
|---|---|---|
| `Journeys/CatalogueJourneyTests.cs` | `Feature=product-catalogue`, `Suite=Smoke` | catalogue renders seeded products; filter narrows results |
| `Journeys/ManageBrandsJourneyTests.cs` | `Feature=manage-brands` | admin adds a brand and sees it listed; edit persists |
| `Journeys/RbacJourneyTests.cs` | `Feature=rbac-hardening`, `Suite=Smoke` | admin reaches Admin Console; support persona sees view-only surface; customer is redirected away from `/admin` routes |

Rules for every journey:
- `DISCOVERY`: read `.specs/{feature}/spec.md` **Section 6 — Acceptance Criteria** first.
  Name each test after the AC it proves: `AC1_Admin_sees_manage_brands_entry`, etc. One
  journey per E2E-provable AC; ACs already fully covered by unit/bUnit tests need no E2E twin
  unless they involve routing, auth, or persistence.
- Class-level `[Trait("Category","E2E")]` + `[Trait("Feature","{feature}")]`; add
  `[Trait("Suite","Smoke")]` only to critical-path classes (whole Smoke suite < 5 minutes).
- Mutating journeys must be **rerun-tolerant within a session**: generate unique names
  (`$"e2e-brand-{Guid.NewGuid():N}"`) so re-running without a reset never collides.
  `supabase db reset` in the env script is the hard reset between sessions — no cleanup
  scripts or count-based assertions are needed beyond that.
- Do **not** register E2E files in any `.specs/*/test-manifest.json` — the manifest gate
  validates unit-test manifests for `/theshop.test`; E2E journeys are `/theshop.verify`'s
  inventory. Two gates, two inventories.

**GATE 3:**
1. `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E"` → all green with env up.
2. `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E&Suite=Smoke"` → green, wall
   time under 5 minutes.
3. `dotnet test tests/TheShop.Web.Tests` and `dotnet build TheShop.slnx` → still green (the
   `data-testid` edits broke nothing).
4. Run one mutating journey twice in a row without a reset → both green (rerun safety).

---

## Phase 4 — Wire into `/theshop.verify` (Tier 1 goes live)

Outcome: `/theshop.verify {feature}` runs the feature's E2E journeys automatically and only
falls back to guided-manual for ACs with no journey.

Edit `.claude/commands/theshop.verify.md`:

1. In **Step 3**, replace the Tier 1 bullet with:

   > **Tier 1 — Automated (preferred):** if `tests/TheShop.E2E.Tests` contains tests stamped
   > `[Trait("Feature","$ARGUMENTS")]` (check with
   > `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E&Feature=$ARGUMENTS" --list-tests`),
   > run the E2E environment script (`pwsh tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1`),
   > then `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E&Feature=$ARGUMENTS"`.
   > The E2E fixtures launch and tear down the app themselves — skip Step 2's manual launch
   > when running this tier. Map each test's pass/fail to its AC by the `AC{n}_` prefix in
   > the test name. Any **skipped** test means the environment didn't start — treat as a halt
   > (Template C), never as a pass. ACs with no matching `AC{n}_` test fall through to Tier 2
   > for that AC only.

2. In **Step 2**, prepend: *"Skip this step when Tier 1 applies — the E2E fixtures own app
   launch and teardown."*

3. In **Template A**'s "Driver tier" line, allow the mixed form
   `Tier 1 — automated ({n} ACs) + Tier 2 — guided manual ({m} ACs)`.

Do not change anything else in that file — verdict rules, teardown rule, and status-ledger
contract stay as written.

**GATE 4:** run `/theshop.verify manage-brands` (or the newest shipped user-facing feature
with journeys). The report must show `Driver tier: Tier 1 — automated` for the
journey-covered ACs with real pass evidence, and the app must not be left running afterwards
(`Get-NetTCPConnection -LocalPort 5218 -ErrorAction SilentlyContinue` → empty).

---

## Phase 5 — CI workflow

Outcome: PRs run the Smoke suite headlessly against a throwaway local stack on the runner;
failures upload replayable traces. Because every runner gets its **own isolated stack**, no
secrets are needed and concurrent runs can never interfere.

Create `.github/workflows/e2e.yml` (a **new** file — do not touch the two SWA workflows):

```yaml
name: E2E Smoke

on:
  pull_request:
    branches: [master, dev]
  workflow_dispatch:
  schedule:
    - cron: "0 3 * * *"   # nightly full run

jobs:
  e2e:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "10.0.x"

      - uses: supabase/setup-cli@v1
        with:
          version: latest

      - name: Start local Supabase (migrations + seed)
        run: |
          supabase start
          supabase status -o env > tests/TheShop.E2E.Tests/.e2e-env

      - name: Build E2E project
        run: dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj -c Release

      - name: Install Playwright browsers
        run: pwsh tests/TheShop.E2E.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

      - name: Run E2E tests
        run: |
          FILTER="Category=E2E&Suite=Smoke"
          if [ "${{ github.event_name }}" != "pull_request" ]; then FILTER="Category=E2E"; fi
          dotnet test tests/TheShop.E2E.Tests -c Release --no-build --filter "$FILTER" \
            --logger "trx;LogFileName=e2e-results.trx"

      - name: Upload traces on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-traces
          path: |
            **/playwright-traces/**
            **/e2e-results.trx
          if-no-files-found: ignore
```

Notes for the executor:
- `supabase start` on `ubuntu-latest` uses the preinstalled Docker daemon; the first image
  pull costs ~2 min. If CI time becomes a problem later, add image caching — not now.
- `supabase db reset` is unnecessary in CI: `supabase start` on a fresh runner already
  applies migrations and `seed.sql`.
- **No GitHub secrets or variables are required** — the local stack's keys are ephemeral and
  scoped to the runner. There is also no concurrency group: each run owns its whole stack.
- The E2E fixtures launch the app via `dotnet run` (Debug) while the test project builds
  Release — acceptable; do not "optimize" this in the first iteration.

**GATE 5:** push the branch, open a draft PR against `dev` (no AI attribution anywhere), and
confirm the `E2E Smoke` check runs and passes. If it fails, download the trace artifact,
diagnose, fix, re-push — do not merge red.

---

## Definition of done (final checklist)

- [ ] `tests/TheShop.E2E.Tests` exists, is in `TheShop.slnx`, builds in the solution build.
- [ ] Solution-level `dotnet test` with **no** E2E environment → E2E tests all skip; every
      other project unaffected.
- [ ] `config.toml` enables the custom access-token hook and sets the local Site URL; both
      committed.
- [ ] `start-e2e-env.ps1` + `dotnet test --filter "Category=E2E"` → all pass, twice in a row.
- [ ] Three personas (admin / support / customer) sign in through the real OTP UI once per
      run — codes read from the local mail catcher, zero external email anywhere — with
      storage states cached under `.auth-states/` (gitignored).
- [ ] Journeys exist for auth, catalogue, manage-brands, rbac-hardening; AC-named;
      feature-trait stamped; Smoke suite < 5 min; mutating journeys unique-named.
- [ ] `/theshop.verify` runs Tier 1 automatically for journey-covered features; teardown
      leaves port 5218 free.
- [ ] `.github/workflows/e2e.yml` green on a draft PR — no secrets required.
- [ ] No existing test manifest, gate script, or SWA workflow modified; no remote project
      touched by any E2E run.

---

## Appendix A — Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| E2E tests hang ~30 s then fail on `.mud-layout` | WASM never booted — usually the appsettings interception returned bad JSON | Inspect `ShopBrowser`'s fulfill body via the trace; keys must be `Supabase:Url` / `Supabase:PublishableKey` shape |
| App boots but every Supabase call 401s | Interception missed, so the app used `appsettings.Development.json` and hit the remote dev project | The `[ENV-CHECK]` console lines (Program.cs) show which URL loaded — it must be the local `127.0.0.1` API URL |
| `supabase start` fails | Docker Desktop not running, or ports 54321–54329 taken | Start Docker; `supabase stop --no-backup` then retry |
| `supabase db reset` migration mismatch | Local history vs MCP-applied remote migrations | CHECKPOINT in 2.2 — repair the **local** history only |
| OTP email never arrives in the catcher | Wrong mail-catcher URL key, or the per-address 60s cooldown after a rapid rerun | `supabase status` → open the mail UI; send a manual OTP from the sign-in page and watch for it |
| OTP email has a link but no 6-digit code | Local magic-link template lacks `{{ .Token }}` | CHECKPOINT in 2.4 — set the template in `config.toml`, restart the stack |
| Sign-in page refuses the persona email before any OTP | `customers` row missing (the `customer_exists` precheck) | Fix `supabase/seed.sql` per 2.3; `supabase db reset` |
| Sign-in succeeds but admin/support journeys all fail authorization | Access-token hook not enabled locally | 2.1 step 1 — `config.toml` `[auth.hook.custom_access_token]`; restart stack; delete `.auth-states/*` |
| Stale OTP typed into the UI | A previous run's email matched the inbox search | The Mailpit delete-after-read in 2.4 handles this; verify it survived any API adaptation |
| `Assert.Skip` compile errors | xUnit v3 API drift | See CHECKPOINT in 1.6 |
| Storage state exists but user is signed out | `db reset` invalidated sessions, or token expired mid-run | Delete `.auth-states/*` and rerun; states are per-run artifacts |
| Port 5218 in use at fixture start | Developer left the app running | Stop the manual instance; the fixture never kills processes it didn't start |

## Appendix B — Why these decisions (for the reviewer, not the executor)

- **Playwright over Selenium/Cypress:** auto-waiting fits Blazor WASM's async render; C#
  keeps one language across all five test projects; trace viewer gives replayable CI
  failures.
- **Local Docker stack over the hosted TheShop-Test project (final decision, 2026-08-01,
  after evaluating both):** the deciding factors were (1) **OTP capture** — the local stack's
  bundled Mailpit is free and unlimited, whereas a hosted project sends real email and needs
  a captive inbox: Mailtrap's free tier (~50/month) cannot cover nightly CI (3 logins/run ×
  ~30 nights ≈ 90+/month), Brevo has no capture sandbox (its "sandbox" is a drop-only API
  header; its inbound parsing needs an owned domain + MX + webhook), and the no-email
  alternative (admin `generate_link` minting) required a service-role secret in CI plus an
  unproven GoTrue token-type assumption; (2) **determinism** — `supabase db reset` gives
  pristine per-session state, where hosted forced unique-name rules, cleanup scripts, and
  serialized CI on a shared mutable DB; (3) **config as code** — the access-token hook (which
  all RBAC behavior depends on) is enabled in versioned `config.toml` locally vs. a
  forgettable dashboard checkbox; (4) **no secrets, no auto-pause, no network flakiness**.
  The accepted cost is the Docker Desktop dependency on the dev machine. **TheShop-Test is
  repurposed as staging** (schema current through 0018 as of 2026-08-01) — manual QA against
  real hosted infrastructure before promoting to prod — which is the role a hosted test
  project is genuinely best at.
- **Real-OTP login + storageState over hand-crafted session injection:** the session JSON in
  localStorage is produced by `Blazored.LocalStorage` serializing a `Supabase.Gotrue.Session`;
  hand-crafting that shape is fragile across package versions. Driving the true flow once and
  snapshotting `storageState` is exact by construction — and makes sign-in itself a covered
  journey for free.
- **Config interception over `appsettings.E2E.json`:** avoids depending on
  `WasmApplicationEnvironmentName` behavior under `dotnet run`, keeps E2E endpoints out of
  the deployable `wwwroot`, and structurally prevents an E2E run from ever touching
  TheShop-Dev.
- **E2E outside the test-manifest system:** the manifest/reconciliation machinery is
  `/theshop.test`'s per-feature unit-test contract; E2E journeys belong to
  `/theshop.verify`'s AC oracle. Two gates, two inventories, no cross-contamination.
