using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.VerifySignInOtp;

/// <summary>
/// Handles <see cref="VerifySignInOtpCommand"/>. Verifies the OTP with the auth provider,
/// then loads the matching customer record to populate the session DTO.
/// </summary>
public sealed class VerifySignInOtpHandler(IAuthService auth, ICustomerRepository customers)
    : IRequestHandler<VerifySignInOtpCommand, Result<SessionDto>>
{
    /// <summary>
    /// Returns <see cref="Result{T}.Ok"/> with a <see cref="SessionDto"/> on success.
    /// Returns a failure result when the OTP is invalid or the customer record is not found
    /// (in the latter case the newly-created auth session is also immediately revoked).
    /// </summary>
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
