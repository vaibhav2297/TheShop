using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;

/// <summary>Checks selected categories and creates the singleton.</summary>
public sealed class CreateHomepageCategoryHandler(IHomepageCategoryRepository repository, ICategoryRepository categories)
    : IRequestHandler<CreateHomepageCategoryCommand, Result>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(CreateHomepageCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await HomepageCategoryInput.CategoriesAvailableAsync(categories, request.CategoryIds, cancellationToken))
                return Result.Fail(HomepageCategoryErrorKeys.CategoryUnavailable);
            var section = HomepageCategorySection.Create(request.Title, request.ButtonLabel, request.ButtonLink,
                request.CategoryIds, request.IsVisible);
            return await repository.SaveAsync(section, true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail(ex.MessageKey); }
        catch { return Result.Fail(HomepageCategoryErrorKeys.SaveFailed); }
    }
}
