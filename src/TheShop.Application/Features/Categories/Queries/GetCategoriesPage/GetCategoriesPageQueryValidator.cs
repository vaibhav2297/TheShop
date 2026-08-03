using FluentValidation;

namespace TheShop.Application.Features.Categories.Queries.GetCategoriesPage;

/// <summary>
/// Validates <see cref="GetCategoriesPageQuery"/>: the requested page number must be at least 1.
/// Page size is not validated here — the handler clamps it to the fixed page size (spec
/// constraint, plan §9).
/// </summary>
public sealed class GetCategoriesPageQueryValidator : AbstractValidator<GetCategoriesPageQuery>
{
    public const int PageSize = 10;

    public GetCategoriesPageQueryValidator()
    {
        RuleFor(x => x.Pagination.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CategoryErrorKeys.PageInvalid);
    }
}
