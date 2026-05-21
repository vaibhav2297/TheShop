using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.ResendOtp;

public sealed record ResendOtpCommand(string Email, OtpPurpose Purpose)
    : IRequest<Result<OtpRequestedDto>>;
