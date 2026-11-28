using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class HomepageBrandRepositoryTests
{
    private readonly IPostgrestClient _rpc = Substitute.For<IPostgrestClient>();
    private SupabaseHomepageBrandRepository Repository() => new(new Supabase.Client(
        Substitute.For<IGotrueClient<User, Session>>(),
        Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>(),
        Substitute.For<Supabase.Functions.Interfaces.IFunctionsClient>(), _rpc,
        Substitute.For<IStorageClient<Bucket, FileObject>>(),
        new Supabase.SupabaseOptions { AutoRefreshToken = false, AutoConnectRealtime = false }));
    private static readonly Guid[] Ids = [Guid.NewGuid(), Guid.NewGuid()];
    private static HomepageBrandSection Section() =>
        HomepageBrandSection.Create("Staff's section", "View All", "/brands", Ids, false);

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Save_UsesOneParameterizedRpcWithOrderedBrandsAndVisibility()
    {
        var repository = Repository();
        foreach (var create in new[] { true, false })
        {
            _rpc.ClearReceivedCalls();
            var result = await repository.SaveAsync(Section(), create, TestContext.Current.CancellationToken);
            result.IsSuccess.Should().BeTrue();
            var call = _rpc.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "Rpc");
            call.GetArguments()[0].Should().Be("save_homepage_brand_admin");
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(call.GetArguments()[1]));
            var fields = json.RootElement;
            fields.GetProperty("p_create").GetBoolean().Should().Be(create);
            fields.GetProperty("p_title").GetString().Should().Be("Staff's section");
            fields.GetProperty("p_button_label").GetString().Should().Be("View All");
            fields.GetProperty("p_button_link").GetString().Should().Be("/brands");
            fields.GetProperty("p_brand_ids").EnumerateArray().Select(x => x.GetGuid()).Should().Equal(Ids);
            fields.GetProperty("p_is_visible").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Save_MapsPermissionConflictMissingAndUnavailableFailures()
    {
        var repository = Repository();
        foreach (var (state, key) in new[]
        {
            ("42501", RbacErrorKeys.AccessDenied),
            ("23505", HomepageBrandErrorKeys.AlreadyExists),
            ("P0002", HomepageBrandErrorKeys.NotFound),
            ("P0001", HomepageBrandErrorKeys.BrandUnavailable),
            ("23514", HomepageBrandErrorKeys.SaveFailed)
        })
        {
            _rpc.Rpc("save_homepage_brand_admin", Arg.Any<object>())
                .ThrowsAsync(new PostgrestException($"{{\"code\":\"{state}\"}}"));
            (await repository.SaveAsync(Section(), true, TestContext.Current.CancellationToken)).Error.Should().Be(key);
        }
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Cancellation_BeforeAdminReadOrSave_DoesNotCallRpc()
    {
        var repository = Repository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        Func<Task> read = async () => await repository.GetForAdminAsync(cts.Token);
        Func<Task> save = async () => await repository.SaveAsync(Section(), true, cts.Token);
        await read.Should().ThrowAsync<OperationCanceledException>();
        await save.Should().ThrowAsync<OperationCanceledException>();
        _rpc.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "Rpc");
    }
}
