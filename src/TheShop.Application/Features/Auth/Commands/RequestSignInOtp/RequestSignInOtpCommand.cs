using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignInOtp;

/// <summary>
/// Sends a sign-in OTP to the given email. Fails when no customer account exists for
/// that address.
/// </summary>
public sealed record RequestSignInOtpCommand(string Email) : IRequest<Result<OtpRequestedDto>>;
