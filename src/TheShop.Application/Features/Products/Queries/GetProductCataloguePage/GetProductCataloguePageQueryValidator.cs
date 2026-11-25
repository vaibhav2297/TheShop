using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetProductCataloguePage;

/// <summary>
/// Validates <see cref="GetProductCataloguePageQuery"/> to ensure page number/size and price range
/// are within acceptable bounds and sort option is a valid enum value.
/// </summary>
public sealed class GetProductCataloguePageQueryValidator : AbstractValidator<GetProductCataloguePageQuery>
{
    public const int MaxPageSize = 48;

    public GetProductCataloguePageQueryValidator()
    {
        RuleFor(x => x.Pagination.Page)
            .GreaterThanOrEqualTo(1).WithMessage(ProductErrorKeys.CataloguePageInvalid);

        RuleFor(x => x.Pagination.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(ProductErrorKeys.CataloguePageSizeInvalid);

        RuleFor(x => x)
            .Must(x => x.PriceMin is null || x.PriceMax is null || x.PriceMin <= x.PriceMax)
            .WithMessage(ProductErrorKeys.CataloguePriceRangeInvalid);

        RuleFor(x => x.Sort)
            .IsInEnum().WithMessage(ProductErrorKeys.CatalogueSortInvalid);

        RuleFor(x => x.SelectedFilters).NotNull().WithMessage(ProductErrorKeys.CatalogueFilterInvalid);
        RuleForEach(x => x.SelectedFilters)
            .Must(filter => filter is not null && ProductFilterKeys.IsSelectableKey(filter.Key)
                && filter.Values is not null && filter.Values.All(value => value is not null))
            .WithMessage(ProductErrorKeys.CatalogueFilterInvalid)
            .When(x => x.SelectedFilters is not null);
    }
}
