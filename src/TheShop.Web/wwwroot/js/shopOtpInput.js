// ES module — loaded lazily by OtpInput via IJSRuntime import().
//
// Digit keys are owned here rather than by the input event:
//   a. e.preventDefault() stops the browser inserting the character, so a box never
//      holds two digits and no input event reaches Blazor.
//   b. e.target.value = e.key shows the digit immediately, before the async C# call.
//   c. invokeMethodAsync('HandleDigitKeyAsync') updates state and advances focus. It
//      runs even when the new digit equals the existing one, which would otherwise
//      produce no change event and leave focus in place.
//
// Non-digit keys (Backspace, ArrowLeft/Right, Tab …) are not prevented and reach
// Blazor's keydown handler. Deletion, virtual keyboards that report no key, and
// one-time-code autofill reach Blazor through the input event.

const digitSelector = 'input.shop-otp-digit';

/**
 * Focuses the digit input at the given zero-based index inside the OTP container.
 *
 * @param {string} containerId – id attribute on the OTP container element
 * @param {number} index       – zero-based index of the digit box to focus
 */
export function focusInput(containerId, index) {
    const el = document.getElementById(containerId);
    if (!el) return;
    const target = el.querySelectorAll(digitSelector)[index];
    if (target) target.focus(); // focusin listener handles select() for visual feedback
}

/**
 * @param {string} elementId  – id attribute on the OTP container element
 * @param {object} dotNetRef  – DotNetObjectReference<OtpInput>
 */
export function registerPaste(elementId, dotNetRef) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // ── Paste ─────────────────────────────────────────────────────────────────
    // Capture phase so the full clipboard text is distributed across every box.
    el.addEventListener('paste', (e) => {
        e.preventDefault();
        const text = (e.clipboardData ?? window.clipboardData)?.getData('text') ?? '';
        dotNetRef.invokeMethodAsync('HandlePasteAsync', text);
    }, true);

    // ── Focus / Click — select-all for visual feedback ────────────────────────
    // focusin bubbles (unlike focus) and covers tab, arrow-key, and programmatic
    // focus. click covers re-clicking an already-focused box (focusin does not
    // re-fire in that case). Both select-all so typing replaces the current digit.
    el.addEventListener('focusin', (e) => {
        if (e.target instanceof HTMLInputElement) e.target.select();
    });

    el.addEventListener('click', (e) => {
        if (e.target instanceof HTMLInputElement) e.target.select();
    });

    // ── Digit keydown (capture) — take full ownership ─────────────────────────
    el.addEventListener('keydown', (e) => {
        if (!(e.target instanceof HTMLInputElement) || !/^\d$/.test(e.key)) return;
        if (e.ctrlKey || e.metaKey || e.altKey) return;

        e.preventDefault();
        e.target.value = e.key;

        const inputs = Array.from(el.querySelectorAll(digitSelector));
        const index = inputs.indexOf(e.target);
        if (index !== -1) {
            dotNetRef.invokeMethodAsync('HandleDigitKeyAsync', index, e.key);
        }
    }, true);
}
