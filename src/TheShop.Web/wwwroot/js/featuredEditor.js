export function mount(root) {
    const desktop = matchMedia('(min-width: 1024px)');
    const scrollRoot = root.closest('.shop-main-scroll');
    let panel = null;
    let disposed = false;
    const resize = new ResizeObserver(updateStickyOffset);

    function updateStickyOffset() {
        if (disposed || !panel) return;
        if (!desktop.matches) {
            panel.style.removeProperty('--shop-featured-sticky-top');
            return;
        }
        const height = scrollRoot?.clientHeight ?? innerHeight;
        const offset = Math.min(0, height - panel.getBoundingClientRect().height);
        panel.style.setProperty('--shop-featured-sticky-top', `${offset}px`);
    }

    function refresh() {
        if (disposed) return;
        const next = root.querySelector('[data-testid="featured-settings"]');
        if (next !== panel) {
            if (panel) {
                resize.unobserve(panel);
                panel.style.removeProperty('--shop-featured-sticky-top');
            }
            panel = next;
            if (panel) resize.observe(panel);
        }
        updateStickyOffset();
    }

    if (scrollRoot) resize.observe(scrollRoot);
    desktop.addEventListener('change', updateStickyOffset);
    window.addEventListener('resize', updateStickyOffset);
    refresh();

    return {
        refresh,
        dispose() {
            disposed = true;
            desktop.removeEventListener('change', updateStickyOffset);
            window.removeEventListener('resize', updateStickyOffset);
            resize.disconnect();
            panel?.style.removeProperty('--shop-featured-sticky-top');
            panel = null;
        }
    };
}
