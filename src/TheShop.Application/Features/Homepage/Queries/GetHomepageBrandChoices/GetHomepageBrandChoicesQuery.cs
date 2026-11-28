using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;

/// <summary>Lists active brand choices for staff with Homepage View.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode)]
public sealed record GetHomepageBrandChoicesQuery : IRequest<Result<IReadOnlyList<BrandDto>>>;
