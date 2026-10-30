namespace TheShop.Domain.Exceptions;

/// <summary>Raised when banner content has no heading.</summary>
public sealed class HeroBannerHeadingRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "HeroBanner_HeadingRequired";
}
