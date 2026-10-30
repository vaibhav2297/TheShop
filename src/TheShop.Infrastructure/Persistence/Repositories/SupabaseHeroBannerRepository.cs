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

/// <summary>Uses explicitly authorized RPCs; unknown write outcomes never authorize image cleanup.</summary>
public sealed class SupabaseHeroBannerRepository(Supabase.Client client) : IHeroBannerRepository
{
    /// <inheritdoc/>
    public async Task<HeroBanner?> GetAsync(CancellationToken cancellationToken)
        => await LoadAsync("get_hero_banner_admin", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<HeroBanner?> GetForWriteAsync(bool create, CancellationToken cancellationToken)
        => await LoadAsync("get_hero_banner_for_write", new { p_create = create }, cancellationToken);

    private async Task<HeroBanner?> LoadAsync(string function, object? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var rows = await client.Rpc<List<HeroBannerRecord>>(function, parameters);
            return rows?.SingleOrDefault()?.ToDomain();
        }
        catch (PostgrestException ex) when (SqlState(ex) == "42501")
        {
            throw new DomainException(RbacErrorKeys.AccessDenied);
        }
    }

    /// <inheritdoc/>
    public Task<Result> AddAsync(HeroBanner banner, CancellationToken cancellationToken) =>
        SaveAsync(banner, true, cancellationToken);

    /// <inheritdoc/>
    public Task<Result> UpdateAsync(HeroBanner banner, CancellationToken cancellationToken) =>
        SaveAsync(banner, false, cancellationToken);

    private async Task<Result> SaveAsync(HeroBanner banner, bool create, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await client.Rpc("save_hero_banner_admin", new
            {
                p_create = create,
                p_heading = banner.Heading,
                p_description = banner.Description,
                p_button_label = banner.ButtonLabel,
                p_button_link = banner.ButtonLink,
                p_desktop_image_key = banner.DesktopImageKey,
                p_mobile_image_key = banner.MobileImageKey,
                p_is_visible = banner.IsVisible
            });
            return Result.Ok();
        }
        catch (PostgrestException ex)
        {
            return Result.Fail(SqlState(ex) switch
            {
                "42501" => RbacErrorKeys.AccessDenied,
                "23505" => HeroBannerErrorKeys.AlreadyExists,
                "P0002" => HeroBannerErrorKeys.NotFound,
                "22023" => HeroBannerErrorKeys.ImageUnavailable,
                "23514" or "23502" => HeroBannerErrorKeys.SaveFailed,
                _ => HeroBannerErrorKeys.SaveUnconfirmed
            });
        }
        catch { return Result.Fail(HeroBannerErrorKeys.SaveUnconfirmed); }
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

