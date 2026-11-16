using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryForAdmin;
using TheShop.Application.Features.Roles;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HomepageCategoryAuthorizationTests
{
    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Save_CreateAndUpdateRequireViewAndMatchingActionBeforeHandler()
    {
        foreach (var create in new[] { true, false })
            foreach (var view in new[] { true, false })
                foreach (var action in new[] { true, false })
                {
                    var user = Substitute.For<ICurrentUserService>();
                    user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
                    user.HasPermission(create ? PermissionCatalogue.Homepage.CreateCode : PermissionCatalogue.Homepage.EditCode)
                        .Returns(action);
                    var reachedHandler = false;
                    async Task<Result> Next(CancellationToken _)
                    {
                        reachedHandler = true;
                        return await Task.FromResult(Result.Ok());
                    }
                    var result = create
                        ? await new AuthorizationBehavior<CreateHomepageCategoryCommand, Result>(user)
                            .Handle(new(" ", " ", " ", [], true), Next, TestContext.Current.CancellationToken)
                        : await new AuthorizationBehavior<UpdateHomepageCategoryCommand, Result>(user)
                            .Handle(new(" ", " ", " ", [], true), Next, TestContext.Current.CancellationToken);
                    result.IsSuccess.Should().Be(view && action);
                    reachedHandler.Should().Be(view && action);
                    if (!view || !action) result.Error.Should().Be(RbacErrorKeys.AccessDenied);
                }
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task AdminAndChoicesQueries_RequireHomepageView()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(false);
        var admin = await new AuthorizationBehavior<GetHomepageCategoryForAdminQuery, Result<HomepageCategoryDto?>>(user)
            .Handle(new(), _ => Task.FromResult(Result.Ok<HomepageCategoryDto?>(null)),
                TestContext.Current.CancellationToken);
        var choices = await new AuthorizationBehavior<GetHomepageCategoryChoicesQuery, Result<IReadOnlyList<CategoryDto>>>(user)
            .Handle(new(), _ => Task.FromResult(Result.Ok<IReadOnlyList<CategoryDto>>([])),
                TestContext.Current.CancellationToken);
        admin.Error.Should().Be(RbacErrorKeys.AccessDenied);
        choices.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }
}
