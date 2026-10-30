namespace TheShop.Domain.Exceptions;

/// <summary>Raised when banner button content is incomplete.</summary>
public sealed class HeroBannerButtonPairRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "HeroBanner_ButtonPairRequired";
}
