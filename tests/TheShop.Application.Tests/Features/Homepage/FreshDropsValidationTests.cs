using FluentAssertions;
using TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;
using TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class FreshDropsValidationTests
{
    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Create_BlankTitleAndIncompleteButton_ReturnsFieldErrors()
    {
        var command = new CreateFreshDropsCommand(" ", "Shop", null, FreshDropsSelectionMode.Newest, [], true);
        var result = new CreateFreshDropsCommandValidator().Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(["Title", "ButtonLink"]);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Update_BlankButtonPairAndSafeLink_AreAccepted()
    {
        var validator = new UpdateFreshDropsCommandValidator();
        validator.Validate(new UpdateFreshDropsCommand("Title", " ", " ", FreshDropsSelectionMode.Newest, [], false)).IsValid.Should().BeTrue();
        validator.Validate(new UpdateFreshDropsCommand("Title", "Shop", "https://example.com/products", FreshDropsSelectionMode.Newest, [], true))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Create_UnsafeLinkDuplicateAndFourthManualChoice_AreRejected()
    {
        var id = Guid.NewGuid();
        var validator = new CreateFreshDropsCommandValidator();
        validator.Validate(new CreateFreshDropsCommand("Title", "Shop", "//evil.example", FreshDropsSelectionMode.Newest, [], true))
            .Errors.Should().Contain(error => error.PropertyName == "ButtonLink");
        validator.Validate(new CreateFreshDropsCommand("Title", null, null, FreshDropsSelectionMode.Manual, [id, id], true))
            .Errors.Should().Contain(error => error.PropertyName == "ManualProductIds");
        validator.Validate(new CreateFreshDropsCommand("Title", null, null, FreshDropsSelectionMode.Manual,
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], true))
            .Errors.Should().Contain(error => error.PropertyName == "ManualProductIds");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Products_InvalidPageBounds_AreRejected()
    {
        var validator = new GetFreshDropsProductsQueryValidator();
        validator.Validate(new GetFreshDropsProductsQuery(Limit: 0, Offset: -1)).Errors.Select(error => error.PropertyName)
            .Should().Contain(["Limit", "Offset"]);
        validator.Validate(new GetFreshDropsProductsQuery(Limit: 48, Offset: 0)).IsValid.Should().BeTrue();
    }
}
