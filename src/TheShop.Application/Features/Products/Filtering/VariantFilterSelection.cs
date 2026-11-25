using TheShop.Application.Common.Filtering;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Filtering;

/// <summary>Reconciles database-issued variant tokens against choices visible to the caller.</summary>
public static class VariantFilterSelection
{
    /// <summary>Removes unavailable selections and combines repeated groups without changing fixed filters.</summary>
    public static VariantFilterSelectionDto Reconcile(
        IReadOnlyList<AppliedFilterDto> requested,
        IReadOnlyList<FilterGroupDto> groups)
    {
        var choices = groups.Where(group => ProductFilterKeys.IsVariantKey(group.Key))
            .ToDictionary(group => group.Key,
                group => group.Options.Select(option => option.Value).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var selected = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var removed = false;

        foreach (var filter in requested.Where(filter => ProductFilterKeys.IsVariantKey(filter.Key)))
        {
            if (!choices.TryGetValue(filter.Key, out var allowed))
            {
                removed = true;
                continue;
            }

            foreach (var value in filter.Values)
            {
                if (!allowed.Contains(value))
                {
                    removed = true;
                    continue;
                }

                if (!selected.TryGetValue(filter.Key, out var values))
                    selected[filter.Key] = values = new HashSet<string>(StringComparer.Ordinal);
                values.Add(value);
            }
        }

        return new VariantFilterSelectionDto(
            [.. selected.Select(pair => new AppliedFilterDto(pair.Key, [.. pair.Value]))], removed);
    }
}
