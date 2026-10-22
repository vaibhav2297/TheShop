using FluentValidation;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductListing;

/// <summary>Applies management input rules after authorization and before metadata reads.</summary>
public sealed class GetAdminProductListingQueryValidator : AbstractValidator<GetAdminProductListingQuery>
{
    public GetAdminProductListingQueryValidator(IValidator<GetAdminProductsPageQuery> validator)
    {
        RuleFor(x => x.PageQuery).NotNull().WithMessage(ProductErrorKeys.CatalogueFilterInvalid)
            .SetValidator(validator);
    }
}
