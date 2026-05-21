using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;

public sealed record RequestSignUpOtpCommand(
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth) : IRequest<Result<OtpRequestedDto>>;
