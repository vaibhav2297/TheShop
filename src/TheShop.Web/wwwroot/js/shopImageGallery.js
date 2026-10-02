// ES module — loaded lazily by ShopImageGallery via IJSRuntime import().
//
// The preview strip scrolls horizontally inside its own frame. Previous/next scroll it by one
// visible width; reveal keeps the selected preview in view after the selection changes from
// outside the strip. Neither changes which image is selected.

function find(windowId) {
    return document.querySelector(`[data-shop-image-gallery="${windowId}"]`);
}

/**
 * Scrolls the strip tagged data-shop-image-gallery="{windowId}" by one visible width.
 * @param {string} windowId  – value of the strip's data-shop-image-gallery attribute
 * @param {number} direction – -1 for previous, 1 for next
 */
export function scrollPage(windowId, direction) {
    const strip = find(windowId);
    if (!strip) return;
    strip.scrollBy({ left: direction * strip.clientWidth, behavior: 'smooth' });
}

/**
 * Scrolls the strip the minimum distance that brings the selected preview fully into view.
 * @param {string} windowId – value of the strip's data-shop-image-gallery attribute
 */
export function reveal(windowId) {
    const strip = find(windowId);
    const selected = strip?.querySelector('[aria-pressed="true"]');
    if (!selected) return;

    const start = selected.offsetLeft;
    const end = start + selected.offsetWidth;

    if (start < strip.scrollLeft) {
        strip.scrollLeft = start;
    } else if (end > strip.scrollLeft + strip.clientWidth) {
        strip.scrollLeft = end - strip.clientWidth;
    }
}
