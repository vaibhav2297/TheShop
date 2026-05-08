namespace TheShop.Web.State;

/// <summary>
/// Client-side state store for the shopping cart.
/// Populated from CartDto returned by Application use cases.
/// </summary>
public class CartState
{
    public int ItemCount { get; private set; }

    public event Action? OnChange;

    public void Clear()
    {
        ItemCount = 0;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
