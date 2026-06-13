using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="VerifySignUpOtpHandler"/>.
/// Covers FR-4, FR-5, AC-1, AC-3, AC-6, AC-7.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class VerifySignUpOtpHandlerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();

    private VerifySignUpOtpHandler CreateSut() => new(_auth, _customers);

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly ValidDob = new(2000, 1, 15);

    private static readonly AuthSession FakeSession = new(
        UserId,
        "jane@example.com",
        "access-token",
        "refresh-token",
        DateTimeOffset.UtcNow.AddHours(1));

    private static VerifySignUpOtpCommand ValidCommand(
        string email = "jane@example.com",
        string code = "123456",
        string firstName = "Jane",
        string lastName = "Doe",
        DateOnly? dob = null) =>
        new(email, code, firstName, lastName, dob ?? ValidDob);

    // =========================================================================
    // Happy path (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithValidOtpAndProfile_ReturnsSuccessSessionDto()
    {
        // Arrange
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));
        _customers.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
                  .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(UserId);
        result.Value.Email.Should().Be("jane@example.com");
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.Customer.FirstName.Should().Be("Jane");
        result.Value.Customer.LastName.Should().Be("Doe");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WithValidOtpAndProfile_PersistsNewCustomer()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));

        await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        await _customers.Received(1).AddAsync(
            Arg.Is<Customer>(c => c.Id == UserId),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // OTP verification failures (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenOtpIsIncorrect_ReturnsCodeIncorrectFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.CodeIncorrect));

        var result = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.CodeIncorrect);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenOtpIsExpired_ReturnsCodeExpiredFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.CodeInvalidOrExpired));

        var result = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.CodeInvalidOrExpired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenTooManyFailedAttempts_ReturnsTooManyAttemptsFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.TooManyAttempts));

        var result = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.TooManyAttempts);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenOtpVerificationFails_DoesNotPersistCustomer()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Fail<AuthSession>(AuthErrorKeys.CodeIncorrect));

        await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        await _customers.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Age / domain invariant failures after OTP success (AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenCustomerIsUnder19AfterOtpVerified_ReturnsUnderageFailure()
    {
        // OTP verified OK, but the DOB makes the user under 19
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));

        var underAgeDob = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-18));
        var cmd = ValidCommand(dob: underAgeDob);

        var result = await CreateSut().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.Underage);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenCustomerIsUnder19AfterOtpVerified_RevokesSession()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));

        var underAgeDob = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-18));
        await CreateSut().Handle(ValidCommand(dob: underAgeDob), CancellationToken.None);

        await _auth.Received(1).SignOutAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenDobIsToday_ReturnsDobInPastFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));

        var todayDob = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var result = await CreateSut().Handle(ValidCommand(dob: todayDob), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.DobInPast);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenDomainExceptionOccursAfterOtpVerified_RevokesSession()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));

        // Empty first name triggers a DomainException after OTP is verified
        var result = await CreateSut().Handle(
            ValidCommand(firstName: ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _auth.Received(1).SignOutAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Repository failure
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_WhenRepositoryThrows_RevokesSessionAndReturnsUnexpectedFailure()
    {
        _auth.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Result.Ok(FakeSession));
        _customers.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
                  .Returns(_ => throw new Exception("Database unavailable"));

        var result = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.Unexpected);
        await _auth.Received(1).SignOutAsync(Arg.Any<CancellationToken>());
    }
}
