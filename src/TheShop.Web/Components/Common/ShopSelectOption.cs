namespace TheShop.Web.Components.Common;

/// <summary>A typed single-select value with an already-localized label and optional unavailable state.</summary>
public sealed record ShopSelectOption<TValue>(TValue Value, string Label, bool Disabled = false);
