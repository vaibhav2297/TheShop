using FluentValidation;

namespace TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;

/// <summary>Bounds catalogue queries from the admin editor.</summary>
public sealed class GetFreshDropsProductsQueryValidator : AbstractValidator<GetFreshDropsProductsQuery>
{
    /// <summary>Creates the page bounds validator.</summary>
    public GetFreshDropsProductsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 48).WithMessage(FreshDropsErrorKeys.SelectionInvalid);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0).WithMessage(FreshDropsErrorKeys.SelectionInvalid);
    }
}
