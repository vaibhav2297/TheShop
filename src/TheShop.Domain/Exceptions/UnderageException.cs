namespace TheShop.Domain.Exceptions;

public sealed class UnderageException : DomainException
{
    public const string MessageResourceKey = "Auth_Underage";

    public UnderageException()
        : base(MessageResourceKey)
    {
    }
}
