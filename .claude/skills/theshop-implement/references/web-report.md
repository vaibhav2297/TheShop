### 8. Report the produced surface

End your response with this structured summary:

```
## Web implementation summary — {feature_name}

**Plan sections read:** 6, 7 (Phase 4), 9 of `.specs/{feature_name}/plan.md`

**Figma sources fetched:**
- {Figma file URL}
- Node `123:456` — "Sign-in form (sign-in page)"
- Node `123:457` — "OTP verification step"

**Files created/modified:**
- `src/TheShop.Web/Pages/Auth/SignIn.razor` + `.razor.cs` (new)
- `src/TheShop.Web/Pages/Auth/VerifyOtp.razor` + `.razor.cs` (new)
- `src/TheShop.Web/Components/Auth/OtpInput.razor` + `.razor.cs` (new — inherits `MudComponentBase`, forwards `Class`/`Style`)
- `src/TheShop.Web/State/AuthState.cs` (modified)
- `src/TheShop.Web/Common/Routes.cs` (3 new constants)
- `src/TheShop.Web/Common/BusyKeys.cs` (2 new keys)
- `src/TheShop.Web/Resources/Strings.resx` (12 keys added)
- `src/TheShop.Web/Resources/Strings.fr.resx` (12 keys added with [TODO])
- `src/TheShop.Web/Styles/abstracts/_typography.scss` (added `fs-22` to `$font-sizes` list — used by OTP heading)
- `src/TheShop.Web/Styles/components/_otp.scss` (new — OTP digit-cell layout, reusable)

**Routes added:**
- `Routes.Auth.SignIn = "/sign-in"`
- `Routes.Auth.VerifyOtp = "/sign-in/verify"`
- `Routes.Auth.SignOut = "/sign-out"`

**Visual validation:**
- ✅ SignIn page matches Figma node 123:456 (3 iterations, parity achieved)
- ⚠️ VerifyOtp page — OTP input field spacing is 4px tighter than Figma; corrected via `Class="gap-2"` instead of default. Final pass matches.

**Build status:** ✅ `dotnet build TheShop.Web` succeeded with 0 warnings / 0 errors.

**Open questions / TODOs:**
- {Anything that needs a design decision the plan didn't make. If none, write "None."}
```

---
