using FluentValidation;

namespace TheShop.Application.Features.Brands.Queries.GetBrandsPage;

/// <summary>
/// Validates <see cref="GetBrandsPageQuery"/>: the requested page number must be at least 1.
/// Page size is not validated here — the handler clamps it to the fixed page size (spec
/// constraint, plan §9).
/// </summary>
public sealed class GetBrandsPageQueryValidator : AbstractValidator<GetBrandsPageQuery>
{
    public const int PageSize = 10;

    public GetBrandsPageQueryValidator()
    {
        RuleFor(x => x.Pagination.Page)
            .GreaterThanOrEqualTo(1).WithMessage(BrandErrorKeys.PageInvalid);
    }
}
