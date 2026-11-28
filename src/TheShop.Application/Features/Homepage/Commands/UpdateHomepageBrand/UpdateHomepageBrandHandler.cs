using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;

/// <summary>Checks selected brands and updates the singleton configuration.</summary>
public sealed class UpdateHomepageBrandHandler(IHomepageBrandRepository repository, IBrandRepository brands)
    : IRequestHandler<UpdateHomepageBrandCommand, Result>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(UpdateHomepageBrandCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await HomepageBrandInput.BrandsAvailableAsync(brands, request.BrandIds, cancellationToken))
                return Result.Fail(HomepageBrandErrorKeys.BrandUnavailable);
            var section = HomepageBrandSection.Create(request.Title, request.ButtonLabel, request.ButtonLink,
                request.BrandIds, request.IsVisible);
            return await repository.SaveAsync(section, false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail(ex.MessageKey); }
        catch { return Result.Fail(HomepageBrandErrorKeys.SaveFailed); }
    }
}
