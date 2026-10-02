using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetProductDetails;

/// <summary>
/// Validates <see cref="GetProductDetailsQuery"/>: an empty id can never name a product, so it
/// fails with the same not-found key as a missing product.
/// </summary>
public sealed class GetProductDetailsQueryValidator : AbstractValidator<GetProductDetailsQuery>
{
    public GetProductDetailsQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(ProductErrorKeys.NotFound);
    }
}
