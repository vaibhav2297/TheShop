namespace TheShop.Domain.Exceptions;

/// <summary>
/// Base class for all domain rule violations. Carries a <see cref="MessageKey"/> that
/// maps to a resource key in <c>Strings.resx</c> so the UI can surface a localized message.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// The resource key used to look up the localized error message in <c>Strings.resx</c>.
    /// </summary>
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
