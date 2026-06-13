// ES module — loaded lazily by OtpInput via IJSRuntime import().
//
// Strategy — digit keys go through three coordinated steps:
//
//   JS (synchronous, keydown capture):
//     a. e.preventDefault()  — stops the browser inserting the character.
//        MudNumericField never receives oninput, so its internal edit buffer
//        never changes.
//     b. e.target.value = e.key  — writes the digit directly to the DOM
//        property (NOT the attribute, so no oninput fires). Two purposes:
//          i.  The box shows the digit immediately, before the async C# call.
//          ii. When C# later calls FocusBoxAsync the box loses focus. MudBlazor
//              reads the DOM value on blur; because it now reads e.key (not null
//              or the stale previous value), OnDigitChangedAsync receives the
//              correct digit and returns early instead of clearing it.
//     c. invokeMethodAsync('HandleDigitKeyAsync')  — queues the C# handler.
//
//   C# HandleDigitKeyAsync (async, runs after JS event finishes):
//     a. Updates _digits[index].
//     b. Calls FocusBoxAsync(index + 1)  — moves focus away BEFORE any
//        StateHasChanged re-render is queued. Once unfocused, MudNumericField
//        exits "edit mode" and will correctly pick up the Value prop on the
//        next render.
//     c. Calls EmitValueAsync / StateHasChanged.
//
// Why FocusBoxAsync must come before EmitValueAsync (and before StateHasChanged):
//   MudNumericField holds an internal edit buffer that it uses for display while
//   focused ("edit mode"). It ignores the externally-bound Value prop in that
//   state. If we re-render while the box is still focused, the display reverts
//   to the internal buffer (which is still the old value, because e.preventDefault
//   stopped oninput from updating it). Moving focus away first guarantees the
//   box is in "display mode" for the upcoming render.
//
// Non-digit keys (Backspace, ArrowLeft/Right, Tab …) are NOT prevented and
// continue to flow to Blazor via OnKeyDown as before.

/**
 * Focuses the digit input at the given zero-based index inside the OTP container.
 * Called by C# FocusBoxAsync. We address inputs by their wrapper class
 * (.shop-otp-digit) rather than by input[type="number"] because MudNumericField's
 * inner <input> type is not guaranteed across MudBlazor versions/variants; a
 * type-agnostic selector keeps indexing consistent with the keydown listener
 * below.
 *
 * @param {string} containerId – id attribute on the OTP container element
 * @param {number} index       – zero-based index of the digit box to focus
 */
export function focusInput(containerId, index) {
    const el = document.getElementById(containerId);
    if (!el) return;
    const inputs = el.querySelectorAll('.shop-otp-digit input');
    const target = inputs[index];
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
    // Capture phase so child-input maxlength cannot truncate clipboard text.
    el.addEventListener('paste', (e) => {
        e.preventDefault();
        const text = (e.clipboardData ?? window.clipboardData)?.getData('text') ?? '';
        dotNetRef.invokeMethodAsync('HandlePasteAsync', text);
    }, true);

    // ── Focus / Click — select-all for visual feedback ────────────────────────
    // focusin bubbles (unlike focus) and covers tab, arrow-key, and programmatic
    // FocusAsync.  click covers re-clicking an already-focused box (focusin does
    // not re-fire in that case).  Both select-all so the user sees the current
    // digit highlighted before they type.
    el.addEventListener('focusin', (e) => {
        if (e.target instanceof HTMLInputElement) e.target.select();
    });

    el.addEventListener('click', (e) => {
        if (e.target instanceof HTMLInputElement) e.target.select();
    });

    // ── Digit keydown (capture) — take full ownership ─────────────────────────
    // e.preventDefault() stops the browser from inserting the character, so
    // MudNumericField never receives an oninput / ValueChanged for digit keys.
    // HandleDigitKeyAsync (C#) then owns updating state and advancing focus.
    // This also covers the case where the new digit equals the existing one:
    // because MudBlazor would suppress ValueChanged entirely, the Blazor handler
    // would never be called — so we must drive it from here unconditionally.
    el.addEventListener('keydown', (e) => {
        if (!(e.target instanceof HTMLInputElement) || !/^\d$/.test(e.key)) return;

        e.preventDefault(); // stop browser inserting the character — MudBlazor gets no oninput

        // Write the digit directly into the DOM immediately (synchronous).
        // Two purposes:
        //   1. Shows the digit visually before the async Blazor round-trip arrives.
        //   2. When HandleDigitKeyAsync calls FocusBoxAsync, this input loses focus
        //      and MudBlazor reads the DOM value on blur. With e.target.value set to
        //      the correct digit, MudBlazor fires ValueChanged(digit) instead of
        //      ValueChanged(null), so OnDigitChangedAsync returns early rather than
        //      clearing the digit we just typed.
        // Programmatic .value assignment does NOT dispatch oninput, so MudBlazor's
        // internal buffer is never touched by this write.
        e.target.value = e.key;

        const inputs = Array.from(el.querySelectorAll('.shop-otp-digit input'));
        const index = inputs.indexOf(e.target);
        if (index !== -1) {
            dotNetRef.invokeMethodAsync('HandleDigitKeyAsync', index, e.key);
        }
    }, true);
}
