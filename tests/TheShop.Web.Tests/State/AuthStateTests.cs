using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.State;

/// <summary>
/// Tests for <see cref="AuthState"/> client-side store.
/// Covers AC-10 (session persistence awareness) and AC-11 (sign-out state change).
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class AuthStateTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private static AuthSession BuildSession(Guid? userId = null) =>
        new(userId ?? Guid.NewGuid(),
            "user@example.com",
            "access-token",
            "refresh-token",
            DateTimeOffset.UtcNow.AddHours(1));

    // =========================================================================
    // IsAuthenticated reflects IAuthService.CurrentSession
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void IsAuthenticated_WhenCurrentSessionIsNull_ReturnsFalse()
    {
        _auth.CurrentSession.Returns((AuthSession?)null);
        var state = new AuthState(_auth);

        state.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void IsAuthenticated_WhenCurrentSessionIsPresent_ReturnsTrue()
    {
        _auth.CurrentSession.Returns(BuildSession());
        var state = new AuthState(_auth);

        state.IsAuthenticated.Should().BeTrue();
    }

    // =========================================================================
    // UserId and Email reflect the current session
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void UserId_WhenAuthenticated_ReturnsSessionUserId()
    {
        var id = Guid.NewGuid();
        _auth.CurrentSession.Returns(BuildSession(id));
        var state = new AuthState(_auth);

        state.UserId.Should().Be(id.ToString());
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void UserId_WhenNotAuthenticated_ReturnsNull()
    {
        _auth.CurrentSession.Returns((AuthSession?)null);
        var state = new AuthState(_auth);

        state.UserId.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Email_WhenAuthenticated_ReturnsSessionEmail()
    {
        _auth.CurrentSession.Returns(BuildSession());
        var state = new AuthState(_auth);

        state.Email.Should().Be("user@example.com");
    }

    // =========================================================================
    // OnChange fires when AuthStateChanged fires (AC-10, AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void OnChange_WhenAuthServiceFiresAuthStateChanged_IsRaised()
    {
        _auth.CurrentSession.Returns((AuthSession?)null);
        var state = new AuthState(_auth);

        var changeCount = 0;
        state.OnChange += () => changeCount++;

        // Simulate auth state change (e.g. sign-in or sign-out)
        _auth.AuthStateChanged += Raise.Event<Action>();

        changeCount.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void OnChange_WhenFiredMultipleTimes_RaisesEachTime()
    {
        _auth.CurrentSession.Returns((AuthSession?)null);
        var state = new AuthState(_auth);

        var changeCount = 0;
        state.OnChange += () => changeCount++;

        _auth.AuthStateChanged += Raise.Event<Action>();
        _auth.AuthStateChanged += Raise.Event<Action>();

        changeCount.Should().Be(2);
    }

    // =========================================================================
    // Dispose unsubscribes from auth events (AC-11 — no stale listeners)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Dispose_WhenCalled_UnsubscribesFromAuthStateChanged()
    {
        _auth.CurrentSession.Returns((AuthSession?)null);
        var state = new AuthState(_auth);

        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Dispose();

        // After dispose, no more change events should propagate.
        _auth.AuthStateChanged += Raise.Event<Action>();

        changeCount.Should().Be(0);
    }
}
