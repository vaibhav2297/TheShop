using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.Mappers;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryForAdmin;

/// <summary>Projects the saved selection, retaining missing IDs for repair.</summary>
public sealed class GetHomepageCategoryForAdminHandler(IHomepageCategoryRepository repository,
    ICategoryRepository categories, IFileStorage files)
    : IRequestHandler<GetHomepageCategoryForAdminQuery, Result<HomepageCategoryDto?>>
{
    /// <inheritdoc/>
    public async Task<Result<HomepageCategoryDto?>> Handle(GetHomepageCategoryForAdminQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var section = await repository.GetForAdminAsync(cancellationToken);
            if (section is null) return Result.Ok<HomepageCategoryDto?>(null);
            var selected = new List<TheShop.Application.Features.Categories.DTOs.CategoryDto>();
            foreach (var id in section.CategoryIds)
            {
                var category = await categories.GetByIdAsync(id, cancellationToken);
                if (category is not null) selected.Add(CategoryDtoMapper.ToDto(category, files));
            }
            return Result.Ok<HomepageCategoryDto?>(new(section.Title, section.ButtonLabel, section.ButtonLink,
                [.. section.CategoryIds], selected, section.IsVisible));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail<HomepageCategoryDto?>(ex.MessageKey); }
        catch { return Result.Fail<HomepageCategoryDto?>(HomepageCategoryErrorKeys.LoadFailed); }
    }
}
