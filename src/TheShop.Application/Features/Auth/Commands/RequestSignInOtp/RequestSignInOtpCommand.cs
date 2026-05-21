using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignInOtp;

public sealed record RequestSignInOtpCommand(string Email) : IRequest<Result<OtpRequestedDto>>;
