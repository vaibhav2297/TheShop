using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetProductDetail;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

public class GetProductDetailQueryValidatorTests
{
    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Validate_EmptyId_ReturnsUnavailableFieldFailure()
    {
        var result = await new GetProductDetailQueryValidator().ValidateAsync(new GetProductDetailQuery(Guid.Empty));
        result.Errors.Should().ContainSingle().Which.Should().Match<FluentValidation.Results.ValidationFailure>(
            e => e.PropertyName == "Id" && e.ErrorMessage == ProductErrorKeys.DetailUnavailable);
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Validate_ValidId_AcceptsPublicRequest()
    {
        var result = await new GetProductDetailQueryValidator().ValidateAsync(new GetProductDetailQuery(ProductDetailTestData.Id(1)));
        result.IsValid.Should().BeTrue();
    }
}
