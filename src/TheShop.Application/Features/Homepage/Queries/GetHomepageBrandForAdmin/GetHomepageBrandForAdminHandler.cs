using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Mappers;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Queries.GetHomepageBrandForAdmin;

/// <summary>Projects saved brand details while retaining missing IDs for repair.</summary>
public sealed class GetHomepageBrandForAdminHandler(IHomepageBrandRepository repository,
    IBrandRepository brands, IFileStorage files)
    : IRequestHandler<GetHomepageBrandForAdminQuery, Result<HomepageBrandDto?>>
{
    /// <inheritdoc/>
    public async Task<Result<HomepageBrandDto?>> Handle(GetHomepageBrandForAdminQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var section = await repository.GetForAdminAsync(cancellationToken);
            if (section is null) return Result.Ok<HomepageBrandDto?>(null);
            var selected = new List<BrandDto>();
            foreach (var id in section.BrandIds)
            {
                var brand = await brands.GetByIdAsync(id, cancellationToken);
                if (brand is not null) selected.Add(BrandDtoMapper.ToDto(brand, files));
            }
            return Result.Ok<HomepageBrandDto?>(new(section.Title, section.ButtonLabel, section.ButtonLink,
                [.. section.BrandIds], selected, section.IsVisible));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail<HomepageBrandDto?>(ex.MessageKey); }
        catch { return Result.Fail<HomepageBrandDto?>(HomepageBrandErrorKeys.LoadFailed); }
    }
}
