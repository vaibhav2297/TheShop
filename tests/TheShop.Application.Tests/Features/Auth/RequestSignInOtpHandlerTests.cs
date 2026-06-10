using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="RequestSignInOtpHandler"/>.
/// Covers FR-2, FR-3 and the no-account edge case (AC-5).
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class RequestSignInOtpHandlerTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private RequestSignInOtpHandler CreateSut() => new(_customers, _auth);

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithExistingEmail_ReturnsSuccessResultWithEmailAndCooldown()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);
        _auth.SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        var result = await CreateSut().Handle(
            new RequestSignInOtpCommand("existing@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("existing@example.com");
        result.Value.ResendCooldownSeconds.Should().Be(60);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithExistingEmail_SendsSignInOtpToAuthService()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);
        _auth.SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        await CreateSut().Handle(
            new RequestSignInOtpCommand("existing@example.com"), CancellationToken.None);

        await _auth.Received(1).SendSignInOtpAsync("existing@example.com", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Account-not-found guard (AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenEmailHasNoAccount_ReturnsAccountNotFoundFailure()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);

        var result = await CreateSut().Handle(
            new RequestSignInOtpCommand("unknown@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountNotFound);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenEmailHasNoAccount_DoesNotSendOtp()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);

        await CreateSut().Handle(
            new RequestSignInOtpCommand("unknown@example.com"), CancellationToken.None);

        await _auth.DidNotReceive().SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Auth service failure propagation
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenAuthServiceFails_PropagatesErrorKey()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);
        _auth.SendSignInOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail(AuthErrorKeys.Network));

        var result = await CreateSut().Handle(
            new RequestSignInOtpCommand("existing@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.Network);
    }
}
