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
using TheShop.Application;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Auth;
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
    private readonly IRequestHandler<RequestSignInOtpCommand, Result<OtpRequestedDto>> _handler =
        Substitute.For<IRequestHandler<RequestSignInOtpCommand, Result<OtpRequestedDto>>>();

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
        _localizer[AuthErrorKeys.EmailRequired].Returns(new LocalizedString(AuthErrorKeys.EmailRequired, Strings.Email_Required));
        _localizer[AuthErrorKeys.EmailInvalid].Returns(new LocalizedString(AuthErrorKeys.EmailInvalid, Strings.Email_Invalid));

        _handler.Handle(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new OtpRequestedDto(call.Arg<RequestSignInOtpCommand>().Email, 60)));
    }

    private void UseCommandValidation()
    {
        new ServiceCollection().AddApplication();
        var validation = new ValidationBehavior<RequestSignInOtpCommand, Result<OtpRequestedDto>>(
            [new RequestSignInOtpCommandValidator()]);
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => validation.Handle(call.Arg<RequestSignInOtpCommand>(),
                token => _handler.Handle(call.Arg<RequestSignInOtpCommand>(), token), call.Arg<CancellationToken>()));
    }

    // =========================================================================
    // Happy path — successful send (AC-2, FR-2)
    // =========================================================================

    [Fact]
    public void Render_TextField_KeepsAssociatedFloatingLabelAndNativeInputContract()
    {
        var cut = Render<SignIn>();
        var input = cut.Find("#signin-email");
        var control = cut.Find(".shop-field-control");
        var label = control.QuerySelector("label")!;

        label.GetAttribute("for").Should().Be(input.Id);
        label.TextContent.Should().Be(Strings.Email_Label);
        label.ClassList.Should().Contain("shop-field-label");
        input.GetAttribute("placeholder").Should().Be(" ");
        input.GetAttribute("autocomplete").Should().Be("email");
        input.GetAttribute("aria-describedby").Should().Be("signin-instruction signin-email-error");
        input.HasAttribute("required").Should().BeTrue();
        cut.FindAll("label[for='signin-email']").Should().HaveCount(1);
        input.HasAttribute("variant").Should().BeFalse();
        input.Input("native-ui@example.invalid");
        cut.Find("#signin-email").GetAttribute("value").Should().Be("native-ui@example.invalid");
        cut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse();
    }

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
    [InlineData("a@b")]
    [InlineData("@")]
    [Trait("Feature", "authentication")]
    public async Task Submit_WithMissingOrInvalidEmail_ShowsCommandValidationWithoutCallingHandler(string email)
    {
        UseCommandValidation();
        var cut = Render<SignIn>();
        cut.Find("input[type='email']").Input(email);

        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("#signin-email-error").TextContent.Should().BeNullOrWhiteSpace();
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        await _mediator.Received(1).Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
        await _handler.DidNotReceive().Handle(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<ShopNotificationKind>());
        var error = string.IsNullOrWhiteSpace(email) ? Strings.Email_Required : Strings.Email_Invalid;
        cut.Find("#signin-email-error").TextContent.Should().Contain(error);
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
        UseCommandValidation();
        var cut = Render<SignIn>();
        cut.Find("input[type='email']").Input("invalid");
        await cut.Find("form").SubmitAsync(EventArgs.Empty);
        cut.Find("#signin-email-error").TextContent.Should().Contain(Strings.Email_Invalid);

        cut.Find("input[type='email']").Input("still-invalid");
        cut.Find("#signin-email-error").TextContent.Should().BeNullOrWhiteSpace();
        await _mediator.Received(1).Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());

        cut.Find("input[type='email']").Input("user@example.com");

        cut.Find("#signin-email-error").TextContent.Should().BeNullOrWhiteSpace();
        // InputBase removes aria-invalid after recovery; omission means the field is not invalid.
        cut.Find("input[type='email']").GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("form").SubmitAsync(EventArgs.Empty);
        await _mediator.Received(2).Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
        await _handler.Received(1).Handle(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_RepeatedValidationFailures_ReplacesMessagesAndDisplaysEveryFieldError()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OtpRequestedDto>(AuthErrorKeys.EmailRequired, [],
            [
                new(nameof(RequestSignInOtpCommand.Email), AuthErrorKeys.EmailRequired),
                new(nameof(RequestSignInOtpCommand.Email), AuthErrorKeys.EmailInvalid)
            ]));
        var cut = Render<SignIn>();

        await cut.Find("form").SubmitAsync(EventArgs.Empty);
        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        var messages = cut.FindAll("#signin-email-error .validation-message");
        messages.Select(message => message.TextContent).Should().Equal(Strings.Email_Required, Strings.Email_Invalid);
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<ShopNotificationKind>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("UnmappedProperty")]
    public async Task Submit_ValidationErrorWithoutAVisibleField_ShowsFormSummary(string propertyName)
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OtpRequestedDto>(AuthErrorKeys.EmailInvalid, [],
                [new(propertyName, AuthErrorKeys.EmailInvalid)]));
        var cut = Render<SignIn>();

        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        cut.Find(".shop-command-validation").TextContent.Should().Contain(Strings.Email_Invalid);
        cut.Find(".shop-command-validation").GetAttribute("aria-live").Should().Be("polite");
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<ShopNotificationKind>());
        cut.Find("#signin-email-error").TextContent.Should().BeNullOrWhiteSpace();
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
        input.GetAttribute("aria-describedby")!.Split(' ').Should().Contain("signin-email-error");
        input.GetAttribute("autocomplete").Should().Be("email");
        input.HasAttribute("required").Should().BeTrue();
        // Valid InputBase controls may omit aria-invalid instead of rendering an explicit false.
        input.GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("#signin-email-error").GetAttribute("aria-live").Should().Be("polite");
        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeFalse();
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
