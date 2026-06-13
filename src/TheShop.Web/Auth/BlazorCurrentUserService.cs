using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TheShop.Application.Common.Interfaces;

namespace TheShop.Web.Auth;

/// <summary>
/// Blazor WebAssembly implementation of <see cref="ICurrentUserService"/>. Resolves
/// the current user's identity synchronously from the active <see cref="AuthenticationStateProvider"/>.
/// </summary>
public sealed class BlazorCurrentUserService(AuthenticationStateProvider provider)
    : ICurrentUserService
{
    public Guid? Id => GetClaimGuid(ClaimTypes.NameIdentifier) ?? GetClaimGuid("sub");

    public string? Email => GetClaim(ClaimTypes.Email) ?? GetClaim("email");

    public bool IsAuthenticated =>
        GetUser().Identity?.IsAuthenticated ?? false;

    private ClaimsPrincipal GetUser()
    {
        var task = provider.GetAuthenticationStateAsync();
        return task.IsCompletedSuccessfully
            ? task.Result.User
            : task.GetAwaiter().GetResult().User;
    }

    private string? GetClaim(string type) =>
        GetUser().FindFirst(type)?.Value;

    private Guid? GetClaimGuid(string type) =>
        Guid.TryParse(GetClaim(type), out var id) ? id : null;
}
