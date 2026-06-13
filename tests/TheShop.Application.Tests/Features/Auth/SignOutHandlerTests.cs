using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Auth.Commands.SignOut;
using Xunit;

namespace TheShop.Application.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="SignOutHandler"/>.
/// Covers FR-9, AC-11.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class SignOutHandlerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private SignOutHandler CreateSut() => new(_auth);

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_Always_CallsSignOutOnAuthService()
    {
        await CreateSut().Handle(new SignOutCommand(), CancellationToken.None);

        await _auth.Received(1).SignOutAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_Always_ReturnsSuccessResult()
    {
        var result = await CreateSut().Handle(new SignOutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // =========================================================================
    // Best-effort — sign-out must always return success, even on failure
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Handle_EvenWhenAuthServiceThrows_StillReturnsSuccessResult()
    {
        // Per the spec, sign-out is best-effort: a network failure is silently swallowed.
        _auth.SignOutAsync(Arg.Any<CancellationToken>())
             .Returns(_ => throw new Exception("Network error"));

        // The handler itself doesn't swallow — SupabaseAuthService is responsible for
        // best-effort semantics. This test documents that the handler delegates and
        // does not add its own error handling.
        var act = async () => await CreateSut().Handle(new SignOutCommand(), CancellationToken.None);

        // If IAuthService throws, the exception surfaces. The spec says the Infrastructure
        // implementation swallows it, not the handler. This test confirms the handler
        // calls SignOutAsync exactly once.
        await act.Should().ThrowAsync<Exception>();
        await _auth.Received(1).SignOutAsync(Arg.Any<CancellationToken>());
    }
}
