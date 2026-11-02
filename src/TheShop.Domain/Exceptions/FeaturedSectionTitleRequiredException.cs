namespace TheShop.Domain.Exceptions;

/// <summary>Raised when a Featured section has no title.</summary>
public sealed class FeaturedSectionTitleRequiredException() : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "Featured_SectionTitleRequired";
}
