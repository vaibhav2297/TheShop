// ES module — loaded lazily by ShopImage via IJSRuntime import().
//
// MudImage handles the <img> onerror event itself and exposes no failure callback, so ShopImage
// listens on its own frame instead. Image error events do not bubble; a capture-phase listener on
// the frame still sees them, including for <img> elements Blazor adds later when a source changes.
// Each report carries the branch and the exact src attribute so C# can ignore failures that belong
// to a source it has since replaced.

const observed = new Map();

function report(img, dotNetRef) {
    const branch = img.dataset.shopImageBranch;
    const src = img.getAttribute('src');
    if (!branch || !src) return;
    dotNetRef.invokeMethodAsync('OnImageFailed', branch, src);
}

/**
 * Starts reporting image failures inside the frame tagged data-shop-image="{frameId}".
 * @param {string} frameId  – value of the frame's data-shop-image attribute
 * @param {object} dotNetRef – DotNetObjectReference<ShopImage>
 */
export function observe(frameId, dotNetRef) {
    const frame = document.querySelector(`[data-shop-image="${frameId}"]`);
    if (!frame || observed.has(frameId)) return;

    const onError = (e) => {
        if (e.target instanceof HTMLImageElement) report(e.target, dotNetRef);
    };

    frame.addEventListener('error', onError, true);
    observed.set(frameId, { frame, onError });

    // An image can fail before this listener exists (cached failure, fast network). Anything
    // already complete is decoded once more: a broken image rejects, a valid one — including an
    // SVG without intrinsic size — resolves.
    frame.querySelectorAll('img[data-shop-image-branch]').forEach((img) => {
        if (img.complete) img.decode().catch(() => report(img, dotNetRef));
    });
}

/**
 * Stops reporting for the frame and releases its listener.
 * @param {string} frameId – value of the frame's data-shop-image attribute
 */
export function unobserve(frameId) {
    const entry = observed.get(frameId);
    if (!entry) return;
    entry.frame.removeEventListener('error', entry.onError, true);
    observed.delete(frameId);
}
