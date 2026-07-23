using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Commands.CreateBrand;

/// <summary>
/// Creates a new brand. Only the name is required; description, logo, and status are optional
/// (status defaults to Active at the Web layer per FR-5).
/// </summary>
[RequiresPermission("brands.create")]
public sealed record CreateBrandCommand(
    string Name,
    string? Description,
    BrandLogoUpload? Logo,
    bool IsActive) : IRequest<Result<BrandDto>>;
