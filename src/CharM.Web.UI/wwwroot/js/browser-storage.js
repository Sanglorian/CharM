// Module-isolated wrapper around localStorage so the Blazor side can call into
// it via IJSRuntime.InvokeAsync<T>("import", ...).getItem / setItem / removeItem
// without ever evaluating raw JS strings. Single-slot persistence for the
// active character (encoded as base64 .dnd4e XML).

const SLOT_KEY = "charm:character:active";

export function getItem(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function setItem(key, value) {
    try {
        window.localStorage.setItem(key, value);
        return true;
    } catch {
        // Quota exceeded, security error, private-mode block. Caller treats
        // false as "transient failure, character keeps working in-memory".
        return false;
    }
}

export function removeItem(key) {
    try {
        window.localStorage.removeItem(key);
    } catch {
        // ignore
    }
}

export function getActiveCharacter() {
    return getItem(SLOT_KEY);
}

export function setActiveCharacter(base64Xml) {
    return setItem(SLOT_KEY, base64Xml);
}

export function clearActiveCharacter() {
    removeItem(SLOT_KEY);
}
