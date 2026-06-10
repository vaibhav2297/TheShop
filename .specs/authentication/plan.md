# Implementation Plan — Authentication

> Companion to `.specs/authentication/spec.md`. This plan is technical (HOW); the spec is non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Add passwordless email + 6-digit-OTP authentication for customers across sign-up and sign-in, plus a `customers` profile table that holds the first name / last name / date of birth captured at sign-up and enforces the 19+ Ontario age rule as a domain invariant. The implementation must (a) use Supabase Auth's built-in email OTP for code generation, delivery, and verification (b) persist sessions across browser restarts via local storage, (c) protect customer data with RLS, and (d) be fully localized in EN + FR. Because this is the first end-to-end vertical slice in the codebase, the plan also scaffolds the missing cross-cutting pieces (`Result<T>`, `ValidationBehavior`, `ICurrentUserService`, `SupabaseAuthStateProvider`, Supabase client registration) that subsequent features (Cart, Orders, etc.) will reuse.

## 2. Tech Stack

- **Domain:** C# 12, no external deps. New: `Customer` entity, `DateOfBirth` + `Email` value objects, `UnderageException`, `DomainException` base.
- **Application:** MediatR 14, FluentValidation 12, AutoMapper 16, `Result<T>` (project-internal, new). New: MediatR pipeline `ValidationBehavior<,>`.
- **Infrastructure:** `supabase-csharp` 1.1.1 for `Auth.SignInWithOtp` / `Auth.VerifyOTP` / `Auth.SignOut`, and for reading/writing the `customers` table.
- **Web:** MudBlazor 9 (`MudTextField`, `MudDatePicker`, `MudButton`, `MudForm`, `MudAlert`, `MudProgressCircular`), Blazored.LocalStorage 4 for session persistence, `Microsoft.AspNetCore.Components.Authorization` for `AuthenticationStateProvider` + `AuthorizeView`, bUnit for component tests.
- **Persistence:** Supabase Postgres + Auth. Auth settings: email OTP enabled, magic-link disabled (6-digit numeric code), `email_otp_expiry = 600s`, rate-limit ≥ 60s between sends per email. RLS on `customers`.

## 3. High-level Architecture

A single user action ("Send code" or "Verify code") propagates through the standard four-layer flow. Two sub-flows exist — sign-up and sign-in — and they differ only at the pre-OTP guard (does a profile already exist?) and the post-OTP side-effect (insert profile vs. nothing). Both share the same `IAuthService` contract.

```
SignUp.razor / SignIn.razor / *Verify.razor
   ↓ IMediator.Send(RequestSignUpOtpCommand | RequestSignInOtpCommand
                    | VerifySignUpOtpCommand | VerifySignInOtpCommand
                    | ResendOtpCommand | SignOutCommand)
   ↓
ValidationBehavior  →  Command handler (Application)
                          ├── ICustomerRepository.ExistsForEmailAsync(...)        // sign-up/sign-in guards
                          ├── DateOfBirth.Create(dob)  →  enforces 19+ invariant   // sign-up only
                          ├── IAuthService.SendOtpAsync(email, type)                // both flows
                          └── IAuthService.VerifyOtpAsync(email, code)              // verify flows
   ↓
SupabaseAuthService (Infrastructure)  → Supabase Auth (OTP create/verify/sign-out)
SupabaseCustomerRepository (Infrastructure) → `customers` table (RLS-gated)
   ↓
Result<SessionDto> → page updates AuthState, persists session via LocalStorage,
                     SupabaseAuthStateProvider re-publishes ClaimsPrincipal,
                     UI navigates to /account (or returnUrl)
```

## 4. Data Model

### Domain entities & value objects

- **`Customer`** (entity, new). Fields: `Id` (matches `auth.users.id`), `FirstName`, `LastName`, `DateOfBirth`, `Email`, `CreatedAt`. Factory: `Customer.Register(Guid authUserId, string firstName, string lastName, Email email, DateOfBirth dob)` — validates non-empty names and enforces 19+ via `dob.RequireAtLeast(19)`. No mutating methods needed for the auth slice.
- **`Email`** (value object, new). Single field `Value`. Factory `Email.Create(string)` validates non-null + RFC-5322-lite regex; throws `DomainException(nameof(Strings.Email_Invalid))` on failure.
- **`DateOfBirth`** (value object, new). Single field `Value` (`DateOnly`). Methods: `int AgeOn(DateOnly today)`, `void RequireAtLeast(int minAge, DateOnly? today = null)` — throws `UnderageException(nameof(Strings.Auth_Underage))` if age < minAge. Factory `DateOfBirth.Create(DateOnly)` throws on future dates.
- **`UnderageException : DomainException`** (new). Carries `MessageKey = nameof(Strings.Auth_Underage)`.
- **`DomainException`** (new, base). `public string MessageKey { get; }` — every domain throw carries a resource key, never localized text.

### DTOs (Application → Web)

- **`SessionDto`** — `UserId: Guid`, `Email: string`, `AccessToken: string`, `RefreshToken: string`, `ExpiresAt: DateTimeOffset`, `Customer: CustomerProfileDto`. Returned from `VerifySignUpOtpCommand` and `VerifySignInOtpCommand`.
- **`CustomerProfileDto`** — `Id: Guid`, `FirstName: string`, `LastName: string`, `Email: string`, `DateOfBirth: DateOnly`.
- **`OtpRequestedDto`** — `Email: string`, `ResendCooldownSeconds: int` (60). Returned from `RequestSignUpOtpCommand` / `RequestSignInOtpCommand` / `ResendOtpCommand` so the UI can drive the resend timer.

### Cross-cutting (Application → all features)

- **`Result<T>`** (new). Properties: `IsSuccess`, `Value`, `Error` (string key). Static `Result.Ok(T)` and `Result.Fail<T>(string errorKey)`. Per `ARCHITECTURE.md §Result<T> Pattern`.
- **`ICurrentUserService`** (new). `Guid? Id { get; }`, `string? Email { get; }`, `bool IsAuthenticated { get; }`. Implemented in Web as `BlazorCurrentUserService` reading from `AuthenticationStateProvider`.

### Database tables (new)

| Table | Purpose | Key columns |
|---|---|---|
| `customers` (new) | One row per signed-up customer; mirrors `auth.users.id` and stores profile fields | `id UUID PK REFERENCES auth.users(id) ON DELETE CASCADE`, `first_name TEXT NOT NULL`, `last_name TEXT NOT NULL`, `date_of_birth DATE NOT NULL`, `email TEXT NOT NULL`, `created_at TIMESTAMPTZ NOT NULL DEFAULT now()` |

### Indexes
- `customers (email)` UNIQUE — supports the "does an account exist for this email" guard and enforces single-account-per-email at the storage layer.

## 5. Core Design Decisions

1. **Decision:** Use Supabase Auth's built-in email-OTP flow (`SignInWithOtp` + `VerifyOTP`) for code generation, delivery, expiry, attempt-counting, and resend rate-limiting — do not roll our own OTP table.
   - **Why:** Supabase already enforces the spec's numeric (`should_create_user`, `email_otp_expiry`, max attempts, per-email cooldown) requirements server-side. Re-implementing them invites bugs and a second source of truth. Spec Constraints (10-min validity, 5 attempts, 60s cooldown, single-use, code invalidated on resend) all map onto Supabase's existing knobs.
   - **Rejected:** Hand-rolled `otp_codes` table + Resend email — more code, more attack surface, no business benefit at MVP.

2. **Decision:** Sign-up profile data (`FirstName`, `LastName`, `DateOfBirth`) lives in a scoped client state store (`PendingSignUpState`) between the email-submit step and the code-verify step. On successful `VerifyOTP`, the verify handler creates the `customers` row using that data.
   - **Why:** The two-page flow needs to remember the sign-up form until the code is verified. Server-side scratch storage is overkill; Supabase user metadata is opaque and harder to validate domain-side; client state is simplest and lives only as long as the user's tab.
   - **Rejected:** (a) Pass via Supabase `data` metadata on `SignInWithOtp` — couples profile shape to Supabase. (b) Insert into `customers` *before* verify — leaks unverified rows.

3. **Decision:** Age (≥19) is a domain invariant on `DateOfBirth`, not just a validator rule.
   - **Why:** Ontario age-of-sale is a legal constraint, not a UI nicety. Spec Constraint #1 + Section 5 edge case. Putting the check in the value object means *any* code path that constructs a `Customer` is guaranteed to be 19+ — defensive in depth.
   - **Rejected:** Validator-only check — easy to bypass by another caller in the future.

4. **Decision:** Sign-up and sign-in use **separate** MediatR commands rather than one polymorphic command.
   - **Why:** They have different inputs (sign-up has names + DOB), different guards (sign-up: profile must *not* exist; sign-in: profile *must* exist), and different success paths (sign-up: insert customer row, then session; sign-in: just session). Forcing them into one command would mean optional fields and runtime branching — both anti-patterns.
   - **Rejected:** Single `RequestOtpCommand` with `OtpPurpose` enum — handler branches everywhere.

5. **Decision:** Session is persisted via `Blazored.LocalStorage` and rehydrated on app start; sliding-refresh is delegated to the Supabase SDK (`PersistSession = true`, `AutoRefreshToken = true`).
   - **Why:** Spec FR-8 + AC-10 require persistence across browser restarts. Supabase SDK does this natively if a `IGotrueSessionPersistence` adapter is supplied; we wrap `Blazored.LocalStorage` as that adapter.
   - **Rejected:** Cookies — not available to Blazor WASM client. In-memory — fails FR-8.

6. **Decision:** OTP email template is bilingual in MVP (single Supabase template containing both EN and FR blocks).
   - **Why:** Supabase Auth has one OTP template per project. Lets us ship without an Auth Hook / custom sender. Spec Constraint requires EN+FR; a bilingual template satisfies that.
   - **Rejected:** Supabase Send-Email auth hook + Resend per-locale templates — better long-term solution but adds an Edge Function deployment. Flagged as Section 11 follow-up.

7. **Decision:** 60-second resend cooldown is enforced **on the client** via a `MudButton` disabled-with-countdown, and Supabase's per-email rate-limit acts as the authoritative server-side guard.
   - **Why:** Spec Constraint specifies 60s. Supabase's default OTP rate-limit is ≥ 60s — set it to exactly 60s in project auth config so client and server agree. Defense-in-depth.

## 6. Core Functional Flow

### Flow 1: Sign up (request OTP)

1. User opens `/sign-up`, enters First Name, Last Name, Email, Date of Birth in a `MudForm`, clicks "Send code".
2. `SignUp.razor` calls `Mediator.Send(new RequestSignUpOtpCommand(FirstName, LastName, Email, DateOfBirth))`.
3. `ValidationBehavior` runs `RequestSignUpOtpCommandValidator` (non-empty names, valid email format, DOB in past, ≥ 19 years). On failure → `ValidationException` → handler catches via global pipeline and surfaces first error key.
4. Handler calls `ICustomerRepository.ExistsForEmailAsync(email, ct)`. If true → `Result.Fail<OtpRequestedDto>(nameof(Strings.Auth_AccountAlreadyExists))`.
5. Handler calls `IAuthService.SendSignUpOtpAsync(email, ct)` — internally invokes `client.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email) { CreateUser = true })`.
6. Returns `Result.Ok(new OtpRequestedDto(email, 60))`.
7. `SignUp.razor` stores `FirstName/LastName/Email/DateOfBirth` in `PendingSignUpState` and navigates to `/sign-up/verify`.
8. UI surface: on `Result.IsSuccess`, `Snackbar.Add(Strings.Auth_CodeSent, Severity.Success)`; on failure, `Snackbar.Add(Localizer[result.Error], Severity.Error)`.

### Flow 2: Sign up (verify OTP)

1. `SignUpVerify.razor` reads `PendingSignUpState`. If empty (deep link or refresh-after-clear) → redirect to `/sign-up`.
2. User enters 6-digit code into a `MudTextField` (`InputMode="numeric"`, max length 6), clicks "Verify".
3. Page calls `Mediator.Send(new VerifySignUpOtpCommand(Email, Code, FirstName, LastName, DateOfBirth))`.
4. Handler calls `IAuthService.VerifyOtpAsync(email, code, OtpType.Email, ct)`. Returns `Session` or throws on bad/expired/too-many-attempts (translated to `Result.Fail` with the corresponding error key — see §9).
5. On success, handler builds a `Customer` via `Customer.Register(session.User.Id, firstName, lastName, Email.Create(email), DateOfBirth.Create(dob))`. Any domain exception → `Result.Fail<SessionDto>(ex.MessageKey)` and we call `IAuthService.SignOutAsync` to roll back the auth session.
6. Handler calls `ICustomerRepository.AddAsync(customer, ct)`.
7. Returns `Result.Ok(_mapper.Map<SessionDto>(session, customer))`.
8. `SignUpVerify.razor` clears `PendingSignUpState`, persists the session via `LocalStorage`, calls `AuthState.SetUser(...)`, and `NavigationManager.NavigateTo("/account")`.

### Flow 3: Sign in (request OTP)

1. User opens `/sign-in`, enters Email, clicks "Send code".
2. Page calls `Mediator.Send(new RequestSignInOtpCommand(Email))`.
3. Validator: email non-empty + format. Handler calls `ICustomerRepository.ExistsForEmailAsync(email, ct)`. If false → `Result.Fail(nameof(Strings.Auth_AccountNotFound))`.
4. Handler calls `IAuthService.SendSignInOtpAsync(email, ct)` — `SignInWithPasswordlessEmailOptions(email) { CreateUser = false }`.
5. Returns `Result.Ok(new OtpRequestedDto(email, 60))`; page navigates to `/sign-in/verify?email=...`.

### Flow 4: Sign in (verify OTP)

1. User enters 6-digit code on `/sign-in/verify`, clicks "Verify".
2. Page calls `Mediator.Send(new VerifySignInOtpCommand(Email, Code))`.
3. Handler calls `IAuthService.VerifyOtpAsync(...)`, then `ICustomerRepository.GetByIdAsync(session.User.Id, ct)`.
4. Returns `Result.Ok(_mapper.Map<SessionDto>(session, customer))`.
5. Page persists session + sets `AuthState`, navigates to `returnUrl ?? "/account"`.

### Flow 5: Resend code

1. On either Verify page, the "Resend code" `MudButton` is disabled until a 60-second timer (driven from `OtpRequestedDto.ResendCooldownSeconds`) elapses.
2. Click → `Mediator.Send(new ResendOtpCommand(Email, OtpPurpose))`. Handler re-runs the same guard as the original Request command, then calls `IAuthService.SendSignUpOtpAsync` / `SendSignInOtpAsync`. Supabase invalidates the previous code automatically.
3. Restart the 60-second timer.

### Flow 6: Sign out

1. User clicks "Sign out" in account menu (lives in `MainLayout.razor` inside `<AuthorizeView>`).
2. Page calls `Mediator.Send(new SignOutCommand())`.
3. Handler calls `IAuthService.SignOutAsync(ct)` → Supabase `client.Auth.SignOut()`.
4. Page clears `LocalStorage` session blob, calls `AuthState.Clear()`, `NavigationManager.NavigateTo("/")`.

### Flow 7: Session expiry mid-browse

1. `SupabaseAuthStateProvider` subscribes to `client.Auth.AddStateChangedListener` and re-publishes `AuthenticationState` whenever the session changes.
2. When the SDK fails to refresh (refresh token expired), the listener fires with `SignedOut`. Provider returns an anonymous `ClaimsPrincipal`.
3. Any `<AuthorizeView>` re-renders the unauthorized branch; pages with `@attribute [Authorize]` redirect via Blazor's `RedirectToLogin` pattern, carrying `?returnUrl=...`.
4. Cart state is *not* cleared (server-persisted by the future Cart feature — see Section 11 assumption).

## 7. Development Plan

### Phase 0 — Cross-cutting foundations (one-time, will be reused by Cart/Orders/etc.)

- `TheShop.Domain/Exceptions/DomainException.cs` — base with `MessageKey` property.
- `TheShop.Application/Common/Models/Result.cs` + `Result<T>` per `ARCHITECTURE.md §Result<T> Pattern`.
- `TheShop.Application/Common/Behaviors/ValidationBehavior.cs` (MediatR pipeline). Wire in `Application/DependencyInjection.cs` via `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));`.
- `TheShop.Application/Common/Interfaces/ICurrentUserService.cs`.
- `TheShop.Application/Common/Interfaces/IAuthService.cs` and `ICustomerRepository.cs`.
- `TheShop.Infrastructure/Persistence/SupabaseClient.cs` — singleton factory reading `Supabase:Url` and `Supabase:AnonKey` from `IConfiguration`; configures `SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = false }` and a custom `IGotrueSessionPersistence` backed by `Blazored.LocalStorage`.
- `TheShop.Infrastructure/DependencyInjection.cs` — register `Supabase.Client` as singleton, plus `IAuthService → SupabaseAuthService`, `ICustomerRepository → SupabaseCustomerRepository`.
- `TheShop.Web/Auth/BlazorCurrentUserService.cs` implementing `ICurrentUserService` (reads `AuthenticationStateProvider.GetAuthenticationStateAsync()`); register in `Web/DependencyInjection.cs`.
- `TheShop.Web/Program.cs` — `services.AddAuthorizationCore()`, `services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>()`, `services.AddBlazoredLocalStorage()`.
- `wwwroot/appsettings.json` (or `appsettings.Development.json`) — add `Supabase:Url`, `Supabase:AnonKey` placeholders.
- Tests: `Result<T>` unit tests in `TheShop.Application.Tests`.

### Phase 1 — Domain

- `TheShop.Domain/ValueObjects/Email.cs` + `DateOfBirth.cs`.
- `TheShop.Domain/Entities/Customer.cs`.
- `TheShop.Domain/Exceptions/UnderageException.cs`.
- Tests in `TheShop.Domain.Tests/`:
  - `DateOfBirth_RequireAtLeast_WhenUnder19_ThrowsUnderageException`
  - `DateOfBirth_AgeOn_OnBirthday_ReturnsExactAge`
  - `Email_Create_WhenMalformed_ThrowsDomainException`
  - `Customer_Register_WhenAllValid_ProducesCustomerWithMatchingIds`
  - `Customer_Register_WhenUnder19_ThrowsUnderageException`

### Phase 2 — Application use cases

- `TheShop.Application/Features/Auth/DTOs/SessionDto.cs`, `CustomerProfileDto.cs`, `OtpRequestedDto.cs`.
- `TheShop.Application/Features/Auth/Commands/`:
  - `RequestSignUpOtpCommand` + handler + validator.
  - `VerifySignUpOtpCommand` + handler + validator.
  - `RequestSignInOtpCommand` + handler + validator.
  - `VerifySignInOtpCommand` + handler + validator.
  - `ResendOtpCommand` + handler + validator (carries `OtpPurpose` enum).
  - `SignOutCommand` + handler.
- `TheShop.Application/Mapping/AuthMappingProfile.cs` — `Session + Customer → SessionDto`.
- New keys in `Strings.resx` + `Strings.fr.resx` (see §9).
- Tests in `TheShop.Application.Tests/Features/Auth/` (NSubstitute for repo + auth service):
  - `RequestSignUpOtpHandler_WhenEmailExists_ReturnsAccountAlreadyExists`
  - `RequestSignUpOtpHandler_WhenUnder19_ReturnsUnderageKey`
  - `RequestSignUpOtpHandler_WhenValid_CallsAuthService_AndReturnsOk`
  - `VerifySignUpOtpHandler_OnAuthServiceFailure_ReturnsMappedKey`
  - `VerifySignUpOtpHandler_OnSuccess_PersistsCustomer_AndReturnsSession`
  - `VerifySignUpOtpHandler_OnRepositoryFailure_CallsSignOut`
  - `RequestSignInOtpHandler_WhenNoAccount_ReturnsAccountNotFound`
  - `VerifySignInOtpHandler_OnSuccess_ReturnsSessionWithProfile`
  - `ResendOtpHandler_RespectsPurposeGuards`
  - Validator tests covering empty/whitespace names, malformed email, future/under-19 DOB.

### Phase 3 — Infrastructure

- `TheShop.Infrastructure/Auth/SupabaseAuthService.cs` implementing `IAuthService`:
  - `SendSignUpOtpAsync(string email, CancellationToken)` → `client.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email) { CreateUser = true })`.
  - `SendSignInOtpAsync` → same with `CreateUser = false`.
  - `VerifyOtpAsync(string email, string code, CancellationToken)` → `client.Auth.VerifyOTP(email, code, Constants.EmailOtpType.Email)`. Maps Supabase `GotrueException` codes to our error keys (see §9).
  - `SignOutAsync` → `client.Auth.SignOut()`.
- `TheShop.Infrastructure/Persistence/Records/CustomerRecord.cs` (Supabase model with `[Table("customers")]`, `[PrimaryKey("id", false)]`).
- `TheShop.Infrastructure/Persistence/Mappers/CustomerMapper.cs` — `CustomerRecord ↔ Customer` (uses `Email.Create` / `DateOfBirth.Create`).
- `TheShop.Infrastructure/Persistence/Repositories/SupabaseCustomerRepository.cs` implementing `ICustomerRepository` (`ExistsForEmailAsync`, `GetByIdAsync`, `AddAsync`).
- `TheShop.Infrastructure/Auth/LocalStorageSessionPersistence.cs` implementing `IGotrueSessionPersistence` over `ILocalStorageService`. (Note: this lives in Infrastructure because Supabase-SDK contracts live there.)
- Tests in `TheShop.Infrastructure.Tests/` against a local PostgreSQL container (Testcontainers) for `SupabaseCustomerRepository` — auth-service unit-level coverage is limited; rely on Application-layer tests that mock `IAuthService` for behavioral coverage.

### Phase 4 — Database & Supabase project config

- Migration `0001_create_customers.sql` (apply via Supabase MCP `apply_migration` against the dev branch):
  - `CREATE TABLE customers (...)` per §10.
  - Unique index on `customers(email)`.
  - RLS policies per §10.
- Supabase Auth settings (via dashboard or `update_config` MCP tool):
  - Email auth: enable, magic-link off, **OTP on with 6-digit numeric** (`mailer_otp_length=6`, `mailer_autoconfirm=false`).
  - `email_otp_expiry = 600` (10 minutes).
  - Per-user OTP rate limit ≥ 60 seconds.
  - Bilingual EN/FR OTP email template — body contains both languages stacked, e.g. `Your code: {{ .Token }}\nVotre code : {{ .Token }}`.

### Phase 5 — Web

**Figma references** *(read by `shop-ui-implementer` at impl time)*

- **File:** `https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2263-4709`
- **Nodes:**
  - `2267:8121` — Login — Sign-in page: email input + "Send code" CTA; maps to `SignIn.razor` (`/sign-in`).
  - `2276:8368` — Login - Verify — Sign-in OTP entry: 6-digit code field, "Verify" button, active "Resend" link; maps to `SignInVerify.razor` (`/sign-in/verify`).
  - `2267:8169` — Login - Verify - Send Again — Sign-in verify page in its post-resend / cooldown-active state; shows timer state for the resend button.
  - `2276:8443` — Sign Up — Sign-up page: First Name, Last Name, Email, Date of Birth fields + "Send code" CTA; maps to `SignUp.razor` (`/sign-up`).
  - `2276:8504` — Sign Up - Verify — Sign-up OTP entry: 6-digit code field, "Verify" button, resend link; maps to `SignUpVerify.razor` (`/sign-up/verify`).
  - `2276:8572` — Sign Up - Verify - Send Again — Sign-up verify page in its post-resend / cooldown-active state; shows timer state for the resend button.

- `TheShop.Web/Auth/SupabaseAuthStateProvider.cs` — see `ARCHITECTURE.md §Authorization wiring` template; subscribe to `client.Auth.AddStateChangedListener` and call `NotifyAuthenticationStateChanged`.
- `TheShop.Web/State/PendingSignUpState.cs` — holds the in-flight sign-up form data (scoped per circuit, but really per-tab in WASM).
- `TheShop.Web/Pages/Account/SignUp.razor` (`/sign-up`) — `MudForm` with `MudTextField` × 3 + `MudDatePicker` + Send-code button.
- `TheShop.Web/Pages/Account/SignUpVerify.razor` (`/sign-up/verify`) — code input + Verify + Resend.
- `TheShop.Web/Pages/Account/SignIn.razor` (`/sign-in`) — Email + Send-code.
- `TheShop.Web/Pages/Account/SignInVerify.razor` (`/sign-in/verify`) — code input + Verify + Resend.
- `TheShop.Web/Pages/Account/AccountHome.razor` (`/account`) wrapped in `<AuthorizeView>` (placeholder — full account UI is out of scope for this slice; show greeting + sign-out only).
- `TheShop.Web/Components/Layout/MainLayout.razor` — wrap content in `<CascadingAuthenticationState>`; in `MudAppBar`, replace the static account icon with an `<AuthorizeView>` that shows Sign-in / Sign-up buttons when unauthenticated and an account menu (with Sign-out item) when authenticated.
- `TheShop.Web/App.razor` — switch to `<AuthorizeRouteView>` with a `<NotAuthorized>` template that redirects to `/sign-in?returnUrl=...`.
- All new strings added to `Strings.resx` (see §9) and mirrored in `Strings.fr.resx` (French translations or `[TODO-FR]` placeholders pending translator).
- Tests in `TheShop.Web.Tests/` (bUnit):
  - `SignUp_WhenAllFieldsValid_DispatchesRequestSignUpOtpCommand`
  - `SignUp_WhenDobUnder19_ShowsValidationMessage_DoesNotDispatch`
  - `SignUpVerify_WhenStateMissing_RedirectsToSignUp`
  - `SignUpVerify_OnSuccessfulCommand_NavigatesToAccount_AndSetsAuthState`
  - `SignInVerify_ResendButton_DisabledForFirst60Seconds_ThenEnabled`
  - `MainLayout_WhenAuthenticated_ShowsSignOut_AndHidesSignIn`

### Phase 6 — End-to-end & polish

- Manual happy-path verification using a Supabase dev project: sign-up → email arrives in both languages → enter code → land on `/account`; sign-out → sign-in → land on `/account`.
- Manual verification of every spec Edge Case (existing email, no-account email, under-19, wrong code, expired code, lockout, premature resend).
- `/theshop.test authentication` then `/theshop.review authentication`.
- Wire the FR translations in `Strings.fr.resx` (or hand off to translator with key list).

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| **AC-1** New user can sign up → code → enter → signed in | Phase 1 (`Customer.Register`), Phase 2 (`RequestSignUpOtpHandler` + `VerifySignUpOtpHandler` happy paths), Phase 5 (`SignUp.razor` + `SignUpVerify.razor`) |
| **AC-2** Returning user can sign in by email → code → enter | Phase 2 (`RequestSignInOtpHandler` + `VerifySignInOtpHandler`), Phase 5 (`SignIn.razor` + `SignInVerify.razor`) |
| **AC-3** Sign-up blocked when DOB < 19 with clear message | Phase 1 (`DateOfBirth.RequireAtLeast` + `UnderageException`), Phase 2 (`RequestSignUpOtpCommandValidator` carrying `Auth_Underage` key), Phase 5 (form-level validation message) |
| **AC-4** Sign-up with existing email shows "Account already exists." and sends no code | Phase 2 (`RequestSignUpOtpHandler` ExistsForEmail guard) + Phase 4 (UNIQUE `customers(email)` as backstop) |
| **AC-5** Sign-in with unknown email shows "No account found." and sends no code | Phase 2 (`RequestSignInOtpHandler` ExistsForEmail guard) |
| **AC-6** Code older than 10 minutes is rejected with "expired" message | Phase 4 (`email_otp_expiry = 600`), Phase 2/3 (map Supabase `otp_expired` error → `Auth_CodeExpired`) |
| **AC-7** 5 wrong attempts invalidates code and returns to email entry | Phase 2/3 (map Supabase `otp_disabled` / max-attempts → `Auth_TooManyAttempts`), Phase 5 (page handles that key by redirecting to email screen) |
| **AC-8** Resend button disabled for first 60s with visible countdown | Phase 4 (server-side rate-limit at 60s), Phase 5 (`SignUpVerify.razor` / `SignInVerify.razor` countdown via `MudTimer` or `System.Threading.Timer`) |
| **AC-9** Requesting new code invalidates the previous one | Supabase native behaviour; no code change. Covered by integration test in Phase 3 against a Supabase test branch. |
| **AC-10** Signed-in user stays signed in across browser restarts | Phase 0 (`LocalStorageSessionPersistence`), Phase 5 (`SupabaseAuthStateProvider` rehydrates session on app start) |
| **AC-11** Sign-out ends session and returns to public view | Phase 2 (`SignOutCommand`), Phase 3 (`SupabaseAuthService.SignOutAsync`), Phase 5 (layout menu) |
| **AC-12** All screens, validation messages, code emails in EN + FR | Phase 4 (bilingual email template), Phase 5 (`Strings.fr.resx` mirror of every new key) |

## 9. Validation & Error Handling Strategy

### Validators (Application layer, FluentValidation)

- `RequestSignUpOtpCommandValidator`:
  - `FirstName` not empty → `nameof(Strings.Auth_FirstName_Required)`
  - `LastName` not empty → `nameof(Strings.Auth_LastName_Required)`
  - `Email` not empty + matches email regex → `nameof(Strings.Email_Required)` / `nameof(Strings.Email_Invalid)`
  - `DateOfBirth` `< DateOnly.FromDateTime(DateTime.Today)` → `nameof(Strings.Auth_Dob_InPast)`
  - `DateOfBirth` age ≥ 19 → `nameof(Strings.Auth_Underage)`
- `RequestSignInOtpCommandValidator`: email required + format.
- `VerifySignUpOtpCommandValidator` / `VerifySignInOtpCommandValidator`: email required + format; code is 6 numeric digits → `nameof(Strings.Auth_Code_Invalid)`.
- `ResendOtpCommandValidator`: email required + format; purpose is a known enum value.

### Domain exceptions

- `UnderageException : DomainException` — `MessageKey = nameof(Strings.Auth_Underage)`.
- Generic `DomainException(string messageKey)` — used for `Email.Create` (`Email_Invalid`) and DOB future-date (`Auth_Dob_InPast`).

### Result.Fail error keys + Supabase error mapping

| Key (resx) | English text | Triggered by |
|---|---|---|
| `Auth_AccountAlreadyExists` | "An account with this email already exists." | Sign-up `ExistsForEmail` guard |
| `Auth_AccountNotFound` | "No account found for this email." | Sign-in `ExistsForEmail` guard |
| `Auth_Underage` | "You must be at least 19 years old to create an account." | `UnderageException` |
| `Auth_Code_Incorrect` | "Incorrect code. Please try again." | Supabase `otp_invalid` |
| `Auth_CodeExpired` | "This code has expired. Please request a new one." | Supabase `otp_expired` |
| `Auth_TooManyAttempts` | "Too many incorrect attempts. Please start again." | Supabase `otp_disabled` / attempt-cap |
| `Auth_Code_Invalid` | "Please enter the 6-digit code from your email." | Validator on `VerifyOtp*Command` |
| `Auth_ResendTooSoon` | "Please wait before requesting a new code." | Supabase rate-limit error (defensive — UI also prevents this) |
| `Auth_FirstName_Required` | "First name is required." | Validator |
| `Auth_LastName_Required` | "Last name is required." | Validator |
| `Auth_Dob_InPast` | "Please enter a valid date of birth." | Validator |
| `Email_Invalid` | "Please enter a valid email address." | `Email.Create` / Validator |
| `Auth_CodeSent` | "We sent a 6-digit code to your email." | Toast on Request* success |
| `Auth_SignedIn` | "Signed in." | Toast on Verify* success |
| `Auth_SignedOut` | "You have been signed out." | Toast on SignOut success |
| `Auth_SessionExpired` | "Please sign in again to continue." | `<NotAuthorized>` template message |
| `SignUp_PageTitle`, `SignUp_Heading`, `SignIn_PageTitle`, `SignIn_Heading`, `Verify_PageTitle`, `Verify_Heading`, `Verify_CodeLabel`, `Verify_CodeHint`, `Verify_Submit`, `Verify_Resend`, `Verify_ResendIn`, `Auth_SendCode`, `FirstName_Label`, `LastName_Label`, `DateOfBirth_Label`, `DateOfBirth_Hint` | (text per resx) | UI labels |

All keys are mirrored in `Strings.fr.resx`. Use `[TODO-FR]` placeholder for any value awaiting professional translation; commit the keys regardless so the build doesn't break.

## 10. Database Schema & RLS Policies

### Schema

```sql
CREATE TABLE customers (
    id              UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    first_name      TEXT NOT NULL CHECK (length(trim(first_name)) > 0),
    last_name       TEXT NOT NULL CHECK (length(trim(last_name))  > 0),
    date_of_birth   DATE NOT NULL CHECK (date_of_birth < CURRENT_DATE),
    email           TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX idx_customers_email ON customers (lower(email));
```

### RLS policies (`ARCHITECTURE.md` §Security — three layers, all required)

```sql
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;

-- A signed-in customer can read and update only their own row
CREATE POLICY "customers_self_select" ON customers
    FOR SELECT USING (id = auth.uid());

CREATE POLICY "customers_self_insert" ON customers
    FOR INSERT WITH CHECK (id = auth.uid());

CREATE POLICY "customers_self_update" ON customers
    FOR UPDATE USING (id = auth.uid()) WITH CHECK (id = auth.uid());

-- Admin can read all (for support); writes go via service-role only
CREATE POLICY "customers_admin_select" ON customers
    FOR SELECT USING ((auth.jwt() ->> 'role') = 'admin');
```

Note: the `customers_self_insert` policy is what lets `VerifySignUpOtpHandler` insert the profile row *from the authenticated session that was just minted by `VerifyOTP`*. Order matters — the OTP must verify (yielding a valid `auth.uid()`) before the insert runs, otherwise the policy rejects it.

### Auth-side guard (defense-in-depth for "no account found" case)

Sign-in's `ExistsForEmail` guard reads from the `customers` table under RLS. Because RLS prevents an unauthenticated client from `SELECT`-ing the `customers` row, this guard must run *server-side* via Supabase's `rpc` or via the anon key against a `customer_exists(email TEXT)` SQL function flagged `SECURITY DEFINER` with a strict allowlist. Define:

```sql
CREATE OR REPLACE FUNCTION public.customer_exists(p_email TEXT)
RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT EXISTS (SELECT 1 FROM customers WHERE lower(email) = lower(p_email))
$$;

GRANT EXECUTE ON FUNCTION public.customer_exists(TEXT) TO anon, authenticated;
```

`ICustomerRepository.ExistsForEmailAsync` calls this RPC rather than a direct `SELECT`.

## 11. Open Questions, Risks & Assumptions

- **❓ Open question — Bilingual email template vs. Auth Hook with Resend.** MVP uses a single Supabase OTP template carrying both EN+FR text stacked. A cleaner long-term solution is the Supabase Send-Email Auth Hook calling a tiny Edge Function that uses Resend with per-locale templates. Confirm whether MVP-stacked is acceptable, or whether we should build the hook now.
- **❓ Open question — How is the user's preferred locale captured at sign-up?** Spec mandates EN+FR throughout but doesn't say how the user selects their language. Right now the assumption is "site-wide culture switcher already exists" — but it doesn't yet. Either (a) ship without a switcher and infer from `navigator.language`, or (b) add a small `LocalePicker` in the header as part of this feature.
- **❓ Open question — Email-existence disclosure.** Spec Edge Cases require explicit "Account already exists" / "No account found" messages, which discloses registration status (per the spec confirmation message in the prior turn). Re-confirm this is acceptable; if a future security review asks us to fall back to a generic "If an account exists, we sent a code" message, only the resx values + handler return keys change — the architecture is unaffected.
- **⚠️ Risk — Supabase OTP rate-limit + attempt-count are project-wide defaults, not per-feature.** Changing them affects every email-OTP flow we ever add (e.g. password-reset, email-change). Document the chosen values (10 min, 60s, 5 attempts) in `Infrastructure/README` so they aren't quietly relaxed later.
- **⚠️ Risk — Insert-after-VerifyOTP can fail (network blip, RLS misconfig) leaving an orphan `auth.users` row with no `customers` profile.** Mitigation: `VerifySignUpOtpHandler` rolls back by calling `IAuthService.SignOutAsync` and surfacing a generic error; a follow-up nightly job (out of scope here) could reconcile by deleting orphan `auth.users` rows older than 1 hour with no matching `customers` row.
- **⚠️ Risk — Sliding-refresh token failures while the tab is open look identical to "session expired" to the user.** Acceptable for MVP; surfacing as `Auth_SessionExpired`. Revisit if it generates support volume.
- **📌 Assumption — `Cart` will be server-persisted** (per the canonical example in `ARCHITECTURE.md`). The spec edge case "cart preserved on session expiry" therefore needs nothing from this feature; once the Cart feature lands, the new `AuthState` reload will refetch the cart automatically.
- **📌 Assumption — Session storage uses `Blazored.LocalStorage`** (already in `TheShop.Web.csproj`), wrapped as a Supabase `IGotrueSessionPersistence`. If a future requirement is "sign me out when I close the tab," swap the adapter to `sessionStorage`.
- **📌 Assumption — Admin users sign up through the same flow** and are promoted later via the SQL snippet in `ARCHITECTURE.md §Promoting a user to admin`. No admin-specific sign-up path is in scope.
- **📌 Assumption — A site-wide culture switcher will be built outside this feature.** Until it exists, the app uses the browser-default culture, and the bilingual email template means OTP delivery still satisfies AC-12.

---
**Status:** Draft · **Spec:** `.specs/authentication/spec.md` · **Created:** 2026-05-20
