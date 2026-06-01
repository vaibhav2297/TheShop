using FluentAssertions;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="RequestSignUpOtpCommandValidator"/>.
/// Covers validation rules for sign-up input fields (FR-1, AC-12 inline messages).
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class RequestSignUpOtpValidatorTests
{
    private readonly RequestSignUpOtpCommandValidator _validator = new();

    private static readonly DateOnly ValidDob = new(2000, 1, 1);

    // =========================================================================
    // First name
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "authentication")]
    public void Validate_WhenFirstNameIsBlank_HasFirstNameRequiredError(string firstName)
    {
        var cmd = new RequestSignUpOtpCommand(firstName, "Doe", "a@b.com", ValidDob);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.FirstName) &&
            e.ErrorMessage == AuthErrorKeys.FirstNameRequired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenFirstNameIsProvided_HasNoFirstNameError()
    {
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "a@b.com", ValidDob);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.FirstName));
    }

    // =========================================================================
    // Last name
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "authentication")]
    public void Validate_WhenLastNameIsBlank_HasLastNameRequiredError(string lastName)
    {
        var cmd = new RequestSignUpOtpCommand("Jane", lastName, "a@b.com", ValidDob);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.LastName) &&
            e.ErrorMessage == AuthErrorKeys.LastNameRequired);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenLastNameIsProvided_HasNoLastNameError()
    {
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "a@b.com", ValidDob);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.LastName));
    }

    // =========================================================================
    // Email
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenEmailIsEmpty_HasEmailRequiredError()
    {
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "", ValidDob);
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
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", email, ValidDob);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Email) &&
            e.ErrorMessage == AuthErrorKeys.EmailInvalid);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WithValidEmail_HasNoEmailError()
    {
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "jane@example.com", ValidDob);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Email));
    }

    // =========================================================================
    // Date of birth
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenDobIsToday_HasDobInPastError()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "jane@example.com", today);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.DateOfBirth) &&
            e.ErrorMessage == AuthErrorKeys.DobInPast);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenDobMakesUserUnder19_HasUnderageError()
    {
        var under19 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-18));
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "jane@example.com", under19);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.DateOfBirth) &&
            e.ErrorMessage == AuthErrorKeys.Underage);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WhenDobMakesUserExactly19_HasNoDobError()
    {
        var exactly19 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-19));
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "jane@example.com", exactly19);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.DateOfBirth));
    }

    // =========================================================================
    // Full valid command
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Validate_WithAllValidInputs_IsValid()
    {
        var cmd = new RequestSignUpOtpCommand("Jane", "Doe", "jane@example.com", ValidDob);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}
