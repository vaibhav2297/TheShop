using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageBrand;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandForAdmin;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HomepageBrandHandlerTests
{
    private static readonly Guid[] Ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private static Brand Brand(Guid id, string name, bool active = true, string? logo = null) =>
        TheShop.Domain.Entities.Brand.Rehydrate(id, name, logoPath: logo, isActive: active);

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Create_ActiveBrands_SavesOneCompleteOrderedSection()
    {
        var repository = Substitute.For<IHomepageBrandRepository>();
        var brands = Substitute.For<IBrandRepository>();
        foreach (var id in Ids) brands.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Brand(id, id.ToString()));
        repository.SaveAsync(Arg.Any<HomepageBrandSection>(), true, Arg.Any<CancellationToken>()).Returns(Result.Ok());

        var result = await new CreateHomepageBrandHandler(repository, brands)
            .Handle(new(" Title ", " View ", " /brands ", [Ids[2], Ids[0]], true), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).SaveAsync(Arg.Is<HomepageBrandSection>(section =>
            section.Title == "Title" && section.ButtonLabel == "View" && section.ButtonLink == "/brands" &&
            section.BrandIds.SequenceEqual(new[] { Ids[2], Ids[0] }) && section.IsVisible),
            true, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Update_InactiveOrDeletedBrand_RejectsWithoutWriting()
    {
        var repository = Substitute.For<IHomepageBrandRepository>();
        var brands = Substitute.For<IBrandRepository>();
        brands.GetByIdAsync(Ids[0], Arg.Any<CancellationToken>()).Returns(Brand(Ids[0], "Active"));
        brands.GetByIdAsync(Ids[1], Arg.Any<CancellationToken>()).Returns(Brand(Ids[1], "Inactive", active: false));
        var result = await new UpdateHomepageBrandHandler(repository, brands)
            .Handle(new("Title", "View", "/brands", [Ids[0], Ids[1]], false), TestContext.Current.CancellationToken);
        result.Error.Should().Be(HomepageBrandErrorKeys.BrandUnavailable);
        await repository.DidNotReceive().SaveAsync(Arg.Any<HomepageBrandSection>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());

        brands.GetByIdAsync(Ids[1], Arg.Any<CancellationToken>()).Returns((Brand?)null);
        result = await new UpdateHomepageBrandHandler(repository, brands)
            .Handle(new("Title", "View", "/brands", [Ids[0], Ids[1]], false), TestContext.Current.CancellationToken);
        result.Error.Should().Be(HomepageBrandErrorKeys.BrandUnavailable);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Update_HiddenEmptySelection_PropagatesRepositoryConflict()
    {
        var repository = Substitute.For<IHomepageBrandRepository>();
        var brands = Substitute.For<IBrandRepository>();
        repository.SaveAsync(Arg.Any<HomepageBrandSection>(), false, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(HomepageBrandErrorKeys.NotFound));
        var result = await new UpdateHomepageBrandHandler(repository, brands)
            .Handle(new("Hidden", "View", "/brands", [], false), TestContext.Current.CancellationToken);
        result.Error.Should().Be(HomepageBrandErrorKeys.NotFound);
        await repository.Received(1).SaveAsync(Arg.Is<HomepageBrandSection>(section =>
            !section.IsVisible && section.BrandIds.Count == 0), false, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task AdminRead_AbsentSection_ReturnsSuccessfulNull()
    {
        var result = await new GetHomepageBrandForAdminHandler(Substitute.For<IHomepageBrandRepository>(),
            Substitute.For<IBrandRepository>(), Substitute.For<IFileStorage>())
            .Handle(new(), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task AdminRead_HiddenSectionPreservesIdsAndMarksInactiveOrMissingBrand()
    {
        var repository = Substitute.For<IHomepageBrandRepository>();
        var brands = Substitute.For<IBrandRepository>();
        var files = Substitute.For<IFileStorage>();
        repository.GetForAdminAsync(Arg.Any<CancellationToken>())
            .Returns(HomepageBrandSection.Create("Saved", "View", "/brands", Ids, false));
        brands.GetByIdAsync(Ids[0], Arg.Any<CancellationToken>()).Returns(Brand(Ids[0], "First", logo: "first.svg"));
        brands.GetByIdAsync(Ids[2], Arg.Any<CancellationToken>()).Returns(Brand(Ids[2], "Third", active: false));
        files.GetPublicUrl(StorageArea.BrandLogos, "first.svg").Returns("https://shop.test/first.svg");
        var result = await new GetHomepageBrandForAdminHandler(repository, brands, files)
            .Handle(new(), TestContext.Current.CancellationToken);
        result.Value.IsVisible.Should().BeFalse();
        result.Value.BrandIds.Should().Equal(Ids);
        result.Value.Brands.Select(brand => brand.Id).Should().Equal(Ids[0], Ids[2]);
        result.Value.Brands[0].LogoUrl.Should().Be("https://shop.test/first.svg");
        result.Value.Brands[1].IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Choices_ActiveBrandsKeepLogoAndNameOnlyChoice()
    {
        var brands = Substitute.For<IBrandRepository>();
        var files = Substitute.For<IFileStorage>();
        brands.GetActiveForHomepageAsync(Arg.Any<CancellationToken>())
            .Returns([Brand(Ids[0], "First", logo: "first.svg"), Brand(Ids[1], "Second")]);
        files.GetPublicUrl(StorageArea.BrandLogos, "first.svg").Returns("https://shop.test/first.svg");
        var result = await new GetHomepageBrandChoicesHandler(brands, files)
            .Handle(new(), TestContext.Current.CancellationToken);
        result.Value.Select(brand => brand.Name).Should().Equal("First", "Second");
        result.Value[0].LogoUrl.Should().Be("https://shop.test/first.svg");
        result.Value[1].LogoUrl.Should().BeNull();
    }
}
