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

/// <summary>Reads the RLS-protected singleton and saves it through an authorized RPC.</summary>
public sealed class SupabaseHomepageCategoryRepository(Supabase.Client client) : IHomepageCategoryRepository
{
    /// <inheritdoc/>
    public async Task<HomepageCategorySection?> GetForAdminAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var rows = await client.Rpc<List<HomepageCategoryRecord>>("get_homepage_category_admin", null);
            return rows?.SingleOrDefault()?.ToDomain();
        }
        catch (PostgrestException ex) when (SqlState(ex) == "42501")
        {
            throw new DomainException(RbacErrorKeys.AccessDenied);
        }
    }

    /// <inheritdoc/>
    public async Task<Result> SaveAsync(HomepageCategorySection section, bool create,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await client.Rpc("save_homepage_category_admin", new
            {
                p_create = create,
                p_title = section.Title,
                p_button_label = section.ButtonLabel,
                p_button_link = section.ButtonLink,
                p_category_ids = section.CategoryIds,
                p_is_visible = section.IsVisible
            });
            return Result.Ok();
        }
        catch (PostgrestException ex)
        {
            return Result.Fail(SqlState(ex) switch
            {
                "42501" => RbacErrorKeys.AccessDenied,
                "23505" => HomepageCategoryErrorKeys.AlreadyExists,
                "P0002" => HomepageCategoryErrorKeys.NotFound,
                "P0001" => HomepageCategoryErrorKeys.CategoryUnavailable,
                _ => HomepageCategoryErrorKeys.SaveFailed
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail(HomepageCategoryErrorKeys.SaveFailed); }
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
