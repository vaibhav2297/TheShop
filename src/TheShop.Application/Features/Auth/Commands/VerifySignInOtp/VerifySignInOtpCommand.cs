using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.VerifySignInOtp;

/// <summary>
/// Verifies a sign-in OTP and, on success, returns a fully-populated session including
/// the customer profile.
/// </summary>
public sealed record VerifySignInOtpCommand(string Email, string Code) : IRequest<Result<SessionDto>>;
