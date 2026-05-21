using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;

public sealed class RequestSignUpOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<RequestSignUpOtpCommand, Result<OtpRequestedDto>>
{
    private const int ResendCooldownSeconds = 60;

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
