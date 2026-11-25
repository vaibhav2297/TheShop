using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>
/// A validated date of birth. Enforces that the date is in the past and supports age calculations.
/// </summary>
public sealed class DateOfBirth : IEquatable<DateOfBirth>
{
    private const string DobInPastKey = "Auth_Dob_InPast";

    public DateOnly Value { get; }

    private DateOfBirth(DateOnly value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a <see cref="DateOfBirth"/> after validating that the date is strictly in the past.
    /// </summary>
    /// <param name="today">Reference date for validation; defaults to today (UTC) when <c>null</c>.</param>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="value"/> is today or a future date.
    /// Carries <c>MessageKey = nameof(Strings.Auth_Dob_InPast)</c>.
    /// </exception>
    public static DateOfBirth Create(DateOnly value, DateOnly? today = null)
    {
        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (value >= reference)
            throw new DomainException(DobInPastKey);

        return new DateOfBirth(value);
    }

    /// <summary>
    /// Returns the person's age in whole years on the given reference date.
    /// </summary>
    public int AgeOn(DateOnly today)
    {
        var age = today.Year - Value.Year;
        if (today < Value.AddYears(age))
            age--;
        return age;
    }

    /// <summary>
    /// Asserts that the person's age meets or exceeds <paramref name="minAge"/> as of today.
    /// </summary>
    /// <param name="minAge">Minimum age in whole years that must be satisfied.</param>
    /// <param name="today">Reference date for age calculation; defaults to today (UTC) when <c>null</c>.</param>
    /// <exception cref="UnderageException">
    /// Thrown when the computed age is less than <paramref name="minAge"/>.
    /// </exception>
    public void RequireAtLeast(int minAge, DateOnly? today = null)
    {
        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (AgeOn(reference) < minAge)
            throw new UnderageException();
    }

    public bool Equals(DateOfBirth? other) =>
        other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as DateOfBirth);

    public override int GetHashCode() => Value.GetHashCode();
}
