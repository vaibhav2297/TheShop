using System.Text.RegularExpressions;
using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

public sealed partial class Email : IEquatable<Email>
{
    private const string EmailInvalidKey = "Email_Invalid";

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException(EmailInvalidKey);

        var trimmed = input.Trim();

        if (!EmailRegex().IsMatch(trimmed))
            throw new DomainException(EmailInvalidKey);

        return new Email(trimmed);
    }

    public override string ToString() => Value;

    public bool Equals(Email? other) =>
        other is not null &&
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.ToLowerInvariant().GetHashCode();

    [GeneratedRegex(
        @"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
