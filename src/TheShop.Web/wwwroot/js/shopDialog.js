const dialogs = new WeakMap();
let activeCleanup;
let activeNotify;

export function show(element, receiver, animated = false) {
    activeNotify?.();
    activeCleanup?.();
    const trigger = document.activeElement;
    let disposed = false;
    let notified = false;
    let pointerStartedOutside = false;
    let revision = 0;

    const outside = event => {
        const rect = element.getBoundingClientRect();
        return event.clientX < rect.left || event.clientX > rect.right ||
            event.clientY < rect.top || event.clientY > rect.bottom;
    };
    const notify = () => {
        if (disposed || notified) return;
        notified = true;
        receiver.invokeMethodAsync('DismissAsync').catch(error => {
            if (!disposed && element.isConnected) console.error('Dialog dismissal failed.', error);
        });
    };
    const cancel = event => { event.preventDefault(); notify(); };
    const keyDown = event => {
        if (event.key !== 'Tab') return;
        const controls = [...element.querySelectorAll('a[href], button, input, select, textarea, [tabindex]')]
            .filter(control => control.tabIndex >= 0 && !control.matches(':disabled') &&
                !control.closest('[inert]') && control.getClientRects().length &&
                getComputedStyle(control).visibility !== 'hidden');
        const first = controls[0];
        const last = controls.at(-1);
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    };
    const pointerDown = event => { pointerStartedOutside = event.target === element && outside(event); };
    const click = event => {
        if (pointerStartedOutside && event.target === element && outside(event)) notify();
        pointerStartedOutside = false;
    };
    const observer = new MutationObserver(() => { if (!element.isConnected) cleanup(); });
    const cleanup = () => {
        if (disposed) return;
        disposed = true;
        revision++;
        observer.disconnect();
        element.removeEventListener('cancel', cancel);
        element.removeEventListener('keydown', keyDown);
        element.removeEventListener('close', notify);
        element.removeEventListener('pointerdown', pointerDown);
        element.removeEventListener('click', click);
        if (element.open) element.close();
        element.removeAttribute('data-modal-visible');
        dialogs.delete(element);
        if (activeCleanup === cleanup) {
            activeCleanup = undefined;
            activeNotify = undefined;
        }
        if (!document.querySelector('dialog:modal')) {
            if (trigger instanceof HTMLElement && trigger.isConnected && !trigger.matches(':disabled')) {
                trigger.focus({ preventScroll: true });
            }
            if (!document.activeElement || document.activeElement === document.body) {
                const fallback = [...document.querySelectorAll('h1[tabindex], main[tabindex], #app button:not(:disabled), #app a[href]')]
                    .find(candidate => candidate.getClientRects().length && !candidate.closest('[inert]'));
                fallback?.focus({ preventScroll: true });
                if (document.activeElement === document.body) {
                    const previousTabIndex = document.body.getAttribute('tabindex');
                    document.body.setAttribute('tabindex', '-1');
                    document.body.focus({ preventScroll: true });
                    if (previousTabIndex === null) document.body.removeAttribute('tabindex');
                    else document.body.setAttribute('tabindex', previousTabIndex);
                }
            }
        }
    };

    const setOpen = open => {
        const currentRevision = ++revision;
        if (open) {
            notified = false;
            element.setAttribute('data-modal-visible', '');
        } else {
            element.removeAttribute('data-modal-visible');
            // Keep the modal and its inert backdrop alive until the exit transition finishes.
            Promise.allSettled(element.getAnimations().map(animation => animation.finished)).then(() => {
                if (!disposed && revision === currentRevision) cleanup();
            });
        }
    };
    dialogs.set(element, { cleanup, setOpen });
    activeCleanup = cleanup;
    activeNotify = notify;
    element.addEventListener('cancel', cancel);
    element.addEventListener('keydown', keyDown);
    element.addEventListener('close', notify);
    element.addEventListener('pointerdown', pointerDown);
    element.addEventListener('click', click);
    observer.observe(document.body, { childList: true, subtree: true });
    try {
        element.showModal();
        if (animated) {
            // Establish the off-screen starting style before enabling the transition.
            element.getBoundingClientRect();
            setOpen(true);
        }
        element.querySelector('[data-dialog-initial-focus]')?.focus({ preventScroll: true });
    } catch (error) {
        cleanup();
        throw error;
    }
}

export function setOpen(element, receiver, open) {
    const entry = dialogs.get(element);
    if (entry) entry.setOpen(open);
    else if (open) show(element, receiver, true);
}

export function dispose(element) {
    dialogs.get(element)?.cleanup();
}
