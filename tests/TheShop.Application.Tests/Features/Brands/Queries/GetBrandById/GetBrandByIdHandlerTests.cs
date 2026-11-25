using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Queries.GetBrandById;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Queries.GetBrandById;

/// <summary>
/// Tests for <see cref="GetBrandByIdHandler"/> — loads a brand for the edit form (AC-27), and
/// fails cleanly when it no longer exists (edge case: "the brand being edited was already removed
/// by another staff member").
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class GetBrandByIdHandlerTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private GetBrandByIdHandler CreateSut() => new(_brands, _fileStorage);

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenBrandExists_ReturnsSuccessResultWithTheBrandsDetails()
    {
        var brand = Brand.Create("Elf Bar", "A vape brand.", isActive: false);
        _brands.GetByIdAsync(brand.Id, Arg.Any<CancellationToken>()).Returns(brand);

        var result = await CreateSut().Handle(new GetBrandByIdQuery(brand.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(brand.Id);
        result.Value.Name.Should().Be("Elf Bar");
        result.Value.Description.Should().Be("A vape brand.");
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenBrandHasALogo_ResolvesLogoUrlViaFileStorage()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        brand.AttachLogo("brands/abc/logo.webp");
        _brands.GetByIdAsync(brand.Id, Arg.Any<CancellationToken>()).Returns(brand);
        _fileStorage.GetPublicUrl(StorageArea.BrandLogos, "brands/abc/logo.webp")
                     .Returns("https://cdn.example/brand-logos/brands/abc/logo.webp");

        var result = await CreateSut().Handle(new GetBrandByIdQuery(brand.Id), CancellationToken.None);

        result.Value.LogoUrl.Should().Be("https://cdn.example/brand-logos/brands/abc/logo.webp");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenBrandDoesNotExist_ReturnsNotFoundFailure()
    {
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Brand?)null);

        var result = await CreateSut().Handle(new GetBrandByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.NotFound);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-27: Handle_WhenBrandExists_ReturnsSuccessResultWithTheBrandsDetails,
//         Handle_WhenBrandDoesNotExist_ReturnsNotFoundFailure
