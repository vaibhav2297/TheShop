using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignInOtp;

/// <summary>
/// Handles <see cref="RequestSignInOtpCommand"/>. Verifies the customer account exists,
/// then delegates OTP delivery to <see cref="IAuthService"/>.
/// </summary>
public sealed class RequestSignInOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<RequestSignInOtpCommand, Result<OtpRequestedDto>>
{
    private const int ResendCooldownSeconds = 60;

    /// <summary>
    /// Returns <see cref="Result{T}.Ok"/> with the email and resend cooldown on success,
    /// or a failure result with <see cref="AuthErrorKeys.AccountNotFound"/> when the
    /// email is not registered.
    /// </summary>
    public async Task<Result<OtpRequestedDto>> Handle(
        RequestSignInOtpCommand request,
        CancellationToken cancellationToken)
    {
        if (!await customers.ExistsForEmailAsync(request.Email, cancellationToken))
            return Result.Fail<OtpRequestedDto>(AuthErrorKeys.AccountNotFound);

        var sendResult = await auth.SendSignInOtpAsync(request.Email, cancellationToken);
        if (sendResult.IsFailure)
            return Result.Fail<OtpRequestedDto>(sendResult.Error!);

        return Result.Ok(new OtpRequestedDto(request.Email, ResendCooldownSeconds));
    }
}
