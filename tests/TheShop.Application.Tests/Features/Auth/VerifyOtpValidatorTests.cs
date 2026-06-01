using FluentAssertions;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.VerifySignInOtp;
using TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="VerifySignUpOtpCommandValidator"/> and
/// <see cref="VerifySignInOtpCommandValidator"/>.
/// Validates the six-digit numeric code format and email rules (FR-4, constraints §3).
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class VerifySignUpOtpValidatorTests
{
    private readonly VerifySignUpOtpCommandValidator _validator = new();

    private static readonly DateOnly ValidDob = new(2000, 1, 1);

    private static VerifySignUpOtpCommand Build(
        string email = "jane@example.com",
        string code = "123456",
        string firstName = "Jane",
        string lastName = "Doe") =>
        new(email, code, firstName, lastName, ValidDob);

    // =========================================================================
    // Code format — exactly 6 numeric digits (spec constraint §3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenCodeIsEmpty_HasCodeInvalidError()
    {
        var cmd = Build(code: "");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Code) &&
            e.ErrorMessage == AuthErrorKeys.CodeInvalid);
    }

    [Theory]
    [InlineData("12345")]     // 5 digits — too short
    [InlineData("1234567")]   // 7 digits — too long
    [InlineData("12345A")]    // non-numeric character
    [InlineData("abc123")]    // mixed alpha
    [InlineData("      ")]    // spaces
    [Trait("Feature", "authentication")]
    public void Validate_WhenCodeIsNotExactlySixDigits_HasCodeInvalidError(string code)
    {
        var cmd = Build(code: code);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Code) &&
            e.ErrorMessage == AuthErrorKeys.CodeInvalid);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenCodeIsExactlySixDigits_HasNoCodeError()
    {
        var cmd = Build(code: "000000");
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Code));
    }

    // =========================================================================
    // Email
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsEmpty_HasEmailRequiredError()
    {
        var cmd = Build(email: "");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailRequired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsMalformed_HasEmailInvalidError()
    {
        var cmd = Build(email: "notanemail");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailInvalid);
    }

    // =========================================================================
    // First / last name
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenFirstNameIsEmpty_HasFirstNameRequiredError()
    {
        var cmd = Build(firstName: "");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.FirstName) &&
            e.ErrorMessage == AuthErrorKeys.FirstNameRequired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenLastNameIsEmpty_HasLastNameRequiredError()
    {
        var cmd = Build(lastName: "");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.LastName) &&
            e.ErrorMessage == AuthErrorKeys.LastNameRequired);
    }

    // =========================================================================
    // All valid
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WithAllValidInputs_IsValid()
    {
        _validator.Validate(Build()).IsValid.Should().BeTrue();
    }
}

public class VerifySignInOtpValidatorTests
{
    private readonly VerifySignInOtpCommandValidator _validator = new();

    private static VerifySignInOtpCommand Build(
        string email = "returning@example.com",
        string code = "654321") => new(email, code);

    // =========================================================================
    // Code format
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenCodeIsEmpty_HasCodeInvalidError()
    {
        var cmd = Build(code: "");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Code) &&
            e.ErrorMessage == AuthErrorKeys.CodeInvalid);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345A")]
    [InlineData("abc123")]
    [Trait("Feature", "authentication")]
    public void Validate_WhenCodeIsNotExactlySixDigits_HasCodeInvalidError(string code)
    {
        var cmd = Build(code: code);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Code) &&
            e.ErrorMessage == AuthErrorKeys.CodeInvalid);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenCodeIsExactlySixDigits_HasNoCodeError()
    {
        var cmd = Build(code: "999999");
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Code));
    }

    // =========================================================================
    // Email
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsEmpty_HasEmailRequiredError()
    {
        var cmd = Build(email: "");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailRequired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsMalformed_HasEmailInvalidError()
    {
        var cmd = Build(email: "badformat");
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailInvalid);
    }

    // =========================================================================
    // All valid
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WithAllValidInputs_IsValid()
    {
        _validator.Validate(Build()).IsValid.Should().BeTrue();
    }
}
