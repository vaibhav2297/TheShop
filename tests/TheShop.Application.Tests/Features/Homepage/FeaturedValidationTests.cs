using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFeatured;
using TheShop.Application.Features.Homepage.Commands.UpdateFeatured;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class FeaturedValidationTests
{
    private readonly IHeroBannerImageInspector _inspector = Substitute.For<IHeroBannerImageInspector>();
    private static ImageUpload Image(int bytes = 1, string mime = "image/png") => new(new byte[bytes], "image.png", mime);
    private static FeaturedTileInput Tile(ImageUpload? image = null, string title = "Tile", string? label = null,
        string? link = null, bool remove = false) => new(null, title, label, link, image, remove);
    private void Inspect(string mime = "image/png", int width = 1200, int height = 1200, bool decodable = true) =>
        _inspector.InspectAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new HeroBannerImageInspection(mime, width, height, decodable));
    private Task<FluentValidation.Results.ValidationResult> Select(ImageUpload image) =>
        new ValidateFeaturedImageQueryValidator(_inspector).ValidateAsync(new ValidateFeaturedImageQuery(image), TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Create_MissingRequiredFieldsReportsBothTilePathsAndSectionTitle()
    {
        var result = await new CreateFeaturedCommandValidator(_inspector).ValidateAsync(
            new CreateFeaturedCommand(" ", Tile(title: ""), Tile(title: " "), true), TestContext.Current.CancellationToken);
        result.Errors.Select(x => x.PropertyName).Should().Contain(["Title", "Tile1.Title", "Tile2.Title", "Tile1.NewImage", "Tile2.NewImage"]);
        result.Errors.Should().OnlyContain(x => x.ErrorMessage.StartsWith("Featured_"));
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Selection_ThreeSupportedTypesAcceptMinimumAndLargerSquare()
    {
        foreach (var mime in new[] { "image/png", "image/jpeg", "image/webp" })
            foreach (var side in new[] { 1200, 2400 })
            {
                Inspect(mime, side, side);
                (await Select(Image(mime: mime))).IsValid.Should().BeTrue();
            }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Selection_ExactFiveMegabytesAcceptsAndLargerRejectsBeforeDecoding()
    {
        Inspect();
        (await Select(Image(5_242_880))).IsValid.Should().BeTrue();
        _inspector.ClearReceivedCalls();
        (await Select(Image(5_242_881))).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageTooLarge);
        await _inspector.DidNotReceiveWithAnyArgs().InspectAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Selection_EmptyUnsupportedMismatchedAndCorruptImagesReject()
    {
        (await Select(Image(0))).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageUnreadable);
        (await Select(Image(mime: "image/gif"))).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageInvalidType);
        Inspect("image/jpeg");
        (await Select(Image())).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageInvalidType);
        Inspect(decodable: false);
        (await Select(Image())).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageUnreadable);
        Inspect(width: 0, height: 0);
        (await Select(Image())).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageUnreadable);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Selection_UndersizedOrNonSquareRejectsWithoutRoundingTolerance()
    {
        foreach (var (width, height) in new[] { (1199, 1199), (1200, 1199), (1200, 1201), (2400, 1200) })
        {
            Inspect(width: width, height: height);
            (await Select(Image())).Errors.Should().Contain(x => x.ErrorMessage == FeaturedErrorKeys.ImageDimensionsInvalid);
        }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Update_ExistingImagesMayRemainButRemovedImageRequiresReplacement()
    {
        Inspect();
        var validator = new UpdateFeaturedCommandValidator(_inspector);
        var command = new UpdateFeaturedCommand("Featured", Tile(), Tile(), false);
        (await validator.ValidateAsync(command, TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
        var removed = command with { Tile2 = Tile(remove: true) };
        (await validator.ValidateAsync(removed, TestContext.Current.CancellationToken)).Errors.Should()
            .Contain(x => x.PropertyName == "Tile2.NewImage" && x.ErrorMessage == FeaturedErrorKeys.ImageRequired);
        (await validator.ValidateAsync(removed with { Tile2 = Tile(Image(), remove: true) }, TestContext.Current.CancellationToken))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Buttons_BlankPairsOptionalAndEachIncompletePairTargetsMissingField()
    {
        var validator = new UpdateFeaturedCommandValidator(_inspector);
        var command = new UpdateFeaturedCommand("Featured", Tile(), Tile(), true);
        (await validator.ValidateAsync(command, TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
        var invalid = command with { Tile1 = Tile(label: "Shop"), Tile2 = Tile(link: "/products") };
        var result = await validator.ValidateAsync(invalid, TestContext.Current.CancellationToken);
        result.Errors.Should().Contain(x => x.PropertyName == "Tile1.ButtonLink" && x.ErrorMessage == FeaturedErrorKeys.ButtonPairRequired);
        result.Errors.Should().Contain(x => x.PropertyName == "Tile2.ButtonLabel" && x.ErrorMessage == FeaturedErrorKeys.ButtonPairRequired);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Links_AllowRootRelativeOrHttpAndRejectUnsafeOrMalformedDestinations()
    {
        var validator = new UpdateFeaturedCommandValidator(_inspector);
        foreach (var link in new[] { "/", "/products?brand=1#top", "/caf%C3%A9", "https://example.test/products", "http://example.test" })
            (await validator.ValidateAsync(new UpdateFeaturedCommand("Featured", Tile(label: "Shop", link: link), Tile(), true),
                TestContext.Current.CancellationToken)).IsValid.Should().BeTrue($"{link} is allowed");
        foreach (var link in new[] { "//example.test", "javascript:alert(1)", "data:text/html,hi", "products", "/bad\\path", "/bad%zz", "/two words", "/bad\npath", "https:///missing-host" })
            (await validator.ValidateAsync(new UpdateFeaturedCommand("Featured", Tile(), Tile(label: "Shop", link: link), true),
                TestContext.Current.CancellationToken)).Errors.Should().Contain(x =>
                    x.PropertyName == "Tile2.ButtonLink" && x.ErrorMessage == FeaturedErrorKeys.ButtonLinkInvalid, link);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Commands_TrimRequiredAndOptionalTextWithoutInventingDefaults()
    {
        var tile = new FeaturedTileInput("  ", " Tile ", " Shop ", " /products ", null, false);
        var command = new CreateFeaturedCommand(" Featured ", tile, Tile(), true);
        command.Title.Should().Be("Featured");
        tile.Title.Should().Be("Tile");
        tile.Label.Should().BeNull();
        tile.ButtonLabel.Should().Be("Shop");
        tile.ButtonLink.Should().Be("/products");
        new UpdateFeaturedCommand(" ", tile, tile, true).Title.Should().BeEmpty();
    }
}
