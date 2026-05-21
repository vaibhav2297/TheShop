using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Auth.Commands.SignOut;

/// <summary>
/// Handles <see cref="SignOutCommand"/>. Delegates to <see cref="IAuthService.SignOutAsync"/>
/// and always returns success — sign-out is best-effort.
/// </summary>
public sealed class SignOutHandler(IAuthService auth) : IRequestHandler<SignOutCommand, Result>
{
    /// <summary>
    /// Signs the current user out and returns <see cref="Result.Ok"/>.
    /// </summary>
    public async Task<Result> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return Result.Ok();
    }
}
