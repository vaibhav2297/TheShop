using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Homepage.Queries.ValidateHeroBannerImage;

/// <summary>Confirms acceptance after pipeline image validation.</summary>
public sealed class ValidateHeroBannerImageHandler : IRequestHandler<ValidateHeroBannerImageQuery, Result>
{
    /// <inheritdoc/>
    public Task<Result> Handle(ValidateHeroBannerImageQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Ok());
}

