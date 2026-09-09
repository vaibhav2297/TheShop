using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

/// <summary>
/// Validates <see cref="GetAdminProductsPageQuery"/>: the requested page number must be at least
/// 1, and a supplied price range must be non-negative with <c>PriceMin &lt;= PriceMax</c>
/// (RULE-10). <c>Search</c> needs no rule — blank or whitespace collapses to <see langword="null"/>
/// in the handler.
/// </summary>
public sealed class GetAdminProductsPageQueryValidator : AbstractValidator<GetAdminProductsPageQuery>
{
    public const int PageSize = 10;

    public GetAdminProductsPageQueryValidator()
    {
        RuleFor(x => x.Pagination.Page)
            .GreaterThanOrEqualTo(1).WithMessage(ProductErrorKeys.PageInvalid);

        RuleFor(x => x.PriceMin)
            .GreaterThanOrEqualTo(0).WithMessage(ProductErrorKeys.PriceRangeInvalid)
            .When(x => x.PriceMin.HasValue);

        RuleFor(x => x.PriceMax)
            .GreaterThanOrEqualTo(0).WithMessage(ProductErrorKeys.PriceRangeInvalid)
            .When(x => x.PriceMax.HasValue);

        RuleFor(x => x)
            .Must(x => x.PriceMin! <= x.PriceMax!)
            .WithMessage(ProductErrorKeys.PriceRangeInvalid)
            .When(x => x.PriceMin.HasValue && x.PriceMax.HasValue);
    }
}
