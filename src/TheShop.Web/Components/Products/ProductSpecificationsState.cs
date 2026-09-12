using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Web.Components.Products;

/// <summary>
/// The specification editor's current rows, as raised to the product form. Rows keep the order
/// they are given in — nothing rearranges them — so <see cref="SpecificationInput.Position"/> is
/// simply each row's index at the moment this state was raised.
/// </summary>
/// <param name="Specifications">The current rows in display order.</param>
public sealed record ProductSpecificationsState(IReadOnlyList<SpecificationInput> Specifications);
