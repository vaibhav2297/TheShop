namespace TheShop.Domain.Exceptions;

/// <summary>Raised when a Featured button has only a label or destination.</summary>
public sealed class FeaturedButtonPairRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "Featured_ButtonPairRequired";
}
