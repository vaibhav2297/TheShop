namespace TheShop.Domain.Exceptions;

public class DomainException : Exception
{
    public string MessageKey { get; }

    public DomainException(string messageKey)
        : base(messageKey)
    {
        MessageKey = messageKey;
    }

    public DomainException(string messageKey, Exception innerException)
        : base(messageKey, innerException)
    {
        MessageKey = messageKey;
    }
}
