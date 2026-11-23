using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Unique local Supabase product with separate image objects and stable variant addresses.</summary>
internal sealed class VariantGalleryData : IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly string _prefix;
    internal Guid Id { get; } = Guid.NewGuid();
    internal string Name => $"e2e-gallery-{Id:N}";
    internal string Slug => Name;
    internal Guid[] Images { get; } = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
    internal Guid[] Values { get; } = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
    internal Guid[] Groups { get; } = [Guid.NewGuid(), Guid.NewGuid()];
    internal Guid[] Variants { get; } = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
    internal string[] VariantSlugs { get; } = ["red-small", "blue-small", "red-large", "blue-large"];
    internal bool[] Available { get; } = [true, true, false, true];
    internal bool AdminSaved { get; set; }
    internal string[] Skus => Enumerable.Range(0, 4).Select(i =>
        AdminSaved ? $"{Name}-{VariantSlugs[i].ToUpperInvariant()}" : $"{Name}-{i}").ToArray();
    internal string ImageKey(int index) => $"{_prefix}/{index}.png";
    internal string VariantUrl(int index) => TheShop.Web.Common.Routes.ProductVariant(Slug, VariantSlugs[index]);
    internal string ParentUrl => TheShop.Web.Common.Routes.ProductBySlug(Slug);

    private VariantGalleryData()
    {
        var api = new Uri(E2EEnvironment.Get("API_URL"));
        if (!api.IsLoopback) throw new InvalidOperationException("Gallery fixtures require local Supabase.");
        _prefix = $"products/e2e-gallery/{Id:N}";
        _client = new HttpClient { BaseAddress = api };
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        _client.DefaultRequestHeaders.Add("apikey", key);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    internal static async Task<VariantGalleryData> CreateAsync(bool published = true)
    {
        var data = new VariantGalleryData();
        try
        {
            var category = await data.FirstIdAsync("categories");
            var brand = await data.FirstIdAsync("brands");
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ1sAAAAASUVORK5CYII=");
            for (var i = 0; i < data.Images.Length; i++)
            {
                using var bytes = new ByteArrayContent([.. png, (byte)i]);
                bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                using var upload = await data._client.PostAsync("/storage/v1/object/product-images/" + data.ImageKey(i), bytes);
                upload.EnsureSuccessStatusCode();
            }
            await data.InsertAsync("products", new
            {
                id = data.Id,
                name = data.Name,
                sku = data.Name,
                currency = "CAD",
                category_id = category,
                brand_id = brand,
                is_published = published
            });
            await data.InsertAsync("product_images", data.Images.Select((id, i) => new
            {
                id,
                product_id = data.Id,
                object_key = data.ImageKey(i),
                position = i,
                is_primary = i == 0
            }).ToArray());
            await data.InsertAsync("product_option_types", data.Groups.Select((id, i) => new
            { id, product_id = data.Id, name = i == 0 ? "Colour" : "Size", position = i }).ToArray());
            var labels = new[] { "Red", "Blue", "Small", "Large" };
            await data.InsertAsync("product_option_values", data.Values.Take(4).Select((id, i) => new
            { id, option_type_id = data.Groups[i < 2 ? 0 : 1], value = labels[i], position = i < 2 ? i : i - 2 }).ToArray());
            for (var i = 0; i < data.Variants.Length; i++)
            {
                await data.InsertAsync("product_variants", new
                {
                    id = data.Variants[i],
                    product_id = data.Id,
                    sku = data.Skus[i],
                    original_price = 20m + i * 5m,
                    is_available = i != 2,
                    position = i,
                    image_id = (Guid?)null
                });
                var colour = i is 1 or 3 ? 1 : 0;
                var size = i >= 2 ? 3 : 2;
                await data.InsertAsync("product_variant_option_values", new[]
                {
                    new { variant_id = data.Variants[i], option_value_id = data.Values[colour] },
                    new { variant_id = data.Variants[i], option_value_id = data.Values[size] }
                });
            }
            await data.InsertAsync("product_routes", new { slug = data.Slug, product_id = data.Id });
            await data.InsertAsync("product_variant_routes", data.Variants.Select((id, i) => new
            { product_slug = data.Slug, slug = data.VariantSlugs[i], variant_id = id }).ToArray());
            await data.InsertAsync("product_variant_images", new[]
            {
                new { product_id = data.Id, variant_id = data.Variants[0], image_id = data.Images[1], position = 0 },
                new { product_id = data.Id, variant_id = data.Variants[0], image_id = data.Images[2], position = 1 },
                new { product_id = data.Id, variant_id = data.Variants[0], image_id = data.Images[5], position = 2 },
                new { product_id = data.Id, variant_id = data.Variants[0], image_id = data.Images[6], position = 3 },
                new { product_id = data.Id, variant_id = data.Variants[0], image_id = data.Images[7], position = 4 },
                new { product_id = data.Id, variant_id = data.Variants[1], image_id = data.Images[3], position = 0 },
                new { product_id = data.Id, variant_id = data.Variants[2], image_id = data.Images[4], position = 0 }
            });
            return data;
        }
        catch { await data.DisposeAsync(); throw; }
    }

    private async Task<Guid> FirstIdAsync(string table)
    {
        var rows = await _client.GetFromJsonAsync<JsonElement>($"/rest/v1/{table}?select=id&limit=1");
        return rows[0].GetProperty("id").GetGuid();
    }

    private async Task InsertAsync(string table, object row)
    {
        using var response = await _client.PostAsJsonAsync("/rest/v1/" + table, row);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Local {table} fixture failed: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    internal async Task PatchAsync(string table, string filter, object change)
    {
        using var response = await _client.PatchAsJsonAsync($"/rest/v1/{table}?{filter}", change);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Local {table} patch failed: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    internal Task AddExtraSizeAsync() => InsertAsync("product_option_values", new
    { id = Values[4], option_type_id = Groups[1], value = "XL", position = 2 });

    internal async Task DeleteAsync(string table, string filter)
    {
        using var response = await _client.DeleteAsync($"/rest/v1/{table}?{filter}");
        response.EnsureSuccessStatusCode();
    }

    internal async Task<JsonElement> RowsAsync(string table, string filter) =>
        await _client.GetFromJsonAsync<JsonElement>($"/rest/v1/{table}?select=*&{filter}");

    public async ValueTask DisposeAsync()
    {
        using var removed = await _client.DeleteAsync($"/rest/v1/products?id=eq.{Id}");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/storage/v1/object/product-images")
        { Content = JsonContent.Create(new { prefixes = Enumerable.Range(0, Images.Length).Select(ImageKey).ToArray() }) };
        using var storage = await _client.SendAsync(request);
        _client.Dispose();
    }
}
