namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Provides the identity of the user executing the current request. The Web layer
/// implementation resolves claims from the Blazor <c>AuthenticationStateProvider</c>.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The authenticated user's ID, or <c>null</c> when unauthenticated.
    /// </summary>
    Guid? Id { get; }

    /// <summary>
    /// The authenticated user's email address, or <c>null</c> when unauthenticated.
    /// </summary>
    string? Email { get; }

    bool IsAuthenticated { get; }
}
