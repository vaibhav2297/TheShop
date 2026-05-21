using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignInOtp;

public sealed class RequestSignInOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<RequestSignInOtpCommand, Result<OtpRequestedDto>>
{
    private const int ResendCooldownSeconds = 60;

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
