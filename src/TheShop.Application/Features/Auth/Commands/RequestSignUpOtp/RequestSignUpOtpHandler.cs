using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;

/// <summary>
/// Handles <see cref="RequestSignUpOtpCommand"/>. Guards against duplicate accounts,
/// then delegates OTP delivery to <see cref="IAuthService"/>.
/// </summary>
public sealed class RequestSignUpOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<RequestSignUpOtpCommand, Result<OtpRequestedDto>>
{
    private const int ResendCooldownSeconds = 60;

    /// <summary>
    /// Returns <see cref="Result{T}.Ok"/> with the email and resend cooldown on success,
    /// or a failure result with <see cref="AuthErrorKeys.AccountAlreadyExists"/> when the
    /// email is already registered.
    /// </summary>
    public async Task<Result<OtpRequestedDto>> Handle(
        RequestSignUpOtpCommand request,
        CancellationToken cancellationToken)
    {
        if (await customers.ExistsForEmailAsync(request.Email, cancellationToken))
            return Result.Fail<OtpRequestedDto>(AuthErrorKeys.AccountAlreadyExists);

        var sendResult = await auth.SendSignUpOtpAsync(request.Email, cancellationToken);
        if (sendResult.IsFailure)
            return Result.Fail<OtpRequestedDto>(sendResult.Error!);

        return Result.Ok(new OtpRequestedDto(request.Email, ResendCooldownSeconds));
    }
}
