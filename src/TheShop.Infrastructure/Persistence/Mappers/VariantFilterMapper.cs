using TheShop.Application.Common.Filtering;
using TheShop.Application.Features.Products;
using TheShop.Domain.Enums;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

internal static class VariantFilterMapper
{
    public static IReadOnlyList<FilterGroupDto> ToVariantGroups(this IReadOnlyList<OptionTypeLookupRecord> types) =>
        [.. types.Where(type => ProductFilterKeys.IsVariantKey(type.Key) && type.Options.Count > 0)
            .Select(type => new FilterGroupDto(type.Key, ProductFilterKeys.VariantLabelKey,
                FilterKind.MultiSelect,
                [.. type.Options.Select(option => new FilterOptionDto(option.Value, option.Label, null))],
                null, type.Name))];
}
