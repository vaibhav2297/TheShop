using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

public class VariantImageAssignmentTests
{
    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Constructor_OrderedUniqueImages_KeepsFirstAsCover()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var assignment = new VariantImageAssignment([second, first]);

        assignment.ImageIds.Should().Equal(second, first);
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Constructor_DuplicateImage_RejectsAssignment()
    {
        var image = Guid.NewGuid();

        var act = () => new VariantImageAssignment([image, image]);

        act.Should().Throw<DuplicateVariantImageException>();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void AppendMissing_ExistingAndRepeatedImages_PreservesCoverAndAppendsOnce()
    {
        var cover = Guid.NewGuid();
        var newImage = Guid.NewGuid();
        var assignment = new VariantImageAssignment([cover]);

        var appended = assignment.AppendMissing([newImage, cover, newImage]);

        assignment.ImageIds.Should().Equal(cover);
        appended.ImageIds.Should().Equal(cover, newImage);
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Remove_SelectedImage_PreservesRemainingOrder()
    {
        var first = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var last = Guid.NewGuid();
        var assignment = new VariantImageAssignment([first, removed, last]);

        var updated = assignment.Remove([removed]);

        assignment.ImageIds.Should().Equal(first, removed, last);
        updated.ImageIds.Should().Equal(first, last);
    }
}
