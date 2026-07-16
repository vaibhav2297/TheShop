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

    /// <summary>
    /// <c>true</c> when the current user's principal carries <paramref name="permissionCode"/>
    /// (e.g. <c>"orders.refund"</c>). Permission claims are minted into the access token by the
    /// database at sign-in and every silent refresh, so staleness is bounded by the token TTL;
    /// Supabase RLS remains the authoritative check. Always <c>false</c> when unauthenticated.
    /// </summary>
    bool HasPermission(string permissionCode);
}
