using System.Text.Json;
using Supabase.Postgrest.Exceptions;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Domain.Exceptions;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the RLS-protected singleton through authorized RPCs.</summary>
public sealed class SupabaseFreshDropsRepository(Supabase.Client client) : IFreshDropsRepository
{
    /// <inheritdoc/>
    public async Task<FreshDrops?> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var rows = await client.Rpc<List<FreshDropsRecord>>("get_fresh_drops_admin", null);
            return rows?.SingleOrDefault()?.ToDomain();
        }
        catch (PostgrestException ex) when (SqlState(ex) == "42501")
        {
            throw new DomainException(RbacErrorKeys.AccessDenied);
        }
    }

    /// <inheritdoc/>
    public async Task<Result> SaveAsync(FreshDrops drops, bool create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await client.Rpc("save_fresh_drops_admin", new
            {
                p_create = create,
                p_title = drops.Title,
                p_button_label = drops.ButtonLabel,
                p_button_link = drops.ButtonLink,
                p_selection_mode = drops.SelectionMode == FreshDropsSelectionMode.Newest ? "newest" : "manual",
                p_manual_product_ids = drops.ManualProductIds,
                p_is_visible = drops.IsVisible
            });
            return Result.Ok();
        }
        catch (PostgrestException ex)
        {
            return Result.Fail(SqlState(ex) switch
            {
                "42501" => RbacErrorKeys.AccessDenied,
                "23505" => FreshDropsErrorKeys.AlreadyExists,
                "P0002" => FreshDropsErrorKeys.NotFound,
                "23514" or "23502" or "22023" => FreshDropsErrorKeys.SaveFailed,
                _ => FreshDropsErrorKeys.SaveFailed
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail(FreshDropsErrorKeys.SaveFailed); }
    }

    private static string? SqlState(PostgrestException exception)
    {
        try
        {
            using var document = JsonDocument.Parse(exception.Content ?? exception.Message);
            return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
