const menus = new WeakMap();

export function observeMenu(element, receiver) {
    dispose(element);
    let visible;
    let disposed = false;
    const observer = new ResizeObserver(() => {
        const next = element.getClientRects().length > 0;
        if (next === visible) return;
        visible = next;
        if (!visible) receiver.invokeMethodAsync('MenuUnavailableAsync').catch(error => {
            if (!disposed && element.isConnected) console.error('Navigation drawer dismissal failed.', error);
        });
    });
    observer.observe(element);
    menus.set(element, () => { disposed = true; observer.disconnect(); });
}

export function dispose(element) {
    menus.get(element)?.();
    menus.delete(element);
}
