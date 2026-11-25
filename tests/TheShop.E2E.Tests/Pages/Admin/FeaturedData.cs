using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Pages.Admin;

internal sealed class FeaturedData : IDisposable
{
    private readonly HttpClient _client;
    internal string Title { get; } = $"e2e-featured-{Guid.NewGuid():N}";
    internal string Key1 { get; } = $"featured/{Guid.NewGuid()}/{Guid.NewGuid():N}.png";
    internal string Key2 { get; } = $"featured/{Guid.NewGuid()}/{Guid.NewGuid():N}.png";
    internal FeaturedData()
    {
        var api = new Uri(E2EEnvironment.Get("API_URL"));
        if (!api.IsLoopback) throw new InvalidOperationException("Featured fixtures require local Supabase.");
        _client = new HttpClient { BaseAddress = api };
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        _client.DefaultRequestHeaders.Add("apikey", key);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    internal async Task ResetAsync()
    {
        using var response = await _client.DeleteAsync("/rest/v1/featured_sections?id=eq.1");
        response.EnsureSuccessStatusCode();
    }

    internal async Task SeedAsync(byte[] artwork)
    {
        await ResetAsync();
        foreach (var key in new[] { Key1, Key2 })
        {
            using var content = new ByteArrayContent(artwork);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var upload = await _client.PostAsync("/storage/v1/object/featured-images/" + key, content);
            upload.EnsureSuccessStatusCode();
        }
        using var insert = await _client.PostAsJsonAsync("/rest/v1/featured_sections", new
        {
            id = 1,
            title = Title,
            tile1_label = "First label",
            tile1_title = "First saved tile",
            tile1_button_label = "First button",
            tile1_button_link = "/products",
            tile1_image_key = Key1,
            tile2_label = "Second label",
            tile2_title = "Second saved tile",
            tile2_button_label = "Second button",
            tile2_button_link = "https://example.test/second",
            tile2_image_key = Key2,
            is_visible = false
        });
        insert.EnsureSuccessStatusCode();
    }

    internal async Task<JsonElement> RowsAsync() => await _client.GetFromJsonAsync<JsonElement>("/rest/v1/featured_sections?select=*");

    internal static object RpcParameters(bool create, string title, string key1, string key2) => new
    {
        p_create = create,
        p_title = title,
        p_tile1_label = (string?)null,
        p_tile1_title = "Direct first",
        p_tile1_button_label = (string?)null,
        p_tile1_button_link = (string?)null,
        p_tile1_image_key = key1,
        p_tile2_label = (string?)null,
        p_tile2_title = "Direct second",
        p_tile2_button_label = (string?)null,
        p_tile2_button_link = (string?)null,
        p_tile2_image_key = key2,
        p_is_visible = true
    };

    internal async Task<(int Status, string Body)> AsStaffAsync(string token, object request)
    {
        using var client = new HttpClient { BaseAddress = _client.BaseAddress };
        client.DefaultRequestHeaders.Add("apikey", E2EEnvironment.Get("ANON_KEY"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PostAsJsonAsync("/rest/v1/rpc/save_featured_admin", request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    internal async Task<Guid> GrantActionAsync(string email, string action)
    {
        var customers = await _client.GetFromJsonAsync<JsonElement>($"/rest/v1/customers?select=id&email=eq.{Uri.EscapeDataString(email)}");
        var user = customers[0].GetProperty("id").GetGuid();
        var permission = await _client.GetFromJsonAsync<JsonElement>($"/rest/v1/permissions?select=id&code=eq.homepage.{action}");
        var role = Guid.NewGuid();
        await InsertAsync("roles", new { id = role, name_key = $"e2e-featured-{role:N}" });
        await InsertAsync("role_permissions", new { role_id = role, permission_id = permission[0].GetProperty("id").GetGuid() });
        await InsertAsync("user_roles", new { role_id = role, user_id = user });
        return role;
    }

    internal async Task RemoveRoleAsync(Guid role)
    {
        foreach (var target in new[] { $"user_roles?role_id=eq.{role}", $"role_permissions?role_id=eq.{role}", $"roles?id=eq.{role}" })
        {
            using var response = await _client.DeleteAsync("/rest/v1/" + target);
            response.EnsureSuccessStatusCode();
        }
    }
    private async Task InsertAsync(string table, object row)
    {
        using var response = await _client.PostAsJsonAsync("/rest/v1/" + table, row);
        response.EnsureSuccessStatusCode();
    }
    public void Dispose() => _client.Dispose();
}
