using FluentAssertions;
using MediatR;
using NSubstitute;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Roles;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Common.Behaviors;

[Trait("Feature", "role-based-access-control")]
public sealed class MultiPermissionAuthorizationBehaviorTests
{
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();

    [RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.CreateCode)]
    private sealed record AllCommand : IRequest<Result>;

    [RequiresAnyPermission(PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.CreateCode)]
    private sealed record AnyQuery : IRequest<Result<string>>;

    [RequiresAllPermissions("products.view", PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.CreateCode)]
    [RequiresAnyPermission(PermissionCatalogue.Homepage.EditCode, "products.delete")]
    private sealed record CombinedCommand : IRequest<Result>;

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public async Task Handle_AllPermissions_RequiresBoth(bool view, bool create, bool allowed)
    {
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(create);
        var behavior = new AuthorizationBehavior<AllCommand, Result>(_user);
        var expected = Result.Ok();
        var calls = 0;

        var result = await behavior.Handle(new AllCommand(), token =>
        {
            token.Should().Be(TestContext.Current.CancellationToken);
            calls++;
            return Task.FromResult(expected);
        }, TestContext.Current.CancellationToken);

        calls.Should().Be(allowed ? 1 : 0);
        if (allowed)
            result.Should().BeSameAs(expected);
        else
            result.Error.Should().Be(RbacErrorKeys.AccessDenied);
        result.IsSuccess.Should().Be(allowed);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Handle_AnyPermission_AcceptsEitherAndPreservesTypedResult(bool view, bool create, bool allowed)
    {
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(create);
        var behavior = new AuthorizationBehavior<AnyQuery, Result<string>>(_user);
        var expected = Result.Ok("permitted");
        var calls = 0;

        var result = await behavior.Handle(new AnyQuery(), _ =>
        {
            calls++;
            return Task.FromResult(expected);
        }, TestContext.Current.CancellationToken);

        calls.Should().Be(allowed ? 1 : 0);
        if (allowed)
            result.Should().BeSameAs(expected);
        else
        {
            result.Error.Should().Be(RbacErrorKeys.AccessDenied);
        }
        result.IsSuccess.Should().Be(allowed);
    }

    [Theory]
    [InlineData(false, true, true, true, true, false)]
    [InlineData(true, false, true, true, true, false)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(true, true, true, false, false, false)]
    [InlineData(true, true, true, true, false, true)]
    [InlineData(true, true, true, false, true, true)]
    [InlineData(true, true, true, true, true, true)]
    public async Task Handle_CombinedAttributes_RequiresEveryGroup(
        bool productsView, bool view, bool create, bool edit, bool delete, bool allowed)
    {
        _user.HasPermission(PermissionCatalogue.Products.View.Code).Returns(productsView);
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(create);
        _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(edit);
        _user.HasPermission(PermissionCatalogue.Products.Delete.Code).Returns(delete);
        var behavior = new AuthorizationBehavior<CombinedCommand, Result>(_user);
        var calls = 0;

        var result = await behavior.Handle(new CombinedCommand(), _ =>
        {
            calls++;
            return Task.FromResult(Result.Ok());
        }, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().Be(allowed);
        calls.Should().Be(allowed ? 1 : 0);
        if (!allowed)
            result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    [Fact]
    public async Task Handle_PermissionsRevokedBetweenCalls_ReevaluatesBothKindsOfGroup()
    {
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(true);
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
        var all = new AuthorizationBehavior<AllCommand, Result>(_user);
        var any = new AuthorizationBehavior<AnyQuery, Result<string>>(_user);

        (await all.Handle(new AllCommand(), _ => Task.FromResult(Result.Ok()),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        (await any.Handle(new AnyQuery(), _ => Task.FromResult(Result.Ok("ok")),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(false);
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(false);

        (await all.Handle(new AllCommand(), _ => throw new InvalidOperationException("Handler must not run."),
            TestContext.Current.CancellationToken)).Error.Should().Be(RbacErrorKeys.AccessDenied);
        (await any.Handle(new AnyQuery(), _ => throw new InvalidOperationException("Handler must not run."),
            TestContext.Current.CancellationToken)).Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Attribute_InvalidPermissionLists_AreRejected(bool all)
    {
        string[][] invalidLists = [[], [""], [" "], [null!], [PermissionCatalogue.Homepage.ViewCode, " "]];
        foreach (var codes in invalidLists)
        {
            Action construct = () => CreateAttribute(all, codes);
            construct.Should().Throw<ArgumentException>();
        }

        Action constructNull = () => CreateAttribute(all, null!);
        constructNull.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Attribute_PermissionCodes_AreAnImmutableSnapshot(bool all)
    {
        string[] codes = [PermissionCatalogue.Homepage.ViewCode];
        var permissions = CreateAttribute(all, codes);
        codes[0] = PermissionCatalogue.Homepage.CreateCode;

        permissions.Should().Equal(PermissionCatalogue.Homepage.ViewCode);
        Action mutate = () => ((IList<string>)permissions)[0] = PermissionCatalogue.Homepage.CreateCode;
        mutate.Should().Throw<NotSupportedException>();
    }

    private static IReadOnlyList<string> CreateAttribute(bool all, string[] codes) => all
        ? new RequiresAllPermissionsAttribute(codes).PermissionCodes
        : new RequiresAnyPermissionAttribute(codes).PermissionCodes;
}
