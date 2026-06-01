using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="ResendOtpHandler"/>.
/// Covers FR-7, AC-8, AC-9 and the 60-second cooldown enforcement.
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class ResendOtpHandlerTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private ResendOtpHandler CreateSut() => new(_customers, _auth);

    // =========================================================================
    // Happy path — sign-up resend
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignUpResend_WhenEmailNotRegistered_ReturnsSuccessWithCooldown()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);
        _auth.SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        var result = await CreateSut().Handle(
            new ResendOtpCommand("new@example.com", OtpPurpose.SignUp),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ResendCooldownSeconds.Should().Be(60);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignUpResend_WhenEmailNotRegistered_SendsSignUpOtp()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);
        _auth.SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        await CreateSut().Handle(
            new ResendOtpCommand("new@example.com", OtpPurpose.SignUp),
            CancellationToken.None);

        await _auth.Received(1).SendSignUpOtpAsync("new@example.com", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Happy path — sign-in resend
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignInResend_WhenEmailIsRegistered_ReturnsSuccessWithCooldown()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);
        _auth.SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        var result = await CreateSut().Handle(
            new ResendOtpCommand("returning@example.com", OtpPurpose.SignIn),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ResendCooldownSeconds.Should().Be(60);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignInResend_WhenEmailIsRegistered_SendsSignInOtp()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);
        _auth.SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        await CreateSut().Handle(
            new ResendOtpCommand("returning@example.com", OtpPurpose.SignIn),
            CancellationToken.None);

        await _auth.Received(1).SendSignInOtpAsync("returning@example.com", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Account-state conflicts
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignUpResend_WhenEmailAlreadyRegistered_ReturnsAccountAlreadyExistsFailure()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);

        var result = await CreateSut().Handle(
            new ResendOtpCommand("taken@example.com", OtpPurpose.SignUp),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountAlreadyExists);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignInResend_WhenEmailNotRegistered_ReturnsAccountNotFoundFailure()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);

        var result = await CreateSut().Handle(
            new ResendOtpCommand("ghost@example.com", OtpPurpose.SignIn),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountNotFound);
    }

    // =========================================================================
    // Auth service rate-limit / resend-too-soon propagation (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenAuthServiceReturnsResendTooSoon_PropagatesResendTooSoonError()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);
        _auth.SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail(AuthErrorKeys.ResendTooSoon));

        var result = await CreateSut().Handle(
            new ResendOtpCommand("new@example.com", OtpPurpose.SignUp),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.ResendTooSoon);
    }

    // =========================================================================
    // No OTP sent when conflict detected (AC-9 — previous code must be invalidated
    // by the next successful send; if send is blocked, nothing changes)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_SignUpResend_WhenAccountConflict_DoesNotCallAuthService()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);

        await CreateSut().Handle(
            new ResendOtpCommand("taken@example.com", OtpPurpose.SignUp),
            CancellationToken.None);

        await _auth.DidNotReceive().SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _auth.DidNotReceive().SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
