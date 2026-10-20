const instances = new Map();

export function sync(id) {
    const slot = document.getElementById(id);
    if (instances.get(id)?.slot !== slot) dispose(id);
    if (!slot) return;
    if (instances.has(id)) {
        instances.get(id).refresh();
        return;
    }

    const root = slot.closest("[data-shop-scroll-root]");
    const bar = slot.firstElementChild;
    if (!root || !bar) return;
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    let frame = 0;
    let animation;
    let docked = false;
    let stopped = false;
    let refreshSize = true;
    let slotWidth = 0;
    let rootWidth = 0;
    let rootHeight = 0;

    const set = (name, value) => slot.style.setProperty(name, value + "px");
    const update = () => {
        frame = 0;
        if (stopped || !slot.isConnected) return;
        const bounds = root.getBoundingClientRect();
        const anchor = slot.getBoundingClientRect();
        const top = bounds.top + root.clientTop;
        const shouldDock = docked ? anchor.top < top + 1 : anchor.top < top - 1;
        const before = bar.getBoundingClientRect();
        const changed = shouldDock !== docked;
        if (changed || refreshSize) animation?.cancel();

        if (refreshSize) {
            slot.removeAttribute("data-docked");
            slot.removeAttribute("data-ready");
            set("--shop-bulk-slot-height", bar.getBoundingClientRect().height);
            slot.setAttribute("data-ready", "");
            refreshSize = false;
        }

        set("--shop-bulk-dock-top", top);
        set("--shop-bulk-dock-left", bounds.left + root.clientLeft);
        set("--shop-bulk-dock-width", root.clientWidth);
        slot.toggleAttribute("data-docked", shouldDock);
        docked = shouldDock;
        const after = bar.getBoundingClientRect();
        root.style.setProperty("--shop-bulk-docked-height", docked ? after.height + "px" : "0px");

        if (changed) {
            if (!motion.matches) {
                // Animate geometry, not scale: text/icons stay sharp and the same controls keep focus.
                animation = bar.animate([
                    { transform: `translate(${before.left - after.left}px, ${Math.max(-12, Math.min(12, before.top - after.top))}px)`, width: before.width + "px" },
                    { transform: "translate(0, 0)", width: after.width + "px" }
                ], { duration: 200, easing: "cubic-bezier(0.2, 0, 0, 1)" });
            }
        }
    };
    const schedule = () => {
        if (!frame && !stopped) frame = requestAnimationFrame(update);
    };
    const refresh = () => { refreshSize = true; schedule(); };
    const resized = new ResizeObserver(entries => {
        const width = slot.getBoundingClientRect().width;
        if (width !== slotWidth || root.clientWidth !== rootWidth || root.clientHeight !== rootHeight) {
            slotWidth = width;
            rootWidth = root.clientWidth;
            rootHeight = root.clientHeight;
            refresh();
        }
        else if (entries.some(entry => entry.target === bar) && animation?.playState !== "running" && animation?.playState !== "paused") {
            refresh();
        }
    });
    const removed = new MutationObserver(() => {
        if (!slot.isConnected) dispose(id);
    });
    const motionChanged = () => { animation?.cancel(); };
    root.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", refresh, { passive: true });
    motion.addEventListener("change", motionChanged);
    resized.observe(root);
    resized.observe(slot);
    resized.observe(bar);
    removed.observe(root, { childList: true, subtree: true });
    instances.set(id, { slot, refresh, dispose: () => {
        stopped = true;
        cancelAnimationFrame(frame);
        animation?.cancel();
        resized.disconnect();
        removed.disconnect();
        root.removeEventListener("scroll", schedule);
        window.removeEventListener("resize", refresh);
        motion.removeEventListener("change", motionChanged);
        root.style.removeProperty("--shop-bulk-docked-height");
        slot.removeAttribute("data-ready");
        slot.removeAttribute("data-docked");
        for (const name of ["slot-height", "dock-top", "dock-left", "dock-width"])
            slot.style.removeProperty("--shop-bulk-" + name);
    } });
    update();
}

export function dispose(id) {
    instances.get(id)?.dispose();
    instances.delete(id);
}
