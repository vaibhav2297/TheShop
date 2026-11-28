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

/// <summary>Reads the RLS-protected singleton and saves through an authorized RPC.</summary>
public sealed class SupabaseHomepageBrandRepository(Supabase.Client client) : IHomepageBrandRepository
{
    /// <inheritdoc/>
    public async Task<HomepageBrandSection?> GetForAdminAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var rows = await client.Rpc<List<HomepageBrandRecord>>("get_homepage_brand_admin", null);
            return rows?.SingleOrDefault()?.ToDomain();
        }
        catch (PostgrestException ex) when (SqlState(ex) == "42501")
        {
            throw new DomainException(RbacErrorKeys.AccessDenied);
        }
    }

    /// <inheritdoc/>
    public async Task<Result> SaveAsync(HomepageBrandSection section, bool create,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await client.Rpc("save_homepage_brand_admin", new
            {
                p_create = create,
                p_title = section.Title,
                p_button_label = section.ButtonLabel,
                p_button_link = section.ButtonLink,
                p_brand_ids = section.BrandIds,
                p_is_visible = section.IsVisible
            });
            return Result.Ok();
        }
        catch (PostgrestException ex)
        {
            return Result.Fail(SqlState(ex) switch
            {
                "42501" => RbacErrorKeys.AccessDenied,
                "23505" => HomepageBrandErrorKeys.AlreadyExists,
                "P0002" => HomepageBrandErrorKeys.NotFound,
                "P0001" => HomepageBrandErrorKeys.BrandUnavailable,
                _ => HomepageBrandErrorKeys.SaveFailed
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail(HomepageBrandErrorKeys.SaveFailed); }
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
