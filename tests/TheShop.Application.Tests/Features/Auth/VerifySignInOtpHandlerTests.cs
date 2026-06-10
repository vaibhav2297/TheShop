using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.VerifySignInOtp;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="VerifySignInOtpHandler"/>.
/// Covers FR-4, AC-2, AC-6, AC-7.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class VerifySignInOtpHandlerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();

    private VerifySignInOtpHandler CreateSut() => new(_auth, _customers);

    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly AuthSession FakeSession = new(
        UserId,
        "returning@example.com",
        "access-token",
        "refresh-token",
        DateTimeOffset.UtcNow.AddHours(1));

    private static Customer BuildCustomer() =>
        Customer.Rehydrate(
            UserId,
            "Jane",
            "Doe",
            DateOfBirth.Create(new DateOnly(2000, 1, 1)),
            Email.Create("returning@example.com"),
            DateTimeOffset.UtcNow.AddDays(-30));

    // =========================================================================
    // Happy path (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithValidOtp_ReturnsSuccessSessionDtoWithCustomerProfile()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));
        _customers.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns(BuildCustomer());

        var result = await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "654321"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(UserId);
        result.Value.Customer.FirstName.Should().Be("Jane");
        result.Value.Customer.LastName.Should().Be("Doe");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithValidOtp_DoesNotCallSignOut()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));
        _customers.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns(BuildCustomer());

        await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "654321"),
            CancellationToken.None);

        await _auth.DidNotReceive().SignOutAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // OTP failures (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenOtpIsIncorrect_ReturnsCodeIncorrectFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.CodeIncorrect));

        var result = await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "000000"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.CodeIncorrect);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenOtpIsExpired_ReturnsCodeExpiredFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.CodeExpired));

        var result = await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "123456"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.CodeExpired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenTooManyFailedAttempts_ReturnsTooManyAttemptsFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.TooManyAttempts));

        var result = await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "999999"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.TooManyAttempts);
    }

    // =========================================================================
    // Missing customer record
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenCustomerRecordMissingAfterOtpVerified_ReturnsAccountNotFoundFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));
        _customers.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns((Customer?)null);

        var result = await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "654321"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountNotFound);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenCustomerRecordMissingAfterOtpVerified_RevokesSession()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));
        _customers.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns((Customer?)null);

        await CreateSut().Handle(
            new VerifySignInOtpCommand("returning@example.com", "654321"),
            CancellationToken.None);

        await _auth.Received(1).SignOutAsync(Arg.Any<CancellationToken>());
    }
}
