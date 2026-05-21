using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// A registered customer. Enforces the minimum age of 19 at registration time and
/// keeps name and email immutable after creation.
/// </summary>
public sealed class Customer
{
    private const int MinimumAge = 19;
    private const string FirstNameRequiredKey = "Auth_FirstName_Required";
    private const string LastNameRequiredKey = "Auth_LastName_Required";

    public Guid Id { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public DateOfBirth DateOfBirth { get; }
    public Email Email { get; }
    public DateTimeOffset CreatedAt { get; }

    private Customer(
        Guid id,
        string firstName,
        string lastName,
        DateOfBirth dateOfBirth,
        Email email,
        DateTimeOffset createdAt)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        Email = email;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a new <see cref="Customer"/>, enforcing all registration invariants.
    /// </summary>
    /// <param name="authUserId">The Supabase auth user ID; used as the customer's primary key.</param>
    /// <param name="createdAt">Timestamp to assign; defaults to <c>DateTimeOffset.UtcNow</c> when <c>null</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="authUserId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="DomainException">Thrown when first name or last name is blank.</exception>
    /// <exception cref="UnderageException">Thrown when the customer is under 19 years of age.</exception>
    public static Customer Register(
        Guid authUserId,
        string firstName,
        string lastName,
        Email email,
        DateOfBirth dateOfBirth,
        DateTimeOffset? createdAt = null)
    {
        if (authUserId == Guid.Empty)
            throw new ArgumentException("Auth user id must not be empty.", nameof(authUserId));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException(FirstNameRequiredKey);

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException(LastNameRequiredKey);

        dateOfBirth.RequireAtLeast(MinimumAge);

        return new Customer(
            authUserId,
            firstName.Trim(),
            lastName.Trim(),
            dateOfBirth,
            email,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Reconstructs a <see cref="Customer"/> from persisted data without re-running
    /// registration invariants. Use only from repository mappers.
    /// </summary>
    public static Customer Rehydrate(
        Guid id,
        string firstName,
        string lastName,
        DateOfBirth dateOfBirth,
        Email email,
        DateTimeOffset createdAt)
    {
        return new Customer(id, firstName, lastName, dateOfBirth, email, createdAt);
    }
}
