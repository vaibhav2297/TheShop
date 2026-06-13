using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="RequestSignUpOtpHandler"/>.
/// Covers FR-1, FR-3, FR-6 and the duplicate-email edge case.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class RequestSignUpOtpHandlerTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private RequestSignUpOtpHandler CreateSut() => new(_customers, _auth);

    private static RequestSignUpOtpCommand ValidCommand(string email = "new@example.com") =>
        new("Jane", "Doe", email, new DateOnly(2000, 1, 1));

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithNewEmail_ReturnsSuccessResultWithEmailAndCooldown()
    {
        // Arrange
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);
        _auth.SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new@example.com");
        result.Value.ResendCooldownSeconds.Should().Be(60);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithNewEmail_SendsSignUpOtpToAuthService()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);
        _auth.SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok());

        await CreateSut().Handle(ValidCommand("send@example.com"), CancellationToken.None);

        await _auth.Received(1).SendSignUpOtpAsync("send@example.com", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Duplicate-email guard (FR-6, AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenEmailAlreadyRegistered_ReturnsAccountAlreadyExistsFailure()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);

        var result = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountAlreadyExists);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenEmailAlreadyRegistered_DoesNotSendOtp()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(true);

        await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        await _auth.DidNotReceive().SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Auth service failure propagation
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenAuthServiceFails_PropagatesErrorKey()
    {
        _customers.ExistsForEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(false);
        _auth.SendSignUpOtpAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail(AuthErrorKeys.Network));

        var result = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.Network);
    }
}
