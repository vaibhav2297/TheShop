using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

/// <summary>
/// Validates <see cref="GetAdminProductsPageQuery"/>: the requested page number must be at
/// least 1. Page size is not validated here — the handler clamps it to the fixed page size.
/// </summary>
public sealed class GetAdminProductsPageQueryValidator : AbstractValidator<GetAdminProductsPageQuery>
{
    public const int PageSize = 10;

    public GetAdminProductsPageQueryValidator()
    {
        RuleFor(x => x.Pagination.Page)
            .GreaterThanOrEqualTo(1).WithMessage(ProductErrorKeys.PageInvalid);
    }
}
