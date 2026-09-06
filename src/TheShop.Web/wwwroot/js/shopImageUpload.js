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
