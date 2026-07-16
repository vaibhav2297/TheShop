using FluentAssertions;
using MediatR;
using NSubstitute;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Roles;
using Xunit;

namespace TheShop.Application.Tests.Common.Behaviors;

/// <summary>
/// Tests for <see cref="AuthorizationBehavior{TRequest, TResponse}"/> — the MediatR pipeline
/// behavior that enforces <see cref="RequiresPermissionAttribute"/> on every command and query
/// by reading the current principal's permission claims via
/// <see cref="ICurrentUserService.HasPermission"/>. Claims are minted into the access token by
/// the database's custom access token hook, so the check is local and its staleness is bounded
/// by the token TTL; Supabase RLS remains the authoritative boundary.
/// </summary>
public class AuthorizationBehaviorTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    // =========================================================================
    // Test doubles — fake MediatR requests
    // =========================================================================

    [RequiresPermission("orders.refund")]
    private sealed record ProtectedCommand : IRequest<Result>;

    [RequiresPermission("orders.view")]
    private sealed record ProtectedQuery : IRequest<Result<string>>;

    private sealed record UnprotectedCommand : IRequest<Result>;

    private static RequestHandlerDelegate<Result> NextReturns(Result result) =>
        (_) => Task.FromResult(result);

    private static RequestHandlerDelegate<Result<string>> NextReturns(Result<string> result) =>
        (_) => Task.FromResult(result);

    // =========================================================================
    // Permitted request passes through to the handler
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenUserHoldsTheRequiredPermission_DelegatesToNext()
    {
        _currentUser.HasPermission("orders.refund").Returns(true);
        var behavior = new AuthorizationBehavior<ProtectedCommand, Result>(_currentUser);

        var result = await behavior.Handle(
            new ProtectedCommand(), NextReturns(Result.Ok()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // =========================================================================
    // Missing permission short-circuits with access-denied
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenUserLacksTheRequiredPermission_ReturnsAccessDeniedFailure()
    {
        _currentUser.HasPermission("orders.refund").Returns(false);
        _currentUser.HasPermission("orders.view").Returns(true); // holds a different permission

        var behavior = new AuthorizationBehavior<ProtectedCommand, Result>(_currentUser);

        var result = await behavior.Handle(
            new ProtectedCommand(), NextReturns(Result.Ok()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenPermissionMissingOnAGenericResultRequest_ReturnsTypedAccessDeniedFailure()
    {
        _currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var behavior = new AuthorizationBehavior<ProtectedQuery, Result<string>>(_currentUser);

        var result = await behavior.Handle(
            new ProtectedQuery(), NextReturns(Result.Ok("unreachable")), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenUserLacksPermission_DoesNotInvokeNext()
    {
        var nextCalled = false;
        _currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var behavior = new AuthorizationBehavior<ProtectedCommand, Result>(_currentUser);

        await behavior.Handle(new ProtectedCommand(), (_) =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Ok());
        }, CancellationToken.None);

        nextCalled.Should().BeFalse("hiding the control must never be the only protection — the pipeline itself must refuse the request");
    }

    // =========================================================================
    // Unauthenticated caller — a principal with no claims holds no permissions
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenCallerIsUnauthenticated_ReturnsAccessDeniedFailure()
    {
        // An anonymous principal carries zero permission claims (ICurrentUserService contract).
        _currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var behavior = new AuthorizationBehavior<ProtectedCommand, Result>(_currentUser);

        var result = await behavior.Handle(
            new ProtectedCommand(), NextReturns(Result.Ok()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    // =========================================================================
    // Requests with no [RequiresPermission] attribute skip the check entirely
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenRequestHasNoRequiresPermissionAttribute_SkipsTheCheckAndDelegates()
    {
        var behavior = new AuthorizationBehavior<UnprotectedCommand, Result>(_currentUser);

        var result = await behavior.Handle(
            new UnprotectedCommand(), NextReturns(Result.Ok()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _currentUser.DidNotReceive().HasPermission(Arg.Any<string>());
    }

    // =========================================================================
    // The current principal is consulted on every call — a token refresh that drops
    // the permission blocks the next attempt
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Handle_WhenClaimsChangeBetweenCalls_TheNextCallReflectsTheNewClaims()
    {
        var behavior = new AuthorizationBehavior<ProtectedCommand, Result>(_currentUser);

        _currentUser.HasPermission("orders.refund").Returns(true);
        var first = await behavior.Handle(new ProtectedCommand(), NextReturns(Result.Ok()), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // A revocation lands in the claims on the next token refresh — no sign-out required.
        _currentUser.HasPermission("orders.refund").Returns(false);
        var second = await behavior.Handle(new ProtectedCommand(), NextReturns(Result.Ok()), CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.Error.Should().Be(RbacErrorKeys.AccessDenied);
        _currentUser.Received(2).HasPermission("orders.refund");
    }
}
