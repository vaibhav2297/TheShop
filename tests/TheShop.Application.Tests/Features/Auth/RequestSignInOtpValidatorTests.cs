using FluentAssertions;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="RequestSignInOtpCommandValidator"/>.
/// Covers email validation for sign-in (FR-2).
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class RequestSignInOtpValidatorTests
{
    private readonly RequestSignInOtpCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsEmpty_HasEmailRequiredError()
    {
        var cmd = new RequestSignInOtpCommand("");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailRequired);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@tld")]
    [InlineData("@nodomain.com")]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsMalformed_HasEmailInvalidError(string email)
    {
        var cmd = new RequestSignInOtpCommand(email);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailInvalid);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WithValidEmail_IsValid()
    {
        var cmd = new RequestSignInOtpCommand("returning@example.com");
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}
