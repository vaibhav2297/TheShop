using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.ResendOtp;

/// <summary>
/// Handles <see cref="ResendOtpCommand"/>. Routes the resend to the sign-up or sign-in
/// OTP flow based on <see cref="OtpPurpose"/> and re-checks the account-existence invariant.
/// </summary>
public sealed class ResendOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<ResendOtpCommand, Result<OtpRequestedDto>>
{
    private const int ResendCooldownSeconds = 60;

    /// <summary>
    /// Returns <see cref="Result{T}.Ok"/> with the email and resend cooldown on success,
    /// or a failure result when the account state conflicts with the requested purpose
    /// or the auth provider rejects the resend (e.g. rate limit).
    /// </summary>
    public async Task<Result<OtpRequestedDto>> Handle(
        ResendOtpCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await customers.ExistsForEmailAsync(request.Email, cancellationToken);

        Result sendResult = request.Purpose switch
        {
            OtpPurpose.SignUp when exists =>
                Result.Fail(AuthErrorKeys.AccountAlreadyExists),

            OtpPurpose.SignIn when !exists =>
                Result.Fail(AuthErrorKeys.AccountNotFound),

            OtpPurpose.SignUp =>
                await auth.SendSignUpOtpAsync(request.Email, cancellationToken),

            OtpPurpose.SignIn =>
                await auth.SendSignInOtpAsync(request.Email, cancellationToken),

            _ => Result.Fail(AuthErrorKeys.Unexpected),
        };

        if (sendResult.IsFailure)
            return Result.Fail<OtpRequestedDto>(sendResult.Error!);

        return Result.Ok(new OtpRequestedDto(request.Email, ResendCooldownSeconds));
    }
}
