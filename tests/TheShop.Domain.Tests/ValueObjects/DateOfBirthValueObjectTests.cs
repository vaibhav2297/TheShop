using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the <see cref="DateOfBirth"/> value object.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class DateOfBirthValueObjectTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    // =========================================================================
    // Create — validation
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Create_WithPastDate_ReturnsInstance()
    {
        var yesterday = Today.AddDays(-1);
        var dob = DateOfBirth.Create(yesterday);
        dob.Value.Should().Be(yesterday);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Create_WithTodayAsDate_ThrowsDomainException()
    {
        var act = () => DateOfBirth.Create(Today);
        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Auth_Dob_InPast");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Create_WithFutureDate_ThrowsDomainException()
    {
        var act = () => DateOfBirth.Create(Today.AddDays(1));
        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Auth_Dob_InPast");
    }

    // =========================================================================
    // AgeOn
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void AgeOn_BeforeBirthdayInBirthYear_ReturnsOneYearLess()
    {
        var dob = DateOfBirth.Create(new DateOnly(2000, 6, 15));
        // Reference date is before birthday in 2020 → age is 19, not 20
        var age = dob.AgeOn(new DateOnly(2020, 6, 14));
        age.Should().Be(19);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void AgeOn_OnExactBirthday_ReturnsExactAge()
    {
        var dob = DateOfBirth.Create(new DateOnly(2000, 6, 15));
        var age = dob.AgeOn(new DateOnly(2020, 6, 15));
        age.Should().Be(20);
    }

    // =========================================================================
    // RequireAtLeast — age guard
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void RequireAtLeast_WhenAgeIsAtMinimum_DoesNotThrow()
    {
        var today = new DateOnly(2025, 6, 15);
        var dob = DateOfBirth.Create(new DateOnly(2006, 6, 15), today.AddDays(1)); // 19 today
        var act = () => dob.RequireAtLeast(19, today);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void RequireAtLeast_WhenAgeIsBelowMinimum_ThrowsUnderageException()
    {
        var today = new DateOnly(2025, 6, 15);
        // 18 years old — dob is 2007-06-15 which makes them exactly 18 on that reference date
        var dob = DateOfBirth.Create(new DateOnly(2007, 6, 15), today.AddDays(1));
        var act = () => dob.RequireAtLeast(19, today);
        act.Should().Throw<UnderageException>();
    }
}
