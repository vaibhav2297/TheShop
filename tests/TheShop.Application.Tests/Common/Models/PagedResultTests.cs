using FluentAssertions;
using TheShop.Application.Common.Models;
using Xunit;

namespace TheShop.Application.Tests.Common.Models;

/// <summary>
/// Tests for <see cref="PagedResult{T}"/> — the reusable paging metadata behind the
/// catalogue's pagination (spec constraint: "A fixed number of products is shown per page:
/// 12 products per page", FR-8, AC-8).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class PagedResultTests
{
    // =========================================================================
    // TotalPages
    // =========================================================================

    [Theory]
    [InlineData(0, 12, 0)]
    [InlineData(1, 12, 1)]
    [InlineData(12, 12, 1)]
    [InlineData(13, 12, 2)]
    [InlineData(24, 12, 2)]
    [InlineData(25, 12, 3)]
    [Trait("Feature", "product-catalogue")]
    public void TotalPages_ComputesCeilingOfTotalCountOverPageSize(int totalCount, int pageSize, int expected)
    {
        var page = new PagedResult<string>([], 1, pageSize, totalCount);

        page.TotalPages.Should().Be(expected);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void TotalPages_WhenPageSizeIsZero_ReturnsZero()
    {
        // Defensive: guards against a division by zero in the ceiling computation.
        var page = new PagedResult<string>([], 1, 0, 10);

        page.TotalPages.Should().Be(0);
    }

    // =========================================================================
    // HasPrevious / HasNext
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void HasPrevious_WhenPageIsOne_ReturnsFalse()
    {
        var page = new PagedResult<string>([], 1, 12, 24);

        page.HasPrevious.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void HasPrevious_WhenPageIsGreaterThanOne_ReturnsTrue()
    {
        var page = new PagedResult<string>([], 2, 12, 24);

        page.HasPrevious.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void HasNext_WhenPageIsBeforeTheLastPage_ReturnsTrue()
    {
        var page = new PagedResult<string>([], 1, 12, 24);

        page.HasNext.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void HasNext_WhenPageIsTheLastPage_ReturnsFalse()
    {
        var page = new PagedResult<string>([], 2, 12, 24);

        page.HasNext.Should().BeFalse();
    }

    // =========================================================================
    // MapItems
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void MapItems_ProjectsEachItemPreservingPagingMetadata()
    {
        var page = new PagedResult<int>([1, 2, 3], 2, 12, 30);

        var mapped = page.MapItems(x => x.ToString());

        mapped.Items.Should().Equal("1", "2", "3");
        mapped.Page.Should().Be(2);
        mapped.PageSize.Should().Be(12);
        mapped.TotalCount.Should().Be(30);
    }

    // =========================================================================
    // Empty
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Empty_ReturnsPageWithNoItemsAndZeroTotalCount()
    {
        // AC-10: "no products match your filters" — the empty-page short-circuit.
        var request = new PaginationRequest(Page: 3, PageSize: 12);

        var page = PagedResult<string>.Empty(request);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.Page.Should().Be(3);
        page.PageSize.Should().Be(12);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: TotalPages_ComputesCeilingOfTotalCountOverPageSize, HasPrevious_*, HasNext_*
// AC-10: Empty_ReturnsPageWithNoItemsAndZeroTotalCount
