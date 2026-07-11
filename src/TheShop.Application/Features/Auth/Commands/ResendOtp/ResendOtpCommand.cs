using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Auth.Commands.ResendOtp;

/// <summary>
/// Re-sends an OTP for either sign-up or sign-in, enforcing the same account-existence
/// guard as the original request.
/// </summary>
public sealed record ResendOtpCommand(string Email, OtpPurpose Purpose)
    : IRequest<Result<OtpRequestedDto>>;
