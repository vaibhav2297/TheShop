using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;

/// <summary>
/// Sends a sign-up OTP to a new email address. Fails when an account for that email
/// already exists.
/// </summary>
public sealed record RequestSignUpOtpCommand(
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth) : IRequest<Result<OtpRequestedDto>>;
