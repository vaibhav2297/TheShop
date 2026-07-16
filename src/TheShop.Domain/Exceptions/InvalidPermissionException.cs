namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a permission code does not match the required <c>module.action</c> format.
/// </summary>
public sealed class InvalidPermissionException(string attemptedCode) : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "Rbac_InvalidPermission";

    /// <summary>
    /// The malformed code that failed validation.
    /// </summary>
    public string AttemptedCode { get; } = attemptedCode;
}
