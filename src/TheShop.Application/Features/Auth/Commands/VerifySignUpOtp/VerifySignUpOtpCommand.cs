using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;

/// <summary>
/// Verifies a sign-up OTP and, on success, registers the new customer and returns
/// a fully-populated session.
/// </summary>
public sealed record VerifySignUpOtpCommand(
    string Email,
    string Code,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth) : IRequest<Result<SessionDto>>;
