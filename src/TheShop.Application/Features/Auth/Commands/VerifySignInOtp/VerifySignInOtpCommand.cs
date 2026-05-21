using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.VerifySignInOtp;

public sealed record VerifySignInOtpCommand(string Email, string Code) : IRequest<Result<SessionDto>>;
