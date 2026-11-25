using TheShop.Web.Common.Notifications;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using System;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.Commands.VerifySignInOtp;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Pages.Auth;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Pages.Auth;

/// <summary>
/// Tests for the <see cref="SignInVerify"/> page (sign-in step 2: code entry).
/// Covers FR-4, FR-7, AC-2, AC-6, AC-7, AC-8.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class SignInVerifyTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public SignInVerifyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);

        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
    }

    // =========================================================================
    // Guard: missing email parameter redirects to sign-in (behavior 4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void OnInitialized_WhenEmailParameterAbsent_RedirectsToSignIn()
    {
        var cut = Render<SignInVerify>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        navManager.Uri.Should().Contain(Routes.Auth.SignIn);
    }

    // =========================================================================
    // Happy path — correct code (AC-2)
    // =========================================================================

    private void NavigateToVerifyPage(string email = "user@example.com")
    {
        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"{Routes.Auth.SignInVerify}?email={Uri.EscapeDataString(email)}");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_WithEmailParameter_VerifyButtonIsInitiallyDisabled()
    {
        NavigateToVerifyPage();
        var cut = Render<SignInVerify>();

        cut.Find("[data-testid='otp-submit']").HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().NotContain("mud-");
        cut.Find("h1").TextContent.Should().Be(Strings.Verify_Heading);
    }

    // =========================================================================
    // Too many attempts → redirect back to email entry (AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_WithEmailParameter_DisplaysEmailAddress()
    {
        NavigateToVerifyPage();
        var cut = Render<SignInVerify>();

        cut.Markup.Should().Contain("user@example.com",
            "the verify page must display the email address the OTP was sent to");
    }

    // =========================================================================
    // Resend — handler wiring (AC-8, AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_WithValidEmail_ResendButtonIsDisabledDuringInitialCooldown()
    {
        // OnInitialized calls StartCooldown(60), so the resend button must be disabled
        // the moment the page renders (_resendCooldown > 0).
        NavigateToVerifyPage();
        var cut = Render<SignInVerify>();

        cut.Find("[data-testid='otp-countdown']").TextContent.Should().Be("01:00");
        cut.FindAll("[data-testid='otp-resend']").Should().BeEmpty();
    }

    // =========================================================================
    // ReturnUrl: after successful sign-in, user goes to return URL not home
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_WithEmailParameter_DoesNotRedirectToSignIn()
    {
        NavigateToVerifyPage();
        var cut = Render<SignInVerify>();

        var navManager = Services.GetRequiredService<NavigationManager>();

        // The URI must point to the verify page; it must NOT be the plain sign-in route.
        // Note: Routes.Auth.SignIn ("/sign-in") is a substring of the verify URL so we
        // assert on the positive presence of the verify path instead.
        navManager.Uri.Should().Contain(Routes.Auth.SignInVerify,
            "a valid email parameter must keep the user on the verify page, not redirect to sign-in");
    }

    [Fact]
    public async Task Submit_IncompleteCode_DoesNotDispatch()
    {
        NavigateToVerifyPage();
        var cut = Render<SignInVerify>();
        cut.Find("input").Input("012");

        await cut.Find("form").SubmitAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<VerifySignInOtpCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_LeadingZeroAndReturnUrl_DispatchesOnceAndNavigates()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(Routes.Auth.SignInVerifyWith("user@example.com", Routes.Products));
        _mediator.Send(Arg.Any<VerifySignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<SessionDto>(null!));
        var cut = Render<SignInVerify>();
        cut.Find("[data-testid='otp-change']").GetAttribute("href")
            .Should().Be(Routes.Auth.SignInWithReturn(Routes.Products));
        cut.Find("input").Input("012345");

        await cut.Find("form").SubmitAsync();

        await _mediator.Received(1).Send(new VerifySignInOtpCommand("user@example.com", "012345"), Arg.Any<CancellationToken>());
        nav.Uri.Should().EndWith(Routes.Products);
        _notifications.Received().Show(Strings.Auth_SignedIn, ShopNotificationKind.Success);
    }

    [Fact]
    public async Task Submit_InFlight_FreezesInputsAndRejectsDuplicateThenClearsFailedCode()
    {
        NavigateToVerifyPage();
        var completion = new TaskCompletionSource<Result<SessionDto>>();
        _mediator.Send(Arg.Any<VerifySignInOtpCommand>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var cut = Render<SignInVerify>();
        cut.Find("input").Input("012345");

        var submit = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => cut.FindAll("input").Should().OnlyContain(input => input.HasAttribute("disabled")));
        cut.Find("[data-testid='otp-submit']").GetAttribute("aria-busy").Should().Be("true");
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Any<VerifySignInOtpCommand>(), Arg.Any<CancellationToken>());
        completion.SetResult(Result.Fail<SessionDto>(nameof(Strings.Auth_Unexpected)));
        await submit;

        cut.FindAll("input").Should().OnlyContain(input => !input.HasAttribute("disabled"));
        cut.FindAll("input").Should().OnlyContain(input => string.IsNullOrEmpty(input.GetAttribute("value")));
        cut.Find("[data-testid='otp-submit']").HasAttribute("disabled").Should().BeTrue();
        _notifications.Received().Show(nameof(Strings.Auth_Unexpected), ShopNotificationKind.Error);
    }

    [Fact]
    public async Task Submit_TooManyAttempts_RedirectsToEmailEntry()
    {
        NavigateToVerifyPage();
        _mediator.Send(Arg.Any<VerifySignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<SessionDto>(nameof(Strings.Auth_TooManyAttempts)));
        var cut = Render<SignInVerify>();
        cut.Find("input").Input("012345");

        await cut.Find("form").SubmitAsync();

        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Auth.SignIn);
    }
}
