using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Pages;

internal sealed class VariantFilterData : IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly Dictionary<string, Guid> _products = [];
    private readonly Dictionary<string, Guid> _variants = [];
    internal string Token { get; } = Guid.NewGuid().ToString("N");
    internal Guid Brand { get; } = Guid.NewGuid();
    internal Guid Category { get; } = Guid.NewGuid();
    internal string Colour => "Colour " + Token;
    internal string Size => "Size " + Token;
    internal string ColourKey => "variant:" + Colour.ToLowerInvariant();
    internal string SizeKey => "variant:" + Size.ToLowerInvariant();
    internal string Name(string label) => "e2evf-" + Token + " " + label;

    private VariantFilterData()
    {
        var api = new Uri(E2EEnvironment.Get("API_URL"));
        if (!api.IsLoopback) throw new InvalidOperationException("Variant-filter fixtures require local Supabase.");
        _client = new HttpClient { BaseAddress = api };
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        _client.DefaultRequestHeaders.Add("apikey", key);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    internal static async Task<VariantFilterData> CreateAsync(bool variants = true, bool onlyMixed = false, int additional = 0)
    {
        var data = new VariantFilterData();
        try
        {
            await data.InsertAsync("brands", new { id = data.Brand, name = data.Name("Brand"), is_active = true });
            await data.InsertAsync("categories", new { id = data.Category, name = data.Name("Category"), is_active = true });
            await data.AddProductAsync("A mixed", variants: variants ? [("Red", "S", true, 10m), ("Blue", "L", false, 40m), ("Green", "M", true, 50m)] : []);
            if (variants) await data.RemoveVariantAsync("A mixed", 2);
            if (!onlyMixed)
            {
                await data.AddProductAsync("B blue", variants: variants ? [("Blue", "L", true, 30m)] : []);
                await data.AddProductAsync("C red", variants: variants ? [("Red", "L", true, 20m), ("Red", "XL", true, 25m)] : []);
                await data.AddProductAsync("D split", variants: variants ? [("Red", "S", true, 60m), ("Blue", "L", true, 70m)] : []);
            }
            await data.AddProductAsync("Plain", variants: []);
            await data.AddProductAsync("Hidden", published: false, variants: variants ? [("Secret", "Hidden", true, 99m)] : []);
            for (var i = 0; i < additional; i++)
                await data.AddProductAsync($"Z{i:D2}", variants: [(i == additional - 1 ? "Later" : "Red", "L", true, 20m)]);
            return data;
        }
        catch { await data.DisposeAsync(); throw; }
    }

    internal async Task AddProductAsync(string label, bool published = true,
        (string Colour, string Size, bool Active, decimal Price)[]? variants = null, string? groupName = null)
    {
        variants ??= [];
        var product = Guid.NewGuid(); _products.Add(label, product);
        await InsertAsync("products", new
        {
            id = product,
            name = Name(label),
            sku = "VF-" + Guid.NewGuid().ToString("N"),
            description = "<p>Variant filter fixture.</p>",
            category_id = Category,
            brand_id = Brand,
            is_published = published,
            currency = "CAD",
            original_price = variants.Length > 0 ? (decimal?)null : 15m,
        });
        if (variants.Length == 0) return;
        var colour = Guid.NewGuid(); var size = Guid.NewGuid();
        await InsertAsync("product_option_types", new[]
        {
            new { id = colour, product_id = product, name = groupName ?? Colour, position = 0 },
            new { id = size, product_id = product, name = Size, position = 1 },
        });
        var colours = variants.Select(v => v.Colour).Distinct().ToDictionary(v => v, _ => Guid.NewGuid());
        var sizes = variants.Select(v => v.Size).Distinct().ToDictionary(v => v, _ => Guid.NewGuid());
        await InsertAsync("product_option_values", colours.Select((v, i) => new { id = v.Value, option_type_id = colour, value = v.Key, position = i })
            .Concat(sizes.Select((v, i) => new { id = v.Value, option_type_id = size, value = v.Key, position = i })).ToArray());
        for (var i = 0; i < variants.Length; i++)
        {
            var variant = Guid.NewGuid(); _variants.Add(label + ":" + i, variant);
            var input = variants[i];
            await InsertAsync("product_variants", new
            {
                id = variant,
                product_id = product,
                sku = "VF-" + variant.ToString("N"),
                original_price = input.Price,
                is_available = input.Active,
                position = i,
            });
            await InsertAsync("product_variant_option_values", new[]
            {
                new { variant_id = variant, option_value_id = colours[input.Colour] },
                new { variant_id = variant, option_value_id = sizes[input.Size] },
            });
        }
    }

    internal Task RemoveVariantAsync(string label, int position) => DeleteAsync("product_variants", _variants[label + ":" + position]);
    internal Task RemoveProductAsync(string label) => DeleteAsync("products", _products[label]);
    internal async Task SetAvailableAsync(string label, int position, bool available)
    {
        using var response = await _client.PatchAsJsonAsync($"/rest/v1/product_variants?id=eq.{_variants[label + ":" + position]}", new { is_available = available });
        response.EnsureSuccessStatusCode();
    }

    private async Task InsertAsync(string table, object rows)
    {
        using var response = await _client.PostAsJsonAsync("/rest/v1/" + table, rows);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Local {table} fixture failed: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task DeleteAsync(string table, Guid id)
    {
        using var response = await _client.DeleteAsync($"/rest/v1/{table}?id=eq.{id}");
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var id in _products.Values) await DeleteAsync("products", id);
            await DeleteAsync("brands", Brand);
            await DeleteAsync("categories", Category);
        }
        finally { _client.Dispose(); }
    }
}
