using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands.Mappers;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Mappers;

/// <summary>
/// Tests for <see cref="BrandDtoMapper"/> — the <see cref="Brand"/> → <c>BrandDto</c> projection
/// that resolves a stored logo key to its public bucket URL through <see cref="IFileStorage"/>
/// (Behavior 2, AC-4).
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class BrandDtoMapperTests
{
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToDto_MapsIdNameDescriptionAndIsActive()
    {
        var brand = Brand.Create("Elf Bar", "A vape brand.", isActive: false);

        var dto = BrandDtoMapper.ToDto(brand, _fileStorage);

        dto.Id.Should().Be(brand.Id);
        dto.Name.Should().Be("Elf Bar");
        dto.Description.Should().Be("A vape brand.");
        dto.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToDto_WhenLogoPathIsSet_ResolvesLogoUrlViaFileStorage()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        brand.AttachLogo("brands/abc/logo.webp");
        _fileStorage.GetPublicUrl(StorageArea.BrandLogos, "brands/abc/logo.webp")
                     .Returns("https://cdn.example/brand-logos/brands/abc/logo.webp");

        var dto = BrandDtoMapper.ToDto(brand, _fileStorage);

        dto.LogoUrl.Should().Be("https://cdn.example/brand-logos/brands/abc/logo.webp");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToDto_WhenLogoPathIsSet_QueriesTheBrandLogosStorageArea()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        brand.AttachLogo("brands/abc/logo.webp");

        BrandDtoMapper.ToDto(brand, _fileStorage);

        _fileStorage.Received(1).GetPublicUrl(StorageArea.BrandLogos, "brands/abc/logo.webp");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToDto_WhenLogoPathIsNull_MapsLogoUrlAsNullWithoutCallingFileStorage()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        var dto = BrandDtoMapper.ToDto(brand, _fileStorage);

        dto.LogoUrl.Should().BeNull();
        _fileStorage.DidNotReceive().GetPublicUrl(Arg.Any<StorageArea>(), Arg.Any<string>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-4: ToDto_WhenLogoPathIsSet_ResolvesLogoUrlViaFileStorage,
//        ToDto_WhenLogoPathIsSet_QueriesTheBrandLogosStorageArea
// AC-6: ToDto_WhenLogoPathIsNull_MapsLogoUrlAsNullWithoutCallingFileStorage
