using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Auth.Commands.SignOut;

/// <summary>
/// Terminates the current user's session with the auth provider.
/// </summary>
public sealed record SignOutCommand : IRequest<Result>;
