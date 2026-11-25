namespace TheShop.Application.Features.Admin.DTOs;

/// <summary>
/// One dashboard card: a governed module and its current record count. A <c>null</c>
/// <see cref="Count"/> means the count could not be retrieved and the Web layer renders a
/// placeholder instead of a number (spec FR-7 / AC-5).
/// </summary>
public sealed record AdminModuleCardDto(AdminModule Module, int? Count);
