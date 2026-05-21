using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Auth.Commands.SignOut;

public sealed record SignOutCommand : IRequest<Result>;
