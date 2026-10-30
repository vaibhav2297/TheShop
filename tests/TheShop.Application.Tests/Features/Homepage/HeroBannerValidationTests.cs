using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHeroBanner;
using TheShop.Application.Features.Homepage.Commands.UpdateHeroBanner;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.ValidateHeroBannerImage;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HeroBannerValidationTests
{
    private readonly IHeroBannerImageInspector _inspector = Substitute.For<IHeroBannerImageInspector>();
    private static ImageUpload Image(int bytes = 1, string mime = "image/png") => new(new byte[bytes], "image.png", mime);
    private static CreateHeroBannerCommand Create(ImageUpload? desktop = null, ImageUpload? mobile = null,
        string heading = "Heading", string? label = null, string? link = null) =>
        new(heading, null, label, link, desktop, mobile, true);

    private void Inspect(string mime, int width, int height, bool decodable = true) =>
        _inspector.InspectAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new HeroBannerImageInspection(mime, width, height, decodable));

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_MissingHeadingAndDesktop_ReturnsBothFieldErrors()
    {
        var result = await new CreateHeroBannerCommandValidator(_inspector).ValidateAsync(Create(heading: "  "));
        result.Errors.Should().Contain(e => e.PropertyName == "Heading" && e.ErrorMessage == HeroBannerErrorKeys.HeadingRequired);
        result.Errors.Should().Contain(e => e.PropertyName == "DesktopImage" && e.ErrorMessage == HeroBannerErrorKeys.DesktopImageRequired);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_ExactMinimumAndLargerSixteenNine_AcceptsBoth()
    {
        var validator = new CreateHeroBannerCommandValidator(_inspector);
        Inspect("image/png", 1920, 1080);
        (await validator.ValidateAsync(Create(Image()))).IsValid.Should().BeTrue();
        Inspect("image/png", 2560, 1440);
        (await validator.ValidateAsync(Create(Image()))).IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_DesktopWrongSizeOrRatio_RejectsImage()
    {
        var validator = new CreateHeroBannerCommandValidator(_inspector);
        Inspect("image/png", 1919, 1080);
        (await validator.ValidateAsync(Create(Image()))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.DesktopDimensionsInvalid);
        Inspect("image/png", 1920, 1081);
        (await validator.ValidateAsync(Create(Image()))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.DesktopDimensionsInvalid);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_MobileFourFive_AcceptsAndWrongRatioRejects()
    {
        Inspect("image/png", 800, 1000);
        var selection = new ValidateHeroBannerImageQueryValidator(_inspector);
        (await selection.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Mobile, Image()))).IsValid.Should().BeTrue();
        Inspect("image/png", 800, 999);
        (await selection.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Mobile, Image()))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.MobileDimensionsInvalid);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Selection_AtFiveMegabytesAccepted_AboveLimitRejectedBeforeDecode()
    {
        var validator = new ValidateHeroBannerImageQueryValidator(_inspector);
        Inspect("image/png", 1920, 1080);
        (await validator.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Desktop, Image(5_242_880)))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Desktop, Image(5_242_881)))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.ImageTooLarge);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Selection_MismatchedOrUndecodableBytes_RejectsImage()
    {
        var validator = new ValidateHeroBannerImageQueryValidator(_inspector);
        (await validator.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Desktop,
            Image(mime: "image/gif")))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.ImageInvalidType);
        Inspect("image/jpeg", 1920, 1080);
        (await validator.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Desktop, Image()))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.ImageInvalidType);
        Inspect("image/png", 1920, 1080, false);
        (await validator.ValidateAsync(new ValidateHeroBannerImageQuery(HeroBannerImageRole.Desktop, Image()))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.ImageUnreadable);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_UnsafeDestinationsAndIncompleteButtonPair_RejectsFields()
    {
        var validator = new CreateHeroBannerCommandValidator(_inspector);
        Inspect("image/png", 1920, 1080);
        foreach (var link in new[] { "javascript:alert(1)", "//example.com", "/bad\\path", "/bad%zz" })
            (await validator.ValidateAsync(Create(Image(), link: link))).Errors.Should()
                .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.ButtonLinkInvalid);
        foreach (var link in new[] { "/products", "https://example.com/products", "http://example.com" })
            (await validator.ValidateAsync(Create(Image(), label: "Shop", link: link))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(Create(Image(), label: "Shop"))).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.ButtonPairRequired);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Update_RemoveDesktopWithoutReplacementRejected_ReplacementAccepted()
    {
        var validator = new UpdateHeroBannerCommandValidator(_inspector);
        Inspect("image/png", 1920, 1080);
        var removed = new UpdateHeroBannerCommand("Heading", null, null, null, null, null, true, false, true);
        (await validator.ValidateAsync(removed)).Errors.Should()
            .Contain(e => e.ErrorMessage == HeroBannerErrorKeys.DesktopImageRequired);
        (await validator.ValidateAsync(removed with { NewDesktopImage = Image() })).IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Commands_NormalizeOptionalTextAndPreservePlainDestination()
    {
        var command = Create(Image(), heading: " Heading ", label: " Shop ", link: " /products ");
        command.Heading.Should().Be("Heading");
        command.ButtonLabel.Should().Be("Shop");
        command.ButtonLink.Should().Be("/products");
    }
}
