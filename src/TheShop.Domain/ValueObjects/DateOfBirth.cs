using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

public sealed class DateOfBirth : IEquatable<DateOfBirth>
{
    private const string DobInPastKey = "Auth_Dob_InPast";

    public DateOnly Value { get; }

    private DateOfBirth(DateOnly value)
    {
        Value = value;
    }

    public static DateOfBirth Create(DateOnly value, DateOnly? today = null)
    {
        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (value >= reference)
            throw new DomainException(DobInPastKey);

        return new DateOfBirth(value);
    }

    public int AgeOn(DateOnly today)
    {
        var age = today.Year - Value.Year;
        if (today < Value.AddYears(age))
            age--;
        return age;
    }

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
