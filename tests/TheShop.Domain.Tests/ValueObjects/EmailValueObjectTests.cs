using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the <see cref="Email"/> value object.
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class EmailValueObjectTests
{
    // =========================================================================
    // Happy path
    // =========================================================================

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("user.name+tag@sub.domain.org")]
    [Trait("Feature", "authentication")]
    public void Create_WithValidEmail_ReturnsEmailWithTrimmedLowerCaseValue(string input)
    {
        var email = Email.Create(input);
        email.Value.Should().Be(input.Trim());
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Create_WithSurroundingWhitespace_TrimsInput()
    {
        var email = Email.Create("  user@example.com  ");
        email.Value.Should().Be("user@example.com");
    }

    // =========================================================================
    // Validation failures
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "authentication")]
    public void Create_WhenInputIsNullOrWhitespace_ThrowsDomainException(string? input)
    {
        var act = () => Email.Create(input);
        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Email_Invalid");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing-at-sign.com")]
    [InlineData("@nodomain")]
    [InlineData("double@@at.com")]
    [InlineData("no.tld@")]
    [Trait("Feature", "authentication")]
    public void Create_WithMalformedEmail_ThrowsDomainException(string input)
    {
        var act = () => Email.Create(input);
        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Email_Invalid");
    }

    // =========================================================================
    // Equality (case-insensitive)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Equals_WhenSameEmailDifferentCase_ReturnsTrue()
    {
        var a = Email.Create("User@Example.com");
        var b = Email.Create("user@example.com");
        a.Should().Be(b);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void GetHashCode_WhenSameEmailDifferentCase_ReturnsSameHash()
    {
        var a = Email.Create("User@Example.com");
        var b = Email.Create("user@example.com");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
