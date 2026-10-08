const bindings = new WeakMap();

export function bind(root, input) {
    unbind(root);
    const controller = new AbortController();
    const state = { input, controller, locked: false, depth: 0 };
    bindings.set(root, state);
    const disabled = () => input.disabled || root.dataset.disabled === 'true' || state.locked;
    const listen = (target, name, handler, capture = false) =>
        target.addEventListener(name, handler, { signal: controller.signal, capture });
    const files = event => Array.from(event.dataTransfer?.types ?? []).includes('Files');
    listen(root, 'click', event => {
        if (!event.target.closest('[data-file-picker]') || disabled()) return;
        input.click();
    });
    listen(input, 'change', event => {
        if (disabled()) { event.stopImmediatePropagation(); return; }
        state.locked = true;
    }, true);
    listen(root, 'dragenter', event => {
        if (!files(event)) return;
        event.preventDefault();
        if (!disabled()) { state.depth++; root.dataset.dragover = 'true'; }
    });
    listen(root, 'dragover', event => {
        if (!files(event)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = disabled() ? 'none' : 'copy';
    });
    listen(root, 'dragleave', event => {
        if (!files(event)) return;
        if (--state.depth <= 0) { state.depth = 0; delete root.dataset.dragover; }
    });
    listen(root, 'drop', event => {
        if (!files(event)) return;
        event.preventDefault();
        state.depth = 0;
        delete root.dataset.dragover;
        if (disabled()) return;
        input.files = event.dataTransfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });
}

export function reset(root) {
    const state = bindings.get(root);
    if (!state) return;
    state.input.value = '';
    state.locked = false;
    state.depth = 0;
    delete root.dataset.dragover;
}

export function unbind(root) {
    bindings.get(root)?.controller.abort();
    bindings.delete(root);
}
