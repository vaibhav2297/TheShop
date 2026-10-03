const dialogs = new WeakMap();
let activeCleanup;
let activeNotify;

export function show(element, receiver) {
    activeNotify?.();
    activeCleanup?.();
    const trigger = document.activeElement;
    let disposed = false;
    let notified = false;
    let pointerStartedOutside = false;

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
    const pointerDown = event => { pointerStartedOutside = event.target === element && outside(event); };
    const click = event => {
        if (pointerStartedOutside && event.target === element && outside(event)) notify();
        pointerStartedOutside = false;
    };
    const observer = new MutationObserver(() => { if (!element.isConnected) cleanup(); });
    const cleanup = () => {
        if (disposed) return;
        disposed = true;
        observer.disconnect();
        element.removeEventListener('cancel', cancel);
        element.removeEventListener('close', notify);
        element.removeEventListener('pointerdown', pointerDown);
        element.removeEventListener('click', click);
        if (element.open) element.close();
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

    dialogs.set(element, cleanup);
    activeCleanup = cleanup;
    activeNotify = notify;
    element.addEventListener('cancel', cancel);
    element.addEventListener('close', notify);
    element.addEventListener('pointerdown', pointerDown);
    element.addEventListener('click', click);
    observer.observe(document.body, { childList: true, subtree: true });
    try {
        element.showModal();
        element.querySelector('[data-dialog-initial-focus]')?.focus({ preventScroll: true });
    } catch (error) {
        cleanup();
        throw error;
    }
}

export function dispose(element) {
    dialogs.get(element)?.();
}
