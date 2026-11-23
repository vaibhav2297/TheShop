using FluentAssertions;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.ApplyVariantImages;
using Xunit;

namespace TheShop.Application.Tests.Features.Products;

public class ApplyVariantImagesTests
{
    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_ExplicitBulkTargets_AppendsMissingWithoutChangingOtherCovers()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var cover = Guid.NewGuid();
        var added = Guid.NewGuid();
        IReadOnlyList<VariantInput> variants =
        [
            new(first, [], "A", null, null, true, [cover]),
            new(second, [], "B", null, null, true, []),
            new(third, [], "C", null, null, true, [])
        ];

        var result = await new ApplyVariantImagesHandler().Handle(
            new(variants, [first, second], [added, cover], true, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].ImageIds.Should().Equal(cover, added);
        result.Value[1].ImageIds.Should().Equal(added, cover);
        result.Value[2].ImageIds.Should().BeEmpty();
        variants[0].ImageIds.Should().Equal(cover);
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_ReplaceOneVariant_ReordersCoverWithoutChangingProductOrSibling()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var imageA = Guid.NewGuid();
        var imageB = Guid.NewGuid();
        IReadOnlyList<VariantInput> variants =
        [
            new(first, [], "A", null, null, true, [imageA, imageB]),
            new(second, [], "B", null, null, true, [imageA])
        ];

        var result = await new ApplyVariantImagesHandler().Handle(
            new(variants, [first], [imageB, imageA], false, []), CancellationToken.None);

        result.Value![0].ImageIds.Should().Equal(imageB, imageA);
        result.Value[1].ImageIds.Should().Equal(imageA);
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_BulkDeselect_RemovesOnlyDeselectedImageFromTargets()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var siblingOnly = Guid.NewGuid();
        IReadOnlyList<VariantInput> variants =
        [
            new(first, [], "A", null, null, true, [removed, kept]),
            new(second, [], "B", null, null, true, [siblingOnly, removed, kept]),
            new(unrelated, [], "C", null, null, true, [removed, kept])
        ];

        var result = await new ApplyVariantImagesHandler().Handle(
            new(variants, [first, second], [kept], true, [removed]), CancellationToken.None);

        result.Value![0].ImageIds.Should().Equal(kept);
        result.Value[1].ImageIds.Should().Equal(siblingOnly, kept);
        result.Value[2].ImageIds.Should().Equal(removed, kept);
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Validator_UnknownTargetAndDuplicateImage_RejectsRequest()
    {
        var image = Guid.NewGuid();
        var result = new ApplyVariantImagesQueryValidator().Validate(
            new ApplyVariantImagesQuery([], [Guid.NewGuid()], [image, image], false, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
