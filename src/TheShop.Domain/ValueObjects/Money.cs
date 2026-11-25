using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>
/// A monetary amount in a given currency. Equality is by value.
/// </summary>
public sealed class Money : IEquatable<Money>
{
    private const string AmountNegativeKey = "Money_Negative";

    /// <summary>
    /// The default currency for this storefront.
    /// </summary>
    public const string DefaultCurrency = "CAD";

    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a <see cref="Money"/> value after validating that the amount is non-negative.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">The currency code; defaults to <see cref="DefaultCurrency"/>.</param>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="amount"/> is negative. Carries <c>MessageKey = nameof(Strings.Money_Negative)</c>.
    /// </exception>
    public static Money Create(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
            throw new DomainException(AmountNegativeKey);

        return new Money(amount, currency);
    }

    public bool Equals(Money? other) =>
        other is not null &&
        Amount == other.Amount &&
        string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency.ToUpperInvariant());
}
