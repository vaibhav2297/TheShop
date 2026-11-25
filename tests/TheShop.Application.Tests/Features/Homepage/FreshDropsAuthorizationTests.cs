using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;
using TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

[Trait("Feature", "004_fresh-drops")]
public sealed class FreshDropsAuthorizationTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public async Task Handle_ValidCommand_OnlyViewAndMatchingActionCanSave(bool create, bool view, bool action)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
        user.HasPermission(create ? PermissionCatalogue.Homepage.CreateCode : PermissionCatalogue.Homepage.EditCode)
            .Returns(action);
        var repository = Substitute.For<IFreshDropsRepository>();
        var expected = Result.Ok();
        repository.SaveAsync(Arg.Any<FreshDrops>(), create, Arg.Any<CancellationToken>()).Returns(expected);
        var result = await Dispatch(create, "Fresh Drops", user, repository);

        result.IsSuccess.Should().Be(view && action);
        if (view && action)
        {
            result.Should().BeSameAs(expected);
            await repository.Received(1).SaveAsync(Arg.Is<FreshDrops>(drops => drops.Title == "Fresh Drops"),
                create, TestContext.Current.CancellationToken);
        }
        else
        {
            result.Error.Should().Be(RbacErrorKeys.AccessDenied);
            await repository.DidNotReceive().SaveAsync(Arg.Any<FreshDrops>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public async Task Handle_InvalidCommand_AuthorizesBothPermissionsBeforeValidation(bool create, bool view, bool action)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
        user.HasPermission(create ? PermissionCatalogue.Homepage.CreateCode : PermissionCatalogue.Homepage.EditCode)
            .Returns(action);
        var repository = Substitute.For<IFreshDropsRepository>();
        var result = await Dispatch(create, "", user, repository);

        result.IsFailure.Should().BeTrue();
        if (view && action)
        {
            result.Error.Should().Be(FreshDropsErrorKeys.TitleRequired);
            result.ValidationErrors.Should().Contain(error => error.PropertyName == "Title");
        }
        else
        {
            result.Error.Should().Be(RbacErrorKeys.AccessDenied);
            result.ValidationErrors.Should().BeEmpty();
        }
        await repository.DidNotReceive().SaveAsync(Arg.Any<FreshDrops>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private static Task<Result> Dispatch(bool create, string title, ICurrentUserService user, IFreshDropsRepository repository)
    {
        if (create)
        {
            var command = new CreateFreshDropsCommand(title, null, null, FreshDropsSelectionMode.Newest, [], true);
            var authorization = new AuthorizationBehavior<CreateFreshDropsCommand, Result>(user);
            var validation = new ValidationBehavior<CreateFreshDropsCommand, Result>([new CreateFreshDropsCommandValidator()]);
            var handler = new CreateFreshDropsHandler(repository);
            return authorization.Handle(command,
                token => validation.Handle(command, handlerToken => handler.Handle(command, handlerToken), token),
                TestContext.Current.CancellationToken);
        }

        var update = new UpdateFreshDropsCommand(title, null, null, FreshDropsSelectionMode.Newest, [], true);
        var updateAuthorization = new AuthorizationBehavior<UpdateFreshDropsCommand, Result>(user);
        var updateValidation = new ValidationBehavior<UpdateFreshDropsCommand, Result>([new UpdateFreshDropsCommandValidator()]);
        var updateHandler = new UpdateFreshDropsHandler(repository);
        return updateAuthorization.Handle(update,
            token => updateValidation.Handle(update, handlerToken => updateHandler.Handle(update, handlerToken), token),
            TestContext.Current.CancellationToken);
    }
}
