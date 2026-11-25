namespace TheShop.Web.Common.UI;

/// <summary>A concrete numeric interval; filter keys and unbounded/null semantics belong to the consumer.</summary>
public readonly record struct ShopRangeValue(decimal Lower, decimal Upper);
