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
using TheShop.Domain.ValueObjects;
using TheShop.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class FeaturedRepositoryTests
{
    private readonly IPostgrestClient _rpc = Substitute.For<IPostgrestClient>();
    private SupabaseFeaturedRepository Repository() => new(new Supabase.Client(
        Substitute.For<IGotrueClient<User, Session>>(),
        Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>(),
        Substitute.For<Supabase.Functions.Interfaces.IFunctionsClient>(), _rpc,
        Substitute.For<IStorageClient<Bucket, FileObject>>(),
        new Supabase.SupabaseOptions { AutoRefreshToken = false, AutoConnectRealtime = false }));
    private static FeaturedSection Section() => FeaturedSection.Create("Staff's section",
        FeaturedTile.Create("New", "One", "Shop", "/products", "first-key"),
        FeaturedTile.Create(null, "Two", null, null, "second-key"), false);

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Save_UsesOneParameterizedRpcContainingEntireOrderedConfiguration()
    {
        var repository = Repository();
        foreach (var create in new[] { true, false })
        {
            _rpc.ClearReceivedCalls();
            var result = create ? await repository.AddAsync(Section(), TestContext.Current.CancellationToken)
                : await repository.UpdateAsync(Section(), TestContext.Current.CancellationToken);
            result.IsSuccess.Should().BeTrue();
            var call = _rpc.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "Rpc");
            call.GetArguments()[0].Should().Be("save_featured_admin");
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(call.GetArguments()[1]));
            var fields = json.RootElement;
            fields.EnumerateObject().Should().HaveCount(13);
            fields.GetProperty("p_create").GetBoolean().Should().Be(create);
            fields.GetProperty("p_title").GetString().Should().Be("Staff's section");
            fields.GetProperty("p_tile1_label").GetString().Should().Be("New");
            fields.GetProperty("p_tile1_title").GetString().Should().Be("One");
            fields.GetProperty("p_tile1_button_label").GetString().Should().Be("Shop");
            fields.GetProperty("p_tile1_button_link").GetString().Should().Be("/products");
            fields.GetProperty("p_tile1_image_key").GetString().Should().Be("first-key");
            fields.GetProperty("p_tile2_title").GetString().Should().Be("Two");
            fields.GetProperty("p_tile2_label").ValueKind.Should().Be(JsonValueKind.Null);
            fields.GetProperty("p_tile2_button_label").ValueKind.Should().Be(JsonValueKind.Null);
            fields.GetProperty("p_tile2_button_link").ValueKind.Should().Be(JsonValueKind.Null);
            fields.GetProperty("p_tile2_image_key").GetString().Should().Be("second-key");
            fields.GetProperty("p_is_visible").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Save_MapsDatabaseFailuresAndUnknownCommitToDistinctResourceKeys()
    {
        var repository = Repository();
        foreach (var (state, key) in new[] { ("42501", RbacErrorKeys.AccessDenied), ("23505", FeaturedErrorKeys.AlreadyExists),
                     ("P0002", FeaturedErrorKeys.NotFound), ("22023", FeaturedErrorKeys.ImageUnavailable),
                     ("23514", FeaturedErrorKeys.SaveFailed), ("23502", FeaturedErrorKeys.SaveFailed),
                     ("XX000", FeaturedErrorKeys.SaveUnconfirmed) })
        {
            _rpc.Rpc("save_featured_admin", Arg.Any<object>()).ThrowsAsync(new PostgrestException($"{{\"code\":\"{state}\"}}"));
            (await repository.AddAsync(Section(), TestContext.Current.CancellationToken)).Error.Should().Be(key);
            (await repository.UpdateAsync(Section(), TestContext.Current.CancellationToken)).Error.Should().Be(key);
        }
        _rpc.Rpc("save_featured_admin", Arg.Any<object>()).ThrowsAsync(new IOException("lost response"));
        (await repository.UpdateAsync(Section(), TestContext.Current.CancellationToken)).Error.Should().Be(FeaturedErrorKeys.SaveUnconfirmed);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task CancellationBeforeReadOrSaveNeverCallsRpc()
    {
        var repository = Repository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        Func<Task> read = async () => await repository.GetAsync(cts.Token);
        Func<Task> add = async () => await repository.AddAsync(Section(), cts.Token);
        Func<Task> edit = async () => await repository.UpdateAsync(Section(), cts.Token);
        await read.Should().ThrowAsync<OperationCanceledException>();
        await add.Should().ThrowAsync<OperationCanceledException>();
        await edit.Should().ThrowAsync<OperationCanceledException>();
        _rpc.ReceivedCalls().Should().NotContain(c => c.GetMethodInfo().Name == "Rpc");
    }
}
