using TheShop.Web.Common.Notifications;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Pages.Auth;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Pages.Auth;

/// <summary>
/// Tests for the <see cref="SignIn"/> page (sign-in step 1: email entry).
/// Covers FR-2, FR-3, AC-2, AC-5 at the UI layer.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class SignInTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public SignInTests()
    {
        // BusyState is a concrete class with no constructor params — use the real one.
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);

        // Localize any error key to itself so assertions stay key-based.
        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
    }

    // =========================================================================
    // Happy path — successful send (AC-2, FR-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenMediatorReturnsSuccess_ShowsSuccessSnackbarAndNavigates()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new OtpRequestedDto("user@example.com", 60)));

        var cut = Render<SignIn>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("input[type='email']").Input("user@example.com");
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        _notifications.Received().Show(
            Arg.Is<string>(s => s == Strings.Auth_CodeSent),
            ShopNotificationKind.Success);
        navManager.Uri.Should().Contain(Routes.Auth.SignInVerify);
    }

    // =========================================================================
    // Account not found (AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenAccountNotFound_ShowsErrorSnackbar()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<OtpRequestedDto>("Auth_AccountNotFound"));

        var cut = Render<SignIn>();

        cut.Find("input[type='email']").Input("ghost@example.com");
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        _notifications.Received().Show(
            Arg.Any<string>(),
            ShopNotificationKind.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [Trait("Feature", "authentication")]
    public async Task Submit_WithMissingOrInvalidEmail_ShowsValidationWithoutSendingACommand(string email)
    {
        var cut = Render<SignIn>();
        cut.Find("input[type='email']").Input(email);

        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        await _mediator.DidNotReceive().Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
        var error = string.IsNullOrWhiteSpace(email) ? Strings.Email_Required : Strings.Email_Invalid;
        cut.Find("#signin-error").TextContent.Should().Contain(error);
        cut.Find("input[type='email']").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("input[type='email']").GetAttribute("value").Should().Be(email);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Submit_WithSurroundingEmailWhitespace_SendsAndNavigatesWithTrimmedEmail()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new OtpRequestedDto("user@example.com", 60)));
        var cut = Render<SignIn>();

        cut.Find("input[type='email']").Input("  user@example.com  ");
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        await _mediator.Received(1).Send(
            Arg.Is<RequestSignInOtpCommand>(command => command.Email == "user@example.com"),
            Arg.Any<CancellationToken>());
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.Uri.Should().Be(nav.ToAbsoluteUri(Routes.Auth.SignInVerifyWith("user@example.com")).ToString());
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Submit_WithAReturnUrl_ForwardsItToVerification()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new OtpRequestedDto("user@example.com", 60)));
        var nav = Services.GetRequiredService<NavigationManager>();
        const string returnUrl = "/products?sort=price&direction=asc";
        nav.NavigateTo(Routes.Auth.SignInWithReturn(returnUrl));
        var cut = Render<SignIn>();

        cut.Find("input[type='email']").Input("user@example.com");
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        nav.Uri.Should().Be(nav.ToAbsoluteUri(Routes.Auth.SignInVerifyWith("user@example.com", returnUrl)).ToString());
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Submit_WhileARequestIsPending_DisablesControlsAndIgnoresDuplicateSubmissions()
    {
        var pending = new TaskCompletionSource<Result<OtpRequestedDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = Render<SignIn>();
        cut.Find("input[type='email']").Input("user@example.com");

        var submission = cut.Find("form").SubmitAsync(EventArgs.Empty);
        try
        {
            cut.WaitForAssertion(() =>
            {
                cut.Find("input[type='email']").HasAttribute("disabled").Should().BeTrue();
                cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
                cut.Find("button[type='submit']").GetAttribute("aria-busy").Should().Be("true");
                cut.Find(".shop-auth-actions [role='status']").ClassList.Should().Contain("shop-visually-hidden");
                cut.Find(".shop-auth-actions [role='status']").TextContent.Should().Be(Strings.Loading);
                cut.Find("button[type='submit'] .shop-spinner").GetAttribute("aria-hidden").Should().Be("true");
            });

            await cut.Find("form").SubmitAsync(EventArgs.Empty);
            _ = _mediator.Received(1).Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            pending.TrySetResult(Result.Fail<OtpRequestedDto>(nameof(Strings.Auth_AccountNotFound)));
            await submission;
        }

        cut.WaitForAssertion(() =>
        {
            cut.Find("input[type='email']").HasAttribute("disabled").Should().BeFalse();
            cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeFalse();
            cut.Find(".shop-auth-actions [role='status']").TextContent.Should().BeEmpty();
        });
        Services.GetRequiredService<BusyState>().IsBusy(BusyKeys.Auth.SignIn).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Input_AfterValidationFailure_ClearsTheErrorAndAllowsSubmissionImmediately()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new OtpRequestedDto("user@example.com", 60)));
        var cut = Render<SignIn>();
        cut.Find("input[type='email']").Input("invalid");
        await cut.Find("form").SubmitAsync(EventArgs.Empty);
        cut.Find("#signin-error").TextContent.Should().Contain(Strings.Email_Invalid);

        cut.Find("input[type='email']").Input("user@example.com");

        cut.Find("#signin-error").TextContent.Should().BeNullOrWhiteSpace();
        // InputBase removes aria-invalid after recovery; omission means the field is not invalid.
        cut.Find("input[type='email']").GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("form").SubmitAsync(EventArgs.Empty);
        await _mediator.Received(1).Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Render — page loads correctly
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_Always_ContainsEmailInput()
    {
        var cut = Render<SignIn>();
        var input = cut.Find("input[type='email']");
        input.Should().NotBeNull();
        cut.Find($"label[for='{input.Id}']").TextContent.Should().Be(Strings.Email_Label);
        input.GetAttribute("aria-describedby")!.Split(' ').Should().Contain("signin-error");
        input.GetAttribute("autocomplete").Should().Be("email");
        input.HasAttribute("required").Should().BeTrue();
        // Valid InputBase controls may omit aria-invalid instead of rendering an explicit false.
        input.GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("#signin-error").GetAttribute("aria-live").Should().Be("polite");
        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_Always_ContainsLinkToSignUpPage()
    {
        var cut = Render<SignIn>();
        var link = cut.Find($"a[href='{Routes.Auth.SignUp}']");
        link.Should().NotBeNull();
    }
}
