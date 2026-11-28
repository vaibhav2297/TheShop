using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageBrand;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandForAdmin;
using TheShop.Application.Features.Roles;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HomepageBrandAuthorizationTests
{
    [Fact]
    [Trait("Feature", "008_homepage-brand")]
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
                    Task<Result> Next(CancellationToken _)
                    {
                        reachedHandler = true;
                        return Task.FromResult(Result.Ok());
                    }
                    var result = create
                        ? await new AuthorizationBehavior<CreateHomepageBrandCommand, Result>(user)
                            .Handle(new(" ", " ", " ", [], true), Next, TestContext.Current.CancellationToken)
                        : await new AuthorizationBehavior<UpdateHomepageBrandCommand, Result>(user)
                            .Handle(new(" ", " ", " ", [], true), Next, TestContext.Current.CancellationToken);
                    result.IsSuccess.Should().Be(view && action);
                    reachedHandler.Should().Be(view && action);
                    if (!view || !action) result.Error.Should().Be(RbacErrorKeys.AccessDenied);
                }
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task AdminAndChoicesQueries_RequireHomepageView()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(false);
        var admin = await new AuthorizationBehavior<GetHomepageBrandForAdminQuery, Result<HomepageBrandDto?>>(user)
            .Handle(new(), _ => Task.FromResult(Result.Ok<HomepageBrandDto?>(null)), TestContext.Current.CancellationToken);
        var choices = await new AuthorizationBehavior<GetHomepageBrandChoicesQuery, Result<IReadOnlyList<BrandDto>>>(user)
            .Handle(new(), _ => Task.FromResult(Result.Ok<IReadOnlyList<BrandDto>>([])), TestContext.Current.CancellationToken);
        admin.Error.Should().Be(RbacErrorKeys.AccessDenied);
        choices.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }
}
