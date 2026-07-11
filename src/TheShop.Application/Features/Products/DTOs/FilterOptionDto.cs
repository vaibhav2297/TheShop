namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A single selectable option within a <see cref="FilterGroupDto"/>.
/// </summary>
public sealed record FilterOptionDto(string Value, string Label, int? Count);
