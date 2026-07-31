using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Commands.UpdateBrand;

/// <summary>
/// Updates an existing brand's name, description, status, and logo. <see cref="RemoveLogo"/>
/// takes precedence when both it and <see cref="NewLogo"/> are supplied.
/// </summary>
[RequiresPermission("brands.edit")]
public sealed record UpdateBrandCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    BrandLogoUpload? NewLogo,
    bool RemoveLogo) : IRequest<Result<BrandDto>>;
