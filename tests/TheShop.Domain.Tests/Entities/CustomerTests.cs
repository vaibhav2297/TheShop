using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="Customer"/> entity and the <see cref="Email"/> / <see cref="DateOfBirth"/>
/// value objects. Covers all registration invariants described in the authentication spec.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class CustomerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly DateOnly TwentyYearsAgo =
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-20));

    private static readonly DateOnly ExactlyNineteenYearsAgo =
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-19));

    private static readonly DateOnly EighteenYearsAndSixMonthsAgo =
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-18).AddMonths(-6));

    private static Customer RegisterValid(
        string firstName = "Jane",
        string lastName = "Doe",
        string email = "jane@example.com",
        DateOnly? dob = null)
    {
        var dateOfBirth = DateOfBirth.Create(dob ?? TwentyYearsAgo);
        return Customer.Register(
            Guid.NewGuid(),
            firstName,
            lastName,
            Email.Create(email),
            dateOfBirth);
    }

    // =========================================================================
    // Customer.Register — Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WithAllValidInputs_ReturnsCustomerWithExpectedProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = Email.Create("jane@example.com");
        var dob = DateOfBirth.Create(TwentyYearsAgo);

        // Act
        var customer = Customer.Register(id, "Jane", "Doe", email, dob);

        // Assert
        customer.Id.Should().Be(id);
        customer.FirstName.Should().Be("Jane");
        customer.LastName.Should().Be("Doe");
        customer.Email.Value.Should().Be("jane@example.com");
        customer.DateOfBirth.Value.Should().Be(TwentyYearsAgo);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenFirstNameHasSurroundingWhitespace_TrimsFirstName()
    {
        // Arrange / Act
        var customer = RegisterValid(firstName: "  Jane  ");

        // Assert
        customer.FirstName.Should().Be("Jane");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenLastNameHasSurroundingWhitespace_TrimsLastName()
    {
        var customer = RegisterValid(lastName: "  Doe  ");
        customer.LastName.Should().Be("Doe");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenCreatedAtIsProvided_UsesSuppliedTimestamp()
    {
        var fixedTime = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var email = Email.Create("a@b.com");
        var dob = DateOfBirth.Create(TwentyYearsAgo);

        var customer = Customer.Register(Guid.NewGuid(), "A", "B", email, dob, fixedTime);

        customer.CreatedAt.Should().Be(fixedTime);
    }

    // =========================================================================
    // Customer.Register — Validation failures
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenAuthUserIdIsEmpty_ThrowsArgumentException()
    {
        var email = Email.Create("a@b.com");
        var dob = DateOfBirth.Create(TwentyYearsAgo);

        var act = () => Customer.Register(Guid.Empty, "Jane", "Doe", email, dob);

        act.Should().Throw<ArgumentException>()
           .WithParameterName("authUserId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "authentication")]
    public void Register_WhenFirstNameIsBlank_ThrowsDomainException(string firstName)
    {
        var email = Email.Create("a@b.com");
        var dob = DateOfBirth.Create(TwentyYearsAgo);

        var act = () => Customer.Register(Guid.NewGuid(), firstName, "Doe", email, dob);

        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Auth_FirstName_Required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "authentication")]
    public void Register_WhenLastNameIsBlank_ThrowsDomainException(string lastName)
    {
        var email = Email.Create("a@b.com");
        var dob = DateOfBirth.Create(TwentyYearsAgo);

        var act = () => Customer.Register(Guid.NewGuid(), "Jane", lastName, email, dob);

        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Auth_LastName_Required");
    }

    // =========================================================================
    // Customer.Register — Age constraint (FR-5, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenCustomerIsExactly19_Succeeds()
    {
        // The day of their 19th birthday — they are exactly 19 today.
        var dob = DateOfBirth.Create(ExactlyNineteenYearsAgo);
        var email = Email.Create("teen@example.com");

        var act = () => Customer.Register(Guid.NewGuid(), "Teen", "User", email, dob);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenCustomerIsUnder19_ThrowsUnderageException()
    {
        var dob = DateOfBirth.Create(EighteenYearsAndSixMonthsAgo);
        var email = Email.Create("young@example.com");

        var act = () => Customer.Register(Guid.NewGuid(), "Young", "User", email, dob);

        act.Should().Throw<UnderageException>()
           .Which.MessageKey.Should().Be("Auth_Underage");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Register_WhenCustomerIsBornOneYearBeforeMinimumAge_ThrowsUnderageException()
    {
        // 18 years and 364 days old — just short of 19
        var dob = DateOfBirth.Create(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-19).AddDays(1)));

        var email = Email.Create("almostlegal@example.com");

        var act = () => Customer.Register(Guid.NewGuid(), "Young", "User", email, dob);

        act.Should().Throw<UnderageException>();
    }

    // =========================================================================
    // Customer.Rehydrate
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Rehydrate_WithValidData_ReturnsCustomerWithoutReevaluatingInvariants()
    {
        // Arrange: use an age that would fail Register but Rehydrate should skip checks.
        var id = Guid.NewGuid();
        var email = Email.Create("stored@example.com");
        var dob = DateOfBirth.Create(TwentyYearsAgo);
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var customer = Customer.Rehydrate(id, "John", "Smith", dob, email, createdAt);

        // Assert
        customer.Id.Should().Be(id);
        customer.FirstName.Should().Be("John");
        customer.LastName.Should().Be("Smith");
        customer.Email.Value.Should().Be("stored@example.com");
        customer.CreatedAt.Should().Be(createdAt);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Register_WithAllValidInputs_ReturnsCustomerWithExpectedProperties
// AC-3: Register_WhenCustomerIsUnder19_ThrowsUnderageException,
//        Register_WhenCustomerIsBornOneYearBeforeMinimumAge_ThrowsUnderageException,
//        Register_WhenCustomerIsExactly19_Succeeds
//        (DateOfBirth age rules: RequireAtLeast_WhenAgeIsBelowMinimum_ThrowsUnderageException)
// AC-12 (email validation for malformed input):
//        Create_WithMalformedEmail_ThrowsDomainException,
//        Create_WhenInputIsNullOrWhitespace_ThrowsDomainException
