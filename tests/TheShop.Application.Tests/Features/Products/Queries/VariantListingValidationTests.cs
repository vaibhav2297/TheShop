using FluentAssertions;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductListing;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Application.Features.Products.Queries.GetProductCatalogueListing;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Enums;
using NSubstitute;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

public class VariantListingValidationTests
{
    [Theory]
    [InlineData("variant:colour", true, true)]
    [InlineData("variant:new merchant type", true, true)]
    [InlineData("brand", true, false)]
    [InlineData("price", false, false)]
    [InlineData("unknown", false, false)]
    [InlineData(null, false, false)]
    [Trait("Feature", "002_variant-filters")]
    public void Validate_FilterNamespace_RespectsPublicAndAdminContracts(string? key, bool publicValid, bool adminValid)
    {
        AppliedFilterDto filter = new(key!, ["red"]);
        var publicQuery = new GetProductCataloguePageQuery([filter], null, null, ProductSortOption.NewestFirst, new(1, 12));
        var adminQuery = new GetAdminProductsPageQuery(null, null, null, null, null, null, AdminProductSortOption.NameAToZ, new(1, 10), [filter]);

        new GetProductCatalogueListingQueryValidator(new GetProductCataloguePageQueryValidator()).Validate(new GetProductCatalogueListingQuery(publicQuery)).IsValid.Should().Be(publicValid);
        new GetAdminProductListingQueryValidator(new GetAdminProductsPageQueryValidator()).Validate(new GetAdminProductListingQuery(adminQuery)).IsValid.Should().Be(adminValid);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Validate_MalformedFilters_ReturnsFilterErrorInsteadOfThrowing()
    {
        var publicQuery = new GetProductCataloguePageQuery([new("variant:colour", null!)], null, null, ProductSortOption.NewestFirst, new(1, 12));
        var adminQuery = new GetAdminProductsPageQuery(null, null, null, null, null, null, AdminProductSortOption.NameAToZ, new(1, 10), [new("variant:colour", [null!])]);

        new GetProductCataloguePageQueryValidator().Validate(publicQuery).Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CatalogueFilterInvalid);
        new GetAdminProductsPageQueryValidator().Validate(adminQuery).Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CatalogueFilterInvalid);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void Validate_WrappedInvalidPageAndPrice_PreservesExistingValidation()
    {
        var publicQuery = new GetProductCataloguePageQuery([], 20, 10, ProductSortOption.NewestFirst, new(0, 12));
        var adminQuery = new GetAdminProductsPageQuery(null, null, null, null, 20, 10, AdminProductSortOption.NameAToZ, new(0, 10));

        var publicResult = new GetProductCatalogueListingQueryValidator(new GetProductCataloguePageQueryValidator()).Validate(new GetProductCatalogueListingQuery(publicQuery));
        var adminResult = new GetAdminProductListingQueryValidator(new GetAdminProductsPageQueryValidator()).Validate(new GetAdminProductListingQuery(adminQuery));

        publicResult.Errors.Should().HaveCount(2);
        adminResult.Errors.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task AdminPage_ValidVariantSelections_ForwardsToRepositoryWithCancellation()
    {
        var products = Substitute.For<IProductRepository>();
        using var cts = new CancellationTokenSource();
        var query = new GetAdminProductsPageQuery(" query ", null, null, null, null, null,
            AdminProductSortOption.NameAToZ, new(1, 10), [new("variant:colour", ["red", "blue"])]);
        products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), cts.Token)
            .Returns(PagedResult<ProductListItemDto>.Empty(new(1, 10)));

        await new GetAdminProductsPageHandler(products).Handle(query, cts.Token);

        await products.Received(1).GetAdminPageAsync(Arg.Is<AdminProductCriteria>(c =>
            c.Search == "query" && c.VariantFilters == query.VariantFilters), cts.Token);
    }
}
