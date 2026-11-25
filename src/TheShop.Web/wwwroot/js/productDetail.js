export function mount(root, receiver, generation) {
    const desktop = matchMedia('(min-width: 1024px)');
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const scrollRoot = root.closest('.shop-main-scroll');
    const buybox = root.querySelector('.shop-product-detail-buybox');
    let animation, announcementTimer, disposed = false;
    function updateStickyOffset() {
        if (disposed || !buybox) return;
        if (!desktop.matches) {
            buybox.style.removeProperty('--shop-detail-buybox-sticky-top');
            return;
        }
        const height = scrollRoot?.clientHeight ?? innerHeight;
        const offset = Math.min(0, height - buybox.getBoundingClientRect().height);
        buybox.style.setProperty('--shop-detail-buybox-sticky-top', `${offset}px`);
    }
    function boundaryChanged() {
        updateStickyOffset();
        if (!disposed) receiver.invokeMethodAsync('OnDetailBreakpointChanged', desktop.matches, generation);
    }
    function usableViewport() {
        const scroll = root.closest('.shop-main-scroll');
        const rect = scroll?.getBoundingClientRect();
        return { top: rect ? Math.max(0, rect.top) : 0, bottom: rect ? Math.min(innerHeight, rect.bottom) : innerHeight };
    }
    function reveal(element) {
        if (!element) return;
        const rect = element.getBoundingClientRect(), bounds = usableViewport();
        if (rect.bottom > bounds.top && rect.top < bounds.bottom && rect.right > 0 && rect.left < innerWidth) return;
        element.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: reduced.matches ? 'instant' : 'smooth' });
    }
    function announce(text) {
        clearTimeout(announcementTimer);
        const live = root.querySelector('[data-detail-announcement]');
        if (!live) return;
        live.textContent = '';
        announcementTimer = setTimeout(() => { if (!disposed) live.textContent = text; }, 60);
    }
    desktop.addEventListener('change', boundaryChanged);
    const resize = new ResizeObserver(updateStickyOffset);
    if (buybox) resize.observe(buybox);
    if (scrollRoot) resize.observe(scrollRoot);
    window.addEventListener('resize', updateStickyOffset);
    updateStickyOffset();
    return {
        isDesktop: () => desktop.matches,
        revealImage(index) {
            if (desktop.matches) reveal(root.querySelector(`[data-carousel-index="${index}"]`));
        },
        focusExpanded(message) {
            const tile = root.querySelector('[data-carousel-index="4"]');
            tile?.focus({ preventScroll: true }); reveal(tile); announce(message);
        },
        attention(groupId, message) {
            const block = root.querySelector('[data-testid="product-detail-options"]');
            if (!block) return;
            const group = groupId ? block.querySelector(`[data-option-group="${groupId}"]`) : block;
            reveal(group ?? block);
            (group ?? block).querySelector('button')?.focus({ preventScroll: true });
            animation?.cancel();
            if (!reduced.matches) animation = block.animate([
                { transform: 'translateX(0)' }, { transform: 'translateX(-6px)' },
                { transform: 'translateX(6px)' }, { transform: 'translateX(-4px)' },
                { transform: 'translateX(4px)' }, { transform: 'translateX(0)' }
            ], { duration: 300, iterations: 1 });
            announce(message);
        },
        clearAttention() {
            animation?.cancel(); clearTimeout(announcementTimer);
            const live = root.querySelector('[data-detail-announcement]');
            if (live) live.textContent = '';
        },
        dispose() {
            disposed = true;
            desktop.removeEventListener('change', boundaryChanged);
            window.removeEventListener('resize', updateStickyOffset);
            resize.disconnect();
            buybox?.style.removeProperty('--shop-detail-buybox-sticky-top');
            animation?.cancel(); clearTimeout(announcementTimer);
        }
    };
}
