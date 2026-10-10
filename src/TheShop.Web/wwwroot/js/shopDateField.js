// Native date inputs open their picker only from the browser's indicator; open it from anywhere in the field control.
document.addEventListener("click", event => {
    if (!(event.target instanceof Element)) return;
    const input = event.target.closest(".shop-field-control")?.querySelector("input[data-shop-date]");
    if (!(input instanceof HTMLInputElement) || input.disabled || input.readOnly) return;
    // A label click would also dispatch a synthetic input click; handle it once here.
    if (event.target.closest("label")) event.preventDefault();
    input.focus();
    try {
        input.showPicker?.();
    } catch {
        // Unsupported context (e.g. cross-origin frame): focus still allows keyboard entry.
    }
});
