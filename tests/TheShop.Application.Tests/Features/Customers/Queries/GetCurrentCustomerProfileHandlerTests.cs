using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Customers.Queries;

/// <summary>
/// Tests for <see cref="GetCurrentCustomerProfileHandler"/> — the profile-menu name/email source.
/// </summary>
public class GetCurrentCustomerProfileHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();

    private GetCurrentCustomerProfileHandler CreateSut() => new(_currentUser, _customers);

    private static readonly Guid UserId = Guid.NewGuid();

    private static Customer BuildCustomer() =>
        Customer.Rehydrate(
            UserId,
            "Jane",
            "Doe",
            DateOfBirth.Create(new DateOnly(2000, 1, 1)),
            Email.Create("jane@example.com"),
            DateTimeOffset.UtcNow.AddDays(-30));

    [Fact]
    [Trait("Feature", "profile-menu")]
    public async Task Handle_WhenAuthenticatedCustomerExists_ReturnsProfile()
    {
        _currentUser.Id.Returns(UserId);
        _customers.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(BuildCustomer());

        var result = await CreateSut().Handle(
            new GetCurrentCustomerProfileQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Jane");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Email.Should().Be("jane@example.com");
    }

    [Fact]
    [Trait("Feature", "profile-menu")]
    public async Task Handle_WhenUnauthenticated_ReturnsAccountNotFound()
    {
        _currentUser.Id.Returns((Guid?)null);

        var result = await CreateSut().Handle(
            new GetCurrentCustomerProfileQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountNotFound);
        await _customers.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "profile-menu")]
    public async Task Handle_WhenCustomerRecordMissing_ReturnsAccountNotFound()
    {
        _currentUser.Id.Returns(UserId);
        _customers.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns((Customer?)null);

        var result = await CreateSut().Handle(
            new GetCurrentCustomerProfileQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrorKeys.AccountNotFound);
    }
}
