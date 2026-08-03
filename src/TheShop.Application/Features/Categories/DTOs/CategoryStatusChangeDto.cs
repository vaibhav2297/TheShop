namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// The outcome of a single or bulk activate/deactivate. <see cref="ChangedCount"/> counts only
/// categories that actually changed state — a category already in the target status is a no-op
/// and is excluded (plan §6 Flow 4 step 3).
/// </summary>
public sealed record CategoryStatusChangeDto(int ChangedCount);
