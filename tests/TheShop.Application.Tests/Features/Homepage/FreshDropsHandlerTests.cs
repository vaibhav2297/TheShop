using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;
using TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsForAdmin;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class FreshDropsHandlerTests
{
    private static CreateFreshDropsCommand Create(IReadOnlyList<Guid>? ids = null) =>
        new("Fresh Drops", "View All Products", "/products", FreshDropsSelectionMode.Newest, ids ?? [], true);

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Create_ValidNewest_SavesSingletonWithNoManualIds()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        repository.SaveAsync(Arg.Any<FreshDrops>(), true, Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var result = await new CreateFreshDropsHandler(repository).Handle(Create([Guid.NewGuid()]), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).SaveAsync(Arg.Is<FreshDrops>(drops => drops.Id == 1 &&
            drops.SelectionMode == FreshDropsSelectionMode.Newest && drops.ManualProductIds.Count == 0), true,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Update_ManualHidden_PreservesExplicitOrder()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        repository.SaveAsync(Arg.Any<FreshDrops>(), false, Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var command = new UpdateFreshDropsCommand("Changed", null, null, FreshDropsSelectionMode.Manual,
            [second, first], false);
        var result = await new UpdateFreshDropsHandler(repository).Handle(command, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).SaveAsync(Arg.Is<FreshDrops>(drops => drops.Title == "Changed" &&
            drops.ButtonLink == null && !drops.IsVisible && drops.ManualProductIds.SequenceEqual(new[] { second, first })),
            false, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Create_InvalidDomainInput_DoesNotWrite()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        var invalid = new CreateFreshDropsCommand(" ", null, null, FreshDropsSelectionMode.Manual, [], true);
        var result = await new CreateFreshDropsHandler(repository).Handle(invalid, TestContext.Current.CancellationToken);
        result.Error.Should().Be(FreshDropsErrorKeys.TitleRequired);
        await repository.DidNotReceive().SaveAsync(Arg.Any<FreshDrops>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Update_RepositoryConflict_PropagatesFailure()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        repository.SaveAsync(Arg.Any<FreshDrops>(), false, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(FreshDropsErrorKeys.NotFound));
        var result = await new UpdateFreshDropsHandler(repository).Handle(
            new("Fresh Drops", null, null, FreshDropsSelectionMode.Newest, [], true), TestContext.Current.CancellationToken);
        result.Error.Should().Be(FreshDropsErrorKeys.NotFound);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Read_Absent_ReturnsSuccessfulNull()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        var result = await new GetFreshDropsForAdminHandler(repository).Handle(new(), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Read_Existing_MapsSavedOrderAndVisibility()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        repository.GetAsync(Arg.Any<CancellationToken>()).Returns(FreshDrops.Create("Saved", null, null,
            FreshDropsSelectionMode.Manual, [second, first], false));
        var result = await new GetFreshDropsForAdminHandler(repository).Handle(new(), TestContext.Current.CancellationToken);
        result.Value!.Title.Should().Be("Saved");
        result.Value.ManualProductIds.Should().Equal(second, first);
        result.Value.IsVisible.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Read_DeniedRepositoryCall_DoesNotPretendConfigurationAbsent()
    {
        var repository = Substitute.For<IFreshDropsRepository>();
        repository.GetAsync(Arg.Any<CancellationToken>()).Returns<FreshDrops?>(_ => throw new DomainException("AccessDenied"));
        var result = await new GetFreshDropsForAdminHandler(repository).Handle(new(), TestContext.Current.CancellationToken);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("AccessDenied");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Products_SelectedIds_RestoresOrderAndSkipsUnpublishedMissingCards()
    {
        var reader = Substitute.For<IFreshDropsProductReader>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var missing = Guid.NewGuid();
        reader.GetAsync(Arg.Any<IReadOnlyList<Guid>>(), null, 3, 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FreshDropsProductDto>>([
                new(first, "First", "Brand", 10, "CAD", null),
                new(second, "Second", "Brand", 20, "CAD", null)]));
        var result = await new GetFreshDropsProductsHandler(reader).Handle(
            new([second, missing, first], Limit: 3), TestContext.Current.CancellationToken);
        result.Value!.Select(card => card.Id).Should().Equal(second, first);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Products_Search_ForwardsPagingAndReturnsReaderCards()
    {
        var reader = Substitute.For<IFreshDropsProductReader>();
        var card = new FreshDropsProductDto(Guid.NewGuid(), "Pod", "Brand", 20, "CAD", null);
        reader.GetAsync(null, "brand", 24, 24, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FreshDropsProductDto>>([card]));
        var result = await new GetFreshDropsProductsHandler(reader).Handle(
            new(Search: "brand", Limit: 24, Offset: 24), TestContext.Current.CancellationToken);
        result.Value.Should().ContainSingle().Which.Should().Be(card);
    }
}
