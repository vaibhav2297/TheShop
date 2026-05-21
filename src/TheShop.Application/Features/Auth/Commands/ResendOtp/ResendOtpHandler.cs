using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.ResendOtp;

public sealed class ResendOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<ResendOtpCommand, Result<OtpRequestedDto>>
{
    private const int ResendCooldownSeconds = 60;

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
