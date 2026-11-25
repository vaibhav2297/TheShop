namespace TheShop.Domain.Enums;

/// <summary>How Fresh Drops chooses its products.</summary>
public enum FreshDropsSelectionMode
{
    /// <summary>Use the newest published products.</summary>
    Newest,
    /// <summary>Use an ordered staff selection.</summary>
    Manual
}
