using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetProductCataloguePage;

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

        RuleForEach(x => x.SelectedFilters)
            .Must(filter => ProductFilterKeys.SelectableKeys.Contains(filter.Key))
            .WithMessage(ProductErrorKeys.CatalogueFilterInvalid);
    }
}
