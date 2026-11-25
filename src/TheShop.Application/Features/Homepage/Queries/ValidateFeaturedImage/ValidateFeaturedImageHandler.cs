using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;

/// <summary>Confirms acceptance after request validation succeeds.</summary>
public sealed class ValidateFeaturedImageHandler : IRequestHandler<ValidateFeaturedImageQuery, Result>
{
    /// <inheritdoc/>
    public Task<Result> Handle(ValidateFeaturedImageQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Ok());
}
