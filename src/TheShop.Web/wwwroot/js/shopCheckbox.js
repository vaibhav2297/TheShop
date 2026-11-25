// Native indeterminate is a DOM property, not an HTML attribute.
export function setIndeterminate(input, value) {
    if (input instanceof HTMLInputElement) input.indeterminate = value;
}
