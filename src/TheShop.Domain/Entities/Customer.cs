using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

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
