using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryChoices;

/// <summary>Lists active category choices for an authorized editor.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode)]
public sealed record GetHomepageCategoryChoicesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;
