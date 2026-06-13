namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a customer does not meet the minimum age requirement (19 years).
/// </summary>
public sealed class UnderageException : DomainException
{
    public const string MessageResourceKey = "Auth_Underage";

    public UnderageException()
        : base(MessageResourceKey)
    {
    }
}
