const controls = new WeakMap();

export function initialize(root, receiver) {
    dispose(root);
    const trigger = root.querySelector('.shop-select-trigger');
    const list = root.querySelector('.shop-select-options');
    let active;
    let expanded = false;
    let disposed = false;
    let search = '';
    let searchAt = 0;
    let fingerprint;
    const options = () => [...list.querySelectorAll('[role="option"]')];
    const enabled = () => options().filter(option => option.getAttribute('aria-disabled') !== 'true');

    const close = () => {
        expanded = false;
        search = '';
        trigger.setAttribute('aria-expanded', 'false');
        trigger.removeAttribute('aria-activedescendant');
        for (const option of options()) option.removeAttribute('data-active');
        if (list.matches(':popover-open')) list.hidePopover();
    };
    const position = () => {
        const rect = trigger.getBoundingClientRect();
        const viewport = window.visualViewport;
        const top = viewport?.offsetTop ?? 0;
        const left = viewport?.offsetLeft ?? 0;
        const height = viewport?.height ?? innerHeight;
        const width = viewport?.width ?? innerWidth;
        const below = Math.max(0, top + height - rect.bottom);
        const above = Math.max(0, rect.top - top);
        const flip = below < Math.min(list.scrollHeight, rect.height * 3) && above > below;
        list.dataset.placement = flip ? 'above' : 'below';
        list.style.setProperty('--shop-select-width', `${Math.min(rect.width, width)}px`);
        list.style.setProperty('--shop-select-left', `${Math.max(left, Math.min(rect.left, left + width - rect.width))}px`);
        list.style.setProperty('--shop-select-available-height', `${flip ? above : below}px`);
        list.style.setProperty('--shop-select-top', `${flip ? rect.top - list.getBoundingClientRect().height : rect.bottom}px`);
    };
    const activate = (option, keyboard = true) => {
        active = option;
        for (const item of options()) item.removeAttribute('data-active');
        if (!option) {
            trigger.removeAttribute('aria-activedescendant');
            return;
        }
        if (keyboard) option.setAttribute('data-active', 'true');
        trigger.setAttribute('aria-activedescendant', option.id);
        option.scrollIntoView({ block: 'nearest' });
    };
    const open = (keyboard = false) => {
        if (trigger.disabled || disposed || enabled().length === 0) return;
        expanded = true;
        trigger.setAttribute('aria-expanded', 'true');
        list.showPopover();
        position();
        activate(enabled().find(option => option.getAttribute('aria-selected') === 'true') ?? enabled()[0], keyboard);
    };
    const commit = option => {
        if (trigger.disabled || !option || option.getAttribute('aria-disabled') === 'true') return;
        close();
        // Blazor owns the committed value, label, checkmark and EditContext notification.
        receiver.invokeMethodAsync('SelectAsync', option.dataset.optionId).catch(error => {
            if (!disposed && root.isConnected) console.error('Select value update failed.', error);
        });
    };
    const click = event => {
        if (trigger.disabled) return;
        const option = event.target.closest('[role="option"]');
        if (option && list.contains(option)) {
            commit(option);
            trigger.focus({ preventScroll: true });
        } else if (trigger.contains(event.target)) {
            if (expanded) close();
            else open();
        }
    };
    const keydown = event => {
        if (trigger.disabled || event.ctrlKey || event.metaKey || event.isComposing) return;
        const key = event.key;
        if (key === 'Tab') {
            if (expanded) commit(active);
            return; // Native Tab/Shift+Tab must leave the control.
        }
        if (key === 'Escape') {
            if (expanded) {
                event.preventDefault();
                event.stopPropagation();
                close();
            }
            return;
        }
        const navigation = ['ArrowDown', 'ArrowUp', 'Home', 'End', 'Enter', ' '].includes(key);
        const printable = key.length === 1 && !event.altKey;
        if (!navigation && !printable) return;
        event.preventDefault();
        event.stopPropagation();
        if (performance.now() - searchAt > 700) search = '';
        if (key === 'Enter' || (key === ' ' && !search)) {
            if (expanded) commit(active);
            else open(true);
            return;
        }
        const wasOpen = expanded;
        if (!expanded) open(true);
        if (!expanded) return;
        const items = enabled();
        const index = items.indexOf(active);
        if (key === 'Home') activate(items[0]);
        else if (key === 'End') activate(items.at(-1));
        else if (key === 'ArrowDown' && wasOpen) activate(items[Math.min(index + 1, items.length - 1)]);
        else if (key === 'ArrowUp' && wasOpen) activate(items[Math.max(index - 1, 0)]);
        else if (printable) {
            const now = performance.now();
            search = now - searchAt > 700 ? key : search + key;
            searchAt = now;
            const repeated = [...search].every(character => character.toLocaleLowerCase() === key.toLocaleLowerCase());
            const term = (repeated ? key : search).toLocaleLowerCase();
            const start = repeated ? index + 1 : index;
            const match = items.map((_, offset) => items[(Math.max(0, start) + offset) % items.length])
                .find(option => option.textContent.trim().toLocaleLowerCase().startsWith(term));
            if (match) activate(match);
        }
    };
    const pointerdown = event => {
        if (!root.contains(event.target)) close();
        else if (list.contains(event.target) && event.target.closest('[role="option"]')) event.preventDefault();
    };
    const focusout = event => { if (!root.contains(event.relatedTarget)) close(); };
    const scroll = event => { if (expanded && !list.contains(event.target)) close(); };
    const resize = () => { if (expanded) position(); };
    const syncState = () => {
        const next = JSON.stringify([trigger.dataset.selected, trigger.disabled,
            options().map(option => [option.id, option.textContent, option.getAttribute('aria-disabled')])]);
        if (fingerprint !== next) close();
        fingerprint = next;
    };
    const cleanup = () => {
        if (disposed) return;
        disposed = true;
        close();
        observer.disconnect();
        root.removeEventListener('click', click);
        root.removeEventListener('focusout', focusout);
        trigger.removeEventListener('keydown', keydown);
        document.removeEventListener('pointerdown', pointerdown, true);
        document.removeEventListener('scroll', scroll, true);
        window.removeEventListener('resize', resize);
        window.visualViewport?.removeEventListener('resize', resize);
        controls.delete(root);
    };
    const observer = new MutationObserver(() => { if (!root.isConnected) cleanup(); });
    observer.observe(document.body, { childList: true, subtree: true });
    root.addEventListener('click', click);
    root.addEventListener('focusout', focusout);
    trigger.addEventListener('keydown', keydown);
    document.addEventListener('pointerdown', pointerdown, true);
    document.addEventListener('scroll', scroll, true);
    window.addEventListener('resize', resize);
    window.visualViewport?.addEventListener('resize', resize);
    controls.set(root, { sync: syncState, dispose: cleanup });
    syncState();
}

export function sync(root) {
    controls.get(root)?.sync();
}

export function dispose(root) {
    controls.get(root)?.dispose();
}
