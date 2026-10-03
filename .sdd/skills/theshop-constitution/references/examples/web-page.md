# Example — Native Web page

Use the existing `src/TheShop.Web/Pages/Auth/SignIn.razor`, `SignIn.razor.cs`, and `SignInFormModel.cs` as the current worked example. Do not invent a product-details route or cart implementation from historical sample code.

## Markup contract

The page uses semantic `section`, `header`, `h1`, `p`, and `nav` elements. Its sign-up link targets `Routes.Auth.SignUp`; the native button submits an `EditForm`. All labels, instructions, and status text come from `Strings`.

The following excerpt uses the actual page's members and controls; it is not a standalone component:

```razor
<EditForm EditContext="@_editContext" OnSubmit="OnSendCodeAsync" novalidate>
    <BusyFor Key="@BusyKeys.Auth.SignIn" Context="busy">
        <ShopTextField id="signin-email" type="email" name="email"
                       @bind-Value="_model.Email"
                       Label="@Strings.Email_Label"
                       StartIcon="@ShopIcons.Outlined.Mention"
                       autocomplete="email" required Disabled="@busy"
                       aria-describedby="signin-instruction" />
        <ShopButton Type="submit" Disabled="@(!CanSubmit)" Loading="@busy">
            @Strings.Auth_Login_Submit
        </ShopButton>
    </BusyFor>
</EditForm>
```

The full page also supplies field instructions, a decorative icon, and its loading indicator. Copy the complete relevant contract, not only this shortened excerpt.

`ShopTextField` derives from `InputBase<string?>` and updates on `oninput`. It owns the outlined shell, associated floating label, optional icon/helper, and validation-message region. It has no visible placeholder or variant API; its internal blank placeholder only supports CSS empty-state detection. The page owns validation rules and busy state. Native attributes target the input; external descriptions merge with generated helper/error IDs (`signin-email-error` here). This preserves immediate email validation, `EditContext`, field notifications, and parsing contracts without repeating field markup on each page.

## Code-behind contract

Read `SignIn.razor.cs` for the complete implementation:

- `[Route(Routes.Auth.SignIn)]` declares the route; markup has no literal `@page` path.
- `OnInitialized` creates the form's `EditContext` and validation-message store, and subscribes to field changes.
- `ValidateEmail` publishes resource-backed field errors. `OnSendCodeAsync` guards duplicate busy submissions and validates before dispatch.
- The handler captures the trimmed email, calls `BusyState.RunAsync(BusyKeys.Auth.SignIn, ...)`, and sends `RequestSignInOtpCommand` through `IMediator`.
- Success navigates using `Routes.Auth.SignInVerifyWith(email, ReturnUrl)`. Runtime error keys use `Localizer`; static text uses typed `Strings` accessors.
- `Dispose` removes the field-change subscription.

Do not introduce a second `_isBusy` state or call repositories from the page. Keep Application behavior and existing result/error contracts unchanged during markup migration.

## Transitional notification bridge

The current SignIn implementation still injects `ISnackbar`, and its active legacy layout supplies the snackbar provider. This is an explicit temporary bridge until the project-owned notification service/host is implemented and verified. It is not the final native architecture and must not become a reason to retain MudBlazor after the last consumer migrates. The native form itself must not depend on vendor CSS or form controls.

## Style and verification references

Page geometry belongs in `Styles/layouts/_auth.scss`; shared fields, buttons, typography, and tokens remain in their respective owners. Classes use single-hyphen `shop-*` names, and CSS variables use the mandatory `--shop-*` prefix. Static styles do not belong in Razor.

`tests/TheShop.Web.Tests/Pages/Auth/SignInTests.cs` exercises native input/submission, validation, trimming, return URLs, busy guards, and recovery. Browser verification also checks typing without blur, keyboard navigation, responsive layout, and visible focus; its input-only journeys do not submit an OTP request. Tests must not set private validity fields to bypass the actual form.
