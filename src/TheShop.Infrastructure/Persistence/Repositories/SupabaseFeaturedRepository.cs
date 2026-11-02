using System.Text.Json;
using Supabase.Postgrest.Exceptions;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>Persists complete Featured rows through permission-checked RPCs.</summary>
public sealed class SupabaseFeaturedRepository(Supabase.Client client) : IFeaturedRepository
{
    /// <inheritdoc/>
    public async Task<FeaturedSection?> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var rows = await client.Rpc<List<FeaturedSectionRecord>>("get_featured_admin", null);
            cancellationToken.ThrowIfCancellationRequested();
            return rows?.SingleOrDefault()?.ToDomain();
        }
        catch (PostgrestException ex) when (SqlState(ex) == "42501")
        {
            throw new DomainException(RbacErrorKeys.AccessDenied);
        }
    }

    /// <inheritdoc/>
    public Task<Result> AddAsync(FeaturedSection section, CancellationToken cancellationToken)
        => SaveAsync(section, true, cancellationToken);

    /// <inheritdoc/>
    public Task<Result> UpdateAsync(FeaturedSection section, CancellationToken cancellationToken)
        => SaveAsync(section, false, cancellationToken);

    private async Task<Result> SaveAsync(FeaturedSection section, bool create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await client.Rpc("save_featured_admin", new
            {
                p_create = create,
                p_title = section.Title,
                p_tile1_label = section.Tile1.Label,
                p_tile1_title = section.Tile1.Title,
                p_tile1_button_label = section.Tile1.ButtonLabel,
                p_tile1_button_link = section.Tile1.ButtonLink,
                p_tile1_image_key = section.Tile1.ImageKey,
                p_tile2_label = section.Tile2.Label,
                p_tile2_title = section.Tile2.Title,
                p_tile2_button_label = section.Tile2.ButtonLabel,
                p_tile2_button_link = section.Tile2.ButtonLink,
                p_tile2_image_key = section.Tile2.ImageKey,
                p_is_visible = section.IsVisible
            });
            return Result.Ok();
        }
        catch (PostgrestException ex)
        {
            return Result.Fail(SqlState(ex) switch
            {
                "42501" => RbacErrorKeys.AccessDenied,
                "23505" => FeaturedErrorKeys.AlreadyExists,
                "P0002" => FeaturedErrorKeys.NotFound,
                "22023" => FeaturedErrorKeys.ImageUnavailable,
                "23514" or "23502" => FeaturedErrorKeys.SaveFailed,
                _ => FeaturedErrorKeys.SaveUnconfirmed
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail(FeaturedErrorKeys.SaveUnconfirmed); }
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
