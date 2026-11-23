namespace TheShop.Domain.Exceptions;

/// <summary>Raised when an image is repeated within a variant gallery.</summary>
public sealed class DuplicateVariantImageException : DomainException
{
    public const string MessageResourceKey = "Product_VariantImageDuplicate";

    public DuplicateVariantImageException() : base(MessageResourceKey) { }
}
