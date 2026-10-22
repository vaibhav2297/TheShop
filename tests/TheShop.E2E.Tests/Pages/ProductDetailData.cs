using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Pages;

internal sealed class ProductDetailData : IAsyncDisposable
{
    private readonly HttpClient _client;
    internal Guid Id { get; } = Guid.NewGuid();
    internal string Name { get; }
    internal Guid[] Images { get; }
    internal Guid[] Values { get; } = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
    internal Guid[] Groups { get; } = [Guid.NewGuid(), Guid.NewGuid()];
    internal string ImageKey { get; }
    internal int Count => Images.Length;

    private ProductDetailData(int images)
    {
        Name = $"e2e-detail-{Id:N}";
        Images = Enumerable.Range(0, images).Select(_ => Guid.NewGuid()).ToArray();
        ImageKey = $"products/e2e-detail/{Id:N}/photo.png";
        var api = new Uri(E2EEnvironment.Get("API_URL"));
        if (!api.IsLoopback) throw new InvalidOperationException("Product-detail fixtures require local Supabase.");
        _client = new HttpClient { BaseAddress = api };
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        _client.DefaultRequestHeaders.Add("apikey", key);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    internal static async Task<ProductDetailData> CreateAsync(int images = 8, bool variants = true,
        bool published = true, bool content = true, bool equal = false, bool primary = true, bool broken = false)
    {
        var data = new ProductDetailData(images);
        try
        {
            var category = await data.FirstIdAsync("categories");
            var brand = await data.FirstIdAsync("brands");
            if (images > 0 && !broken)
            {
                using var png = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ1sAAAAASUVORK5CYII="));
                png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                using var upload = await data._client.PostAsync("/storage/v1/object/product-images/" + data.ImageKey, png);
                upload.EnsureSuccessStatusCode();
            }
            await data.InsertAsync("products", new
            {
                id = data.Id,
                name = data.Name,
                sku = data.Name,
                description = content ? "<p>Fresh peach product details.</p>" : "   ",
                original_price = variants ? (decimal?)null : 30m,
                sale_price = variants ? (decimal?)null : 25m,
                currency = "CAD",
                category_id = category,
                brand_id = brand,
                is_published = published,
                image_path = (string?)null
            });
            if (images > 0) await data.InsertAsync("product_images", data.Images.Select((id, i) => new
            { id, product_id = data.Id, object_key = data.ImageKey, position = i == 0 && primary ? 20 : i, is_primary = i == 0 && primary }).ToArray());
            if (content) await data.InsertAsync("product_specifications", new
            { id = Guid.NewGuid(), product_id = data.Id, name = "Capacity", value = "2 ml", position = 0 });
            if (variants)
            {
                await data.InsertAsync("product_option_types", data.Groups.Select((id, i) => new
                { id, product_id = data.Id, name = i == 0 ? "Flavour" : "Size", position = i }).ToArray());
                await data.InsertAsync("product_option_values", data.Values.Select((id, i) => new
                { id, option_type_id = data.Groups[i / 2], value = new[] { "Peach", "Mango", "Small", "Large" }[i], position = i % 2 }).ToArray());
                for (var i = 0; i < 4; i++)
                {
                    var variant = Guid.NewGuid();
                    var target = i == 2 || images == 0 ? (Guid?)null : data.Images[Math.Min(new[] { 1, 5, 0, 7 }[i], images - 1)];
                    await data.InsertAsync("product_variants", new
                    {
                        id = variant,
                        product_id = data.Id,
                        sku = data.Name + "-" + i,
                        original_price = equal ? 25m : new[] { 30m, 25m, 30m, 35m }[i],
                        sale_price = !equal && i == 0 ? 20m : (decimal?)null,
                        is_available = true,
                        image_id = target,
                        position = i
                    });
                    await data.InsertAsync("product_variant_option_values", new[] {
                        new { variant_id = variant, option_value_id = data.Values[i / 2] },
                        new { variant_id = variant, option_value_id = data.Values[2 + i % 2] } });
                }
            }
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
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Local {table} fixture failed: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    internal async Task<JsonElement> PublicRowsAsync(string table, string filter, string? token = null)
    {
        using var client = new HttpClient { BaseAddress = _client.BaseAddress };
        var anon = E2EEnvironment.Get("ANON_KEY");
        client.DefaultRequestHeaders.Add("apikey", anon);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? anon);
        using var response = await client.GetAsync($"/rest/v1/{table}?select=*&{filter}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    internal async Task<int> ChildCountAsync(string table)
    {
        var rows = await _client.GetFromJsonAsync<JsonElement>($"/rest/v1/{table}?select=id&product_id=eq.{Id}");
        return rows.GetArrayLength();
    }
    public async ValueTask DisposeAsync()
    {
        using var removed = await _client.DeleteAsync($"/rest/v1/products?id=eq.{Id}");
        if (Count > 0)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, "/storage/v1/object/product-images")
            { Content = JsonContent.Create(new { prefixes = new[] { ImageKey } }) };
            using var storage = await _client.SendAsync(request);
        }
        _client.Dispose();
    }
}
