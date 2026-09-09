## Auth guard details

When the spec describes a feature that's reachable from an authenticated context, write these tests:

**Application layer (handler):**
```csharp
[Fact]
[Trait("Feature", "add-to-cart")]
public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedResult()
{
    _user.IsAuthenticated.Returns(false);
    var sut = CreateSut();

    var result = await sut.Handle(new SomeCommand(), CancellationToken.None);

    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Be("Unauthorized");
}
```

**Web layer (admin page):**
```csharp
[Fact]
[Trait("Feature", "add-to-cart")]
public void Render_WhenUserNotAuthenticated_RedirectsToLogin()
{
    // Set up unauthenticated AuthState
    // Render admin page
    // Assert NavigationManager redirected to /login
}
```

If the spec doesn't tell you which users are allowed, ask. Don't assume.

---
