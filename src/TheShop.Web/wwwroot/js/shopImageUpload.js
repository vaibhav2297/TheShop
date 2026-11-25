// ES module — loaded lazily by ShopImageUpload via IJSRuntime import().
//
// Preview thumbnails use object URLs (blob:) instead of base64 data: URIs. A data: URI would
// hold a picked file's full bytes a second time as one giant string in Blazor's render tree,
// re-diffed on every unrelated re-render of the page (e.g. typing in another field) — the
// larger and more numerous the picked files, the more that costs. An object URL is a short
// fixed-length handle the browser resolves internally, so the render tree never carries the
// payload at all.

/**
 * Creates an object URL for a file's bytes, transferred as a .NET stream reference so the
 * payload never crosses the JS interop boundary as a base64-encoded JSON string.
 * @param {any} streamRef      – DotNetStreamReference wrapping the file's bytes
 * @param {string} contentType – MIME type reported by the browser
 * @returns {Promise<string>} the blob: URL
 */
export async function createObjectUrl(streamRef, contentType) {
    const arrayBuffer = await streamRef.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType });
    return URL.createObjectURL(blob);
}

/**
 * Releases a previously created object URL's underlying memory. Must be called once the
 * preview is no longer shown (removed from the selection, replaced, or the component is
 * disposed) — object URLs otherwise leak for the lifetime of the page.
 * @param {string} url – the blob: URL returned by createObjectUrl
 */
export function revokeObjectUrl(url) {
    URL.revokeObjectURL(url);
}

const ordering = new WeakMap();

export function bindOrder(root, dotnet) {
    unbindOrder(root);
    const controller = new AbortController();
    ordering.set(root, controller);
    let source = null;
    const blocked = () => root.dataset.uploadDisabled === 'true';
    const item = event => event.target.closest('[data-upload-id]');
    const clear = () => root.querySelectorAll('[data-drop-target]').forEach(node => delete node.dataset.dropTarget);
    const listen = (name, handler) => root.addEventListener(name, handler, { signal: controller.signal });
    listen('dragstart', event => {
        const tile = item(event);
        if (blocked() || !tile || !event.target.matches('.shop-image-tile-surface[draggable="true"]')) {
            event.preventDefault(); return;
        }
        source = tile.dataset.uploadId;
        event.dataTransfer.setData('text/plain', source);
        event.dataTransfer.effectAllowed = 'move';
    });
    listen('dragover', event => {
        if (!source || blocked() || !item(event)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        clear();
        item(event).dataset.dropTarget = 'true';
    });
    listen('drop', event => {
        if (!source) return;
        event.preventDefault();
        const from = source;
        source = null;
        clear();
        const target = item(event)?.dataset.uploadId;
        if (!blocked() && target) dotnet.invokeMethodAsync('ReorderAsync', from, target);
    });
    listen('dragend', () => { source = null; clear(); });
    listen('keydown', event => {
        if (event.target.matches('.shop-image-tile-surface') && event.altKey &&
            ['ArrowLeft', 'ArrowRight'].includes(event.key)) event.preventDefault();
    });
}

export function unbindOrder(root) {
    ordering.get(root)?.abort();
    ordering.delete(root);
}

export function focusImage(root, id) {
    requestAnimationFrame(() => {
        if (!root.isConnected) return;
        const item = [...root.querySelectorAll('[data-upload-id]')].find(node => node.dataset.uploadId === id);
        (item?.querySelector('.shop-image-tile-surface') ?? root.querySelector('[data-file-picker]'))?.focus();
    });
}
