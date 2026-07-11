using FluentAssertions;
using TheShop.Application.Common.Models;
using Xunit;

namespace TheShop.Application.Tests.Common.Models;

/// <summary>
/// Tests for <see cref="PaginationRequest"/> — the requested page position/size behind the
/// catalogue's "12 products per page" constraint and pagination controls (FR-8, AC-8).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class PaginationRequestTests
{
    // =========================================================================
    // Normalized
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [Trait("Feature", "product-catalogue")]
    public void Normalized_WhenPageIsBelowOne_ClampsPageToOne(int page)
    {
        var request = new PaginationRequest(page, 12).Normalized(48);

        request.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Normalized_WhenPageSizeExceedsMax_ClampsPageSizeToMax()
    {
        var request = new PaginationRequest(1, PageSize: 100).Normalized(48);

        request.PageSize.Should().Be(48);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Feature", "product-catalogue")]
    public void Normalized_WhenPageSizeIsBelowOne_ClampsPageSizeToOne(int pageSize)
    {
        var request = new PaginationRequest(1, pageSize).Normalized(48);

        request.PageSize.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Normalized_WithValidPageAndPageSize_LeavesValuesUnchanged()
    {
        // AC-8's 12-per-page default must survive normalization untouched.
        var request = new PaginationRequest(2, 12).Normalized(48);

        request.Page.Should().Be(2);
        request.PageSize.Should().Be(12);
    }

    // =========================================================================
    // Skip / ToInclusiveRange
    // =========================================================================

    [Theory]
    [InlineData(1, 12, 0)]
    [InlineData(2, 12, 12)]
    [InlineData(3, 12, 24)]
    [Trait("Feature", "product-catalogue")]
    public void Skip_ComputesOffsetForThePage(int page, int pageSize, int expectedSkip)
    {
        var request = new PaginationRequest(page, pageSize);

        request.Skip.Should().Be(expectedSkip);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToInclusiveRange_ForFirstPage_ReturnsZeroBasedInclusiveRange()
    {
        var request = new PaginationRequest(1, 12);

        request.ToInclusiveRange().Should().Be((0, 11));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToInclusiveRange_ForSecondPage_ReturnsNextZeroBasedInclusiveRange()
    {
        var request = new PaginationRequest(2, 12);

        request.ToInclusiveRange().Should().Be((12, 23));
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: Normalized_WithValidPageAndPageSize_LeavesValuesUnchanged,
//        Normalized_WhenPageSizeExceedsMax_ClampsPageSizeToMax, Skip_*, ToInclusiveRange_*
