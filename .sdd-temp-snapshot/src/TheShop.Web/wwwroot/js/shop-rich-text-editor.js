// ES module — loaded lazily by ShopRichTextEditor via IJSRuntime import().
//
// Drives a vendored Quill 2.0.3 instance directly (plan Decision 1) instead of a MudBlazor
// wrapper package. Quill's own <link>/<script> are injected into <head> on first use only,
// behind a cached promise, so a page that never mounts the editor never downloads Quill.
// The Quill instance itself is stashed on the host element (host._shopQuill) rather than in
// module state, since more than one editor instance can be on screen (or torn down and
// recreated) within one page's lifetime.
//
// Toolbar: Quill's own `snow` toolbar (plan Decision 2 waiver — Rules 2/3/19 waived for this
// component only). Quill mounts on an inner div appended to host, never on host itself, because
// the toolbar module inserts a `.ql-toolbar` sibling next to its container — host stays an empty
// leaf Blazor never re-renders into, so that sibling insertion is never fought by Blazor's diff.

const ALLOWED_TAGS = new Set([
    'P', 'BR', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'STRONG', 'EM', 'B', 'I', 'OL', 'UL', 'LI', 'A',
]);

let assetsPromise = null;

function ensureAssetsLoaded() {
    if (assetsPromise) return assetsPromise;

    assetsPromise = new Promise((resolve, reject) => {
        if (window.Quill) {
            resolve();
            return;
        }

        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'lib/quill/quill.snow.css';
        document.head.appendChild(link);

        const script = document.createElement('script');
        script.src = 'lib/quill/quill.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load Quill.'));
        document.head.appendChild(script);
    });

    return assetsPromise;
}

// A fast client-side heuristic for the FR-7/AC-10 paste notice only — not the grammar's
// enforcement point. That is ProductDescription.Create server-side and the products_description_
// markup_allowed CHECK in the database (plan Decision 4); this only decides whether to show the
// "formatting removed" notice while the staff member is still looking at the paste that caused it.
function hasUnsupportedMarkup(html) {
    try {
        const doc = new DOMParser().parseFromString(html, 'text/html');
        const elements = doc.body.querySelectorAll('*');
        for (const el of elements) {
            if (!ALLOWED_TAGS.has(el.tagName)) return true;
            if (el.hasAttribute('style')) return true;
            if (el.tagName !== 'A' && el.attributes.length > 0) return true;
        }
        return false;
    } catch {
        return false;
    }
}

const TOOLBAR = [
    [{ header: [1, 2, 3, 4, 5, 6, false] }],
    ['bold', 'italic'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    ['link'],
];

/**
 * Initializes a Quill instance inside the host element.
 * @param {HTMLElement} host  – the empty div Blazor renders as @ref; Quill owns its DOM from here on
 * @param {object} dotNetRef  – DotNetObjectReference<ShopRichTextEditor>
 * @param {object} options    – { formats: string[], disabled: boolean, initialHtml: string|null, placeholder: string|null, ariaLabelledBy: string|null }
 */
export async function init(host, dotNetRef, options) {
    await ensureAssetsLoaded();

    const editorEl = document.createElement('div');
    host.appendChild(editorEl);

    const quill = new window.Quill(editorEl, {
        theme: 'snow',
        formats: options.formats,
        placeholder: options.placeholder ?? '',
        modules: { toolbar: TOOLBAR },
    });

    if (options.ariaLabelledBy) {
        quill.root.setAttribute('aria-labelledby', options.ariaLabelledBy);
    }

    if (options.initialHtml) {
        // Parsed into a Delta against the registered formats rather than assigned to innerHTML —
        // this is the path AC-11 depends on: unsupported markup is dropped by Quill's own format
        // registration, never interpreted as active content.
        quill.clipboard.dangerouslyPasteHTML(options.initialHtml);
    }

    quill.enable(!options.disabled);

    quill.on('text-change', () => {
        // quill.getText() already reflects the parsed document's real text — tags stripped, block
        // boundaries as \n, entities decoded — which is what the Decision 5 counting rule targets;
        // trimming here mirrors ProductDescription's server-side Trim() so the live counter agrees
        // with the boundary the server enforces.
        dotNetRef.invokeMethodAsync('OnTextChanged', quill.getSemanticHTML(), quill.getText().trim().length);
    });

    // Capture phase so this inspects the clipboard's own HTML before Quill's paste handling
    // strips unregistered formats out of the inserted Delta (FR-7, AC-10).
    host.addEventListener('paste', (e) => {
        const html = (e.clipboardData ?? window.clipboardData)?.getData('text/html');
        if (html && hasUnsupportedMarkup(html)) {
            dotNetRef.invokeMethodAsync('OnPasteUnsupported');
        }
    }, true);

    host._shopQuill = quill;
}

/** Replaces the editor's content — used when Value changes from outside the editor itself. */
export function setHtml(host, html) {
    const quill = host._shopQuill;
    if (!quill) return;
    quill.setText('');
    if (html) {
        quill.clipboard.dangerouslyPasteHTML(html);
    }
}

export function setDisabled(host, disabled) {
    const quill = host._shopQuill;
    if (quill) quill.enable(!disabled);
}

export function dispose(host) {
    delete host._shopQuill;
}
