namespace TheShop.Domain.Exceptions;

/// <summary>Raised when a Featured tile has no title.</summary>
public sealed class FeaturedTileTitleRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "Featured_TileTitleRequired";
}
