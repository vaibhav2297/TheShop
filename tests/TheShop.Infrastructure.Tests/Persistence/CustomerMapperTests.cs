using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="CustomerMapper"/>.
/// Verifies the mapping contract between <see cref="CustomerRecord"/> (the Supabase row model)
/// and the <see cref="Customer"/> domain entity, sourced from the plan Data Model (§4) and the
/// schema (§10).
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class CustomerMapperTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateOnly Dob = new(2000, 6, 15);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    // =========================================================================
    // ToRecord — Customer → CustomerRecord
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToRecord_MapsIdCorrectly()
    {
        var customer = BuildCustomer();
        var record = CustomerMapper.ToRecord(customer);
        record.Id.Should().Be(Id);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToRecord_MapsFirstNameCorrectly()
    {
        var customer = BuildCustomer();
        var record = CustomerMapper.ToRecord(customer);
        record.FirstName.Should().Be("Jane");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToRecord_MapsLastNameCorrectly()
    {
        var customer = BuildCustomer();
        var record = CustomerMapper.ToRecord(customer);
        record.LastName.Should().Be("Doe");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToRecord_MapsEmailCorrectly()
    {
        var customer = BuildCustomer();
        var record = CustomerMapper.ToRecord(customer);
        record.Email.Should().Be("jane@example.com");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToRecord_MapsDateOfBirthAsDateTimeAtMidnightUtc()
    {
        // Plan §4 schema: date_of_birth DATE. Mapper converts DateOnly → DateTime at midnight.
        var customer = BuildCustomer();
        var record = CustomerMapper.ToRecord(customer);

        var expected = Dob.ToDateTime(TimeOnly.MinValue);
        record.DateOfBirth.Should().Be(expected);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToRecord_MapsCreatedAtAsUtcDateTime()
    {
        var customer = BuildCustomer();
        var record = CustomerMapper.ToRecord(customer);
        record.CreatedAt.Should().Be(CreatedAt.UtcDateTime);
    }

    // =========================================================================
    // ToDomain — CustomerRecord → Customer (round-trip)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToDomain_MapsIdCorrectly()
    {
        var record = BuildRecord();
        var customer = CustomerMapper.ToDomain(record);
        customer.Id.Should().Be(Id);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToDomain_MapsFirstNameCorrectly()
    {
        var record = BuildRecord();
        var customer = CustomerMapper.ToDomain(record);
        customer.FirstName.Should().Be("Jane");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToDomain_MapsLastNameCorrectly()
    {
        var record = BuildRecord();
        var customer = CustomerMapper.ToDomain(record);
        customer.LastName.Should().Be("Doe");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToDomain_MapsEmailCorrectly()
    {
        var record = BuildRecord();
        var customer = CustomerMapper.ToDomain(record);
        customer.Email.Value.Should().Be("jane@example.com");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToDomain_MapsDateOfBirthCorrectly()
    {
        var record = BuildRecord();
        var customer = CustomerMapper.ToDomain(record);
        customer.DateOfBirth.Value.Should().Be(Dob);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void ToDomain_MapsCreatedAtAsUtcDateTimeOffset()
    {
        var record = BuildRecord();
        var customer = CustomerMapper.ToDomain(record);
        customer.CreatedAt.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(CreatedAt.UtcDateTime, DateTimeKind.Utc)));
    }

    // =========================================================================
    // Round-trip: Customer → Record → Customer
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void RoundTrip_ToRecord_ThenToDomain_PreservesAllFields()
    {
        var original = BuildCustomer();
        var record = CustomerMapper.ToRecord(original);
        var restored = CustomerMapper.ToDomain(record);

        restored.Id.Should().Be(original.Id);
        restored.FirstName.Should().Be(original.FirstName);
        restored.LastName.Should().Be(original.LastName);
        restored.Email.Value.Should().Be(original.Email.Value);
        restored.DateOfBirth.Value.Should().Be(original.DateOfBirth.Value);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Customer BuildCustomer() =>
        Customer.Rehydrate(
            Id,
            "Jane",
            "Doe",
            DateOfBirth.Create(Dob),
            Email.Create("jane@example.com"),
            CreatedAt);

    private static CustomerRecord BuildRecord() => new()
    {
        Id = Id,
        FirstName = "Jane",
        LastName = "Doe",
        DateOfBirth = Dob.ToDateTime(TimeOnly.MinValue),
        Email = "jane@example.com",
        CreatedAt = CreatedAt.UtcDateTime,
    };
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: RoundTrip_ToRecord_ThenToDomain_PreservesAllFields
//        (mapper is the seam through which Customer persists and reconstitutes)
// AC-4: ToRecord_MapsEmailCorrectly
//        (plan §10 UNIQUE index is on lower(email); mapper stores email verbatim,
//         index operates at DB layer — mapper correctness is the prerequisite)
