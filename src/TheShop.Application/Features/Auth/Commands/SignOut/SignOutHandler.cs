using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Auth.Commands.SignOut;

public sealed class SignOutHandler(IAuthService auth) : IRequestHandler<SignOutCommand, Result>
{
    public async Task<Result> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return Result.Ok();
    }
}
