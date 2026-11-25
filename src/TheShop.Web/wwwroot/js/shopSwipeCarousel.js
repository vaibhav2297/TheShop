export function mount(viewport, receiver) {
    let enabled = false, count = 0, active = -1, disposed = false, timer, target = null;
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const slides = () => Array.from(viewport.children);
    function nearest() {
        const bounds = viewport.getBoundingClientRect();
        return slides().reduce((best, slide, index) => {
            const distance = Math.abs(slide.getBoundingClientRect().left - bounds.left);
            return distance < best.distance ? { index, distance } : best;
        }, { index: 0, distance: Infinity }).index;
    }
    function align(index, smooth) {
        const slide = viewport.children[index];
        if (!slide) return;
        const delta = slide.getBoundingClientRect().left - viewport.getBoundingClientRect().left;
        viewport.scrollTo({ left: viewport.scrollLeft + delta, behavior: smooth && !reduced.matches ? 'smooth' : 'instant' });
    }
    async function settled() {
        clearTimeout(timer);
        if (!enabled || disposed || count < 2) return;
        const index = nearest();
        if (target !== null && index !== target) return;
        target = null;
        if (index === active) return;
        active = index;
        await receiver.invokeMethodAsync('OnCarouselIndexChanged', index);
    }
    function scrolling() { clearTimeout(timer); timer = setTimeout(settled, 150); }
    function interrupt() { target = null; }
    function keydown(event) {
        if (!enabled || count < 2 || event.target !== viewport || event.altKey || event.ctrlKey || event.metaKey) return;
        const current = nearest();
        const index = { ArrowLeft: Math.max(0, current - 1), ArrowRight: Math.min(count - 1, current + 1), Home: 0, End: count - 1 }[event.key];
        if (index === undefined) return;
        event.preventDefault();
        target = null;
        align(index, true);
        scrolling();
    }
    const resize = new ResizeObserver(() => { if (enabled && active >= 0) align(target ?? active, false); });
    resize.observe(viewport);
    viewport.addEventListener('scroll', scrolling, { passive: true });
    viewport.addEventListener('scrollend', settled);
    viewport.addEventListener('pointerdown', interrupt, { passive: true });
    viewport.addEventListener('wheel', interrupt, { passive: true });
    viewport.addEventListener('keydown', keydown);
    return {
        update(index, nextEnabled, nextCount) {
            if (disposed) return;
            const changed = index !== active || nextEnabled !== enabled || count !== nextCount;
            enabled = nextEnabled; count = nextCount;
            if (!enabled) {
                clearTimeout(timer); target = null; active = index;
                viewport.scrollTo({ left: 0, behavior: 'instant' });
                return;
            }
            if (changed) {
                clearTimeout(timer); target = index; active = index;
                align(index, false);
                timer = setTimeout(settled, 150);
            }
        },
        dispose() {
            disposed = true; clearTimeout(timer); resize.disconnect();
            viewport.removeEventListener('scroll', scrolling);
            viewport.removeEventListener('scrollend', settled);
            viewport.removeEventListener('pointerdown', interrupt);
            viewport.removeEventListener('wheel', interrupt);
            viewport.removeEventListener('keydown', keydown);
        }
    };
}
