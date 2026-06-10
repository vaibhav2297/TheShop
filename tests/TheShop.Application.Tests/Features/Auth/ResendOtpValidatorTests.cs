using FluentAssertions;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="ResendOtpCommandValidator"/>.
/// Covers email validation and enum guard for the resend flow (FR-7, AC-8).
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class ResendOtpValidatorTests
{
    private readonly ResendOtpCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsEmpty_HasEmailRequiredError()
    {
        var cmd = new ResendOtpCommand("", OtpPurpose.SignIn);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailRequired);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@nodomain.com")]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsMalformed_HasEmailInvalidError(string email)
    {
        var cmd = new ResendOtpCommand(email, OtpPurpose.SignIn);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailInvalid);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenPurposeIsInvalidEnumValue_HasPurposeError()
    {
        var cmd = new ResendOtpCommand("a@b.com", (OtpPurpose)99);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Purpose));
    }

    [Theory]
    [InlineData(OtpPurpose.SignUp)]
    [InlineData(OtpPurpose.SignIn)]
    [Trait("Feature", "authentication")]
    public void Validate_WithValidEmailAndPurpose_IsValid(OtpPurpose purpose)
    {
        var cmd = new ResendOtpCommand("user@example.com", purpose);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}
