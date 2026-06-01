using Blazored.LocalStorage;
using FluentAssertions;
using NSubstitute;
using Supabase.Gotrue;
using TheShop.Infrastructure.Auth;
using Xunit;

namespace TheShop.Infrastructure.Tests.Auth;

/// <summary>
/// Unit tests for <see cref="LocalStorageSessionPersistence"/>.
/// Verifies the session-persistence adapter that backs AC-10 (session survives browser
/// restart): the correct storage key is written, read, and deleted via
/// <see cref="ISyncLocalStorageService"/>.
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class LocalStorageSessionPersistenceTests
{
    private const string ExpectedKey = "shop.auth.session";

    private readonly ISyncLocalStorageService _storage = Substitute.For<ISyncLocalStorageService>();

    private LocalStorageSessionPersistence CreateSut() => new(_storage);

    // =========================================================================
    // SaveSession — writes session to the expected key (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void SaveSession_Always_CallsSetItemWithCorrectKey()
    {
        var sut = CreateSut();
        var session = new Session();

        sut.SaveSession(session);

        _storage.Received(1).SetItem(ExpectedKey, session);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void SaveSession_Always_PassesSessionObjectThrough()
    {
        var sut = CreateSut();
        var session = new Session { AccessToken = "tok-abc" };

        sut.SaveSession(session);

        _storage.Received(1).SetItem(ExpectedKey, Arg.Is<Session>(s => s.AccessToken == "tok-abc"));
    }

    // =========================================================================
    // DestroySession — removes session from storage (AC-11 sign-out)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void DestroySession_Always_CallsRemoveItemWithCorrectKey()
    {
        var sut = CreateSut();

        sut.DestroySession();

        _storage.Received(1).RemoveItem(ExpectedKey);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void DestroySession_CalledTwice_RemovesItemTwice()
    {
        // Idempotent behaviour — calling destroy twice (e.g. sign-out then page unload)
        // must not throw and must call RemoveItem twice.
        var sut = CreateSut();

        sut.DestroySession();
        sut.DestroySession();

        _storage.Received(2).RemoveItem(ExpectedKey);
    }

    // =========================================================================
    // LoadSession — returns null when key absent (AC-10 cold start)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void LoadSession_WhenKeyDoesNotExist_ReturnsNull()
    {
        _storage.ContainKey(ExpectedKey).Returns(false);
        var sut = CreateSut();

        var result = sut.LoadSession();

        result.Should().BeNull("no session is present in storage on a cold start");
    }

    // =========================================================================
    // LoadSession — returns stored session when key present (AC-10 warm start)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void LoadSession_WhenKeyExists_ReturnsDeserializedSession()
    {
        var stored = new Session { AccessToken = "tok-stored" };
        _storage.ContainKey(ExpectedKey).Returns(true);
        _storage.GetItem<Session>(ExpectedKey).Returns(stored);
        var sut = CreateSut();

        var result = sut.LoadSession();

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("tok-stored");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void LoadSession_WhenKeyExists_CallsGetItemWithCorrectKey()
    {
        var stored = new Session();
        _storage.ContainKey(ExpectedKey).Returns(true);
        _storage.GetItem<Session>(ExpectedKey).Returns(stored);
        var sut = CreateSut();

        sut.LoadSession();

        _storage.Received(1).GetItem<Session>(ExpectedKey);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void LoadSession_WhenKeyDoesNotExist_DoesNotCallGetItem()
    {
        _storage.ContainKey(ExpectedKey).Returns(false);
        var sut = CreateSut();

        sut.LoadSession();

        _storage.DidNotReceive().GetItem<Session>(Arg.Any<string>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10: LoadSession_WhenKeyExists_ReturnsDeserializedSession,
//         LoadSession_WhenKeyDoesNotExist_ReturnsNull,
//         SaveSession_Always_CallsSetItemWithCorrectKey
//         (session persistence adapter that keeps the user signed in across browser restarts)
// AC-11: DestroySession_Always_CallsRemoveItemWithCorrectKey
//         (session is removed from storage when the user signs out)
