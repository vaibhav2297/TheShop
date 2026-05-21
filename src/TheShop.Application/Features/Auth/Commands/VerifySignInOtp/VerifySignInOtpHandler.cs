using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.VerifySignInOtp;

public sealed class VerifySignInOtpHandler(IAuthService auth, ICustomerRepository customers)
    : IRequestHandler<VerifySignInOtpCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> Handle(
        VerifySignInOtpCommand request,
        CancellationToken cancellationToken)
    {
        var verifyResult = await auth.VerifyOtpAsync(
            request.Email, request.Code, cancellationToken);

        if (verifyResult.IsFailure)
            return Result.Fail<SessionDto>(verifyResult.Error!);

        var session = verifyResult.Value;

        var customer = await customers.GetByIdAsync(session.UserId, cancellationToken);
        if (customer is null)
        {
            await auth.SignOutAsync(cancellationToken);
            return Result.Fail<SessionDto>(AuthErrorKeys.AccountNotFound);
        }

        var sessionDto = new SessionDto(
            session.UserId,
            session.Email,
            session.AccessToken,
            session.RefreshToken,
            session.ExpiresAt,
            new CustomerProfileDto(
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Email.Value,
                customer.DateOfBirth.Value));

        return Result.Ok(sessionDto);
    }
}
