const instances = new Map();

export function sync(id) {
    const root = document.getElementById(id);
    if (instances.get(id)?.root !== root) dispose(id);
    if (!root) return;
    if (instances.has(id)) { instances.get(id).position(); return; }
    const stage = root.querySelector(".shop-range-stage");
    const inputs = [...stage.querySelectorAll("input")];
    let frame;
    const position = () => {
        const bubble = root.querySelector(".shop-range-bubble");
        if (!bubble) return;
        if (root.disabled || root.closest("[inert]")) {
            if (bubble.matches(":popover-open")) bubble.hidePopover();
            return;
        }
        const rail = stage.querySelector(".shop-range-rail").getBoundingClientRect();
        const name = bubble.classList.contains("shop-range-bubble-lower") ? "--shop-range-lower" : "--shop-range-upper";
        const fraction = parseFloat(getComputedStyle(stage).getPropertyValue(name)) / 100;
        const rem = parseFloat(getComputedStyle(document.documentElement).fontSize);
        bubble.style.left = `${rail.left + rail.width * fraction}px`;
        // Thumb radius (0.75rem), visual gap (0.25rem), and the 6px pointer.
        bubble.style.top = `${rail.top + rail.height / 2 - rem - 6}px`;
        if (!bubble.matches(":popover-open")) bubble.showPopover();
    };
    const schedulePosition = () => {
        if (frame) return;
        frame = requestAnimationFrame(() => { frame = null; position(); });
    };
    const resizeObserver = new ResizeObserver(schedulePosition);
    resizeObserver.observe(stage);
    document.addEventListener("scroll", schedulePosition, true);
    window.addEventListener("resize", schedulePosition);
    let drag;
    const update = event => {
        if (!drag || root.disabled || root.closest("[inert]")) return;
        const rect = inputs[0].getBoundingClientRect();
        const radius = parseFloat(getComputedStyle(document.documentElement).fontSize) * 0.75;
        const fraction = Math.max(0, Math.min(1, (event.clientX - rect.left - radius) / Math.max(1, rect.width - radius * 2)));
        const min = Number(drag.min), max = Number(drag.max);
        drag.value = String(min + fraction * (max - min));
        drag.dispatchEvent(new Event("input", { bubbles: true }));
    };
    const down = event => {
        if (event.button !== 0 || root.disabled || root.closest("[inert]") || event.target instanceof HTMLInputElement) return;
        const rect = stage.getBoundingClientRect();
        if (event.clientY < rect.bottom - 34) return;
        const fraction = (event.clientX - rect.left) / rect.width;
        const value = Number(inputs[0].min) + fraction * (Number(inputs[0].max) - Number(inputs[0].min));
        const lower = Number(inputs[0].value), upper = Number(inputs[1].value);
        drag = lower === upper
            ? (value < lower ? inputs[0] : inputs[1])
            : (Math.abs(value - lower) < Math.abs(value - upper) ? inputs[0] : inputs[1]);
        drag.focus({ preventScroll: true });
        stage.setPointerCapture(event.pointerId);
        event.preventDefault();
        update(event);
    };
    const up = () => { drag = null; };
    const key = event => {
        const input = event.target;
        if (!inputs.includes(input) || root.disabled || root.closest("[inert]")) return;
        const min = Number(input.getAttribute("aria-valuemin"));
        const max = Number(input.getAttribute("aria-valuemax"));
        const step = Number(input.dataset.step);
        let value = Number(input.value);
        switch (event.key) {
            case "ArrowLeft": case "ArrowDown": value -= step; break;
            case "ArrowRight": case "ArrowUp": value += step; break;
            case "PageDown": value -= step * 10; break;
            case "PageUp": value += step * 10; break;
            case "Home": value = min; break;
            case "End": value = max; break;
            default: return;
        }
        event.preventDefault();
        input.value = String(Math.max(min, Math.min(max, value)));
        input.dispatchEvent(new Event("input", { bubbles: true }));
    };
    stage.addEventListener("pointerdown", down);
    stage.addEventListener("pointermove", update);
    stage.addEventListener("pointerup", up);
    stage.addEventListener("pointercancel", up);
    stage.addEventListener("lostpointercapture", up);
    root.addEventListener("keydown", key);
    instances.set(id, { root, position, dispose: () => {
        up();
        cancelAnimationFrame(frame);
        resizeObserver.disconnect();
        document.removeEventListener("scroll", schedulePosition, true);
        window.removeEventListener("resize", schedulePosition);
        const bubble = root.querySelector(".shop-range-bubble:popover-open");
        bubble?.hidePopover();
        stage.removeEventListener("pointerdown", down);
        stage.removeEventListener("pointermove", update);
        stage.removeEventListener("pointerup", up);
        stage.removeEventListener("pointercancel", up);
        stage.removeEventListener("lostpointercapture", up);
        root.removeEventListener("keydown", key);
    } });
    position();
}

export function dispose(id) {
    instances.get(id)?.dispose();
    instances.delete(id);
}
