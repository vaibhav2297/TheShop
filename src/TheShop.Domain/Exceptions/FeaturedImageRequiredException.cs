namespace TheShop.Domain.Exceptions;

/// <summary>Raised when a Featured tile has no artwork.</summary>
public sealed class FeaturedImageRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "Featured_ImageRequired";
}
