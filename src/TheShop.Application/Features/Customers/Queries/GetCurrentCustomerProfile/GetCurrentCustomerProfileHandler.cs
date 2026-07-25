using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;

/// <summary>
/// Handles <see cref="GetCurrentCustomerProfileQuery"/> by loading the customer whose ID matches
/// the current auth identity. Returns <see cref="AuthErrorKeys.AccountNotFound"/> when the caller
/// is unauthenticated or has no customer record. The Supabase RLS policy remains the authoritative
/// boundary that restricts the read to the caller's own row.
/// </summary>
public sealed class GetCurrentCustomerProfileHandler(
    ICurrentUserService currentUser,
    ICustomerRepository customers)
    : IRequestHandler<GetCurrentCustomerProfileQuery, Result<CustomerProfileDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CustomerProfileDto>> Handle(
        GetCurrentCustomerProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return Result.Fail<CustomerProfileDto>(AuthErrorKeys.AccountNotFound);

        var customer = await customers.GetByIdAsync(userId, cancellationToken);
        if (customer is null)
            return Result.Fail<CustomerProfileDto>(AuthErrorKeys.AccountNotFound);

        return Result.Ok(new CustomerProfileDto(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email.Value,
            customer.DateOfBirth.Value));
    }
}
