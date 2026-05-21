using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;

/// <summary>
/// Handles <see cref="VerifySignUpOtpCommand"/>. Verifies the OTP, constructs and persists
/// the new <see cref="Customer"/> entity, then returns the session DTO. If domain
/// invariants are violated after a successful OTP exchange, the auth session is revoked
/// before returning the failure.
/// </summary>
public sealed class VerifySignUpOtpHandler(IAuthService auth, ICustomerRepository customers)
    : IRequestHandler<VerifySignUpOtpCommand, Result<SessionDto>>
{
    /// <summary>
    /// Returns <see cref="Result{T}.Ok"/> with a <see cref="SessionDto"/> on success,
    /// or a failure result when the OTP is invalid or domain registration invariants are violated.
    /// </summary>
    public async Task<Result<SessionDto>> Handle(
        VerifySignUpOtpCommand request,
        CancellationToken cancellationToken)
    {
        var verifyResult = await auth.VerifyOtpAsync(
            request.Email, request.Code, cancellationToken);

        if (verifyResult.IsFailure)
            return Result.Fail<SessionDto>(verifyResult.Error!);

        var session = verifyResult.Value;

        Customer customer;
        try
        {
            var email = Email.Create(request.Email);
            var dob = DateOfBirth.Create(request.DateOfBirth);

            customer = Customer.Register(
                session.UserId,
                request.FirstName,
                request.LastName,
                email,
                dob);
        }
        catch (DomainException ex)
        {
            await auth.SignOutAsync(cancellationToken);
            return Result.Fail<SessionDto>(ex.MessageKey);
        }

        await customers.AddAsync(customer, cancellationToken);

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
