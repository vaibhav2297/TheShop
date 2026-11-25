namespace TheShop.Domain.Exceptions;

/// <summary>Raised when banner artwork has no desktop image.</summary>
public sealed class HeroBannerDesktopImageRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "HeroBanner_DesktopImageRequired";
}
