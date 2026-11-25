using FluentValidation;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;

namespace TheShop.Application.Features.Products.Queries.GetProductCatalogueListing;

/// <summary>Applies existing catalogue input rules before metadata is loaded.</summary>
public sealed class GetProductCatalogueListingQueryValidator : AbstractValidator<GetProductCatalogueListingQuery>
{
    public GetProductCatalogueListingQueryValidator(IValidator<GetProductCataloguePageQuery> validator)
    {
        RuleFor(x => x.PageQuery).NotNull().WithMessage(ProductErrorKeys.CatalogueFilterInvalid)
            .SetValidator(validator);
    }
}
