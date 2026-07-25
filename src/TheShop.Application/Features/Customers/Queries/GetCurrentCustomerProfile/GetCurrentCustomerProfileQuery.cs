using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;

namespace TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;

/// <summary>
/// Requests the signed-in customer's profile (name and email) for display in account surfaces
/// such as the app-bar profile menu. Resolves the customer from the current auth identity, so it
/// takes no parameters. Returns a failure when there is no authenticated user or no matching
/// customer record.
/// </summary>
public sealed record GetCurrentCustomerProfileQuery : IRequest<Result<CustomerProfileDto>>;
