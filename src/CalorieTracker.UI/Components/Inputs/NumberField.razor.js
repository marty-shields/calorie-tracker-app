// Focus can be lost while a host (such as the stories iframe) is still laying out or swapping its content,
// so keep trying on each frame until the input really is the active element.
export function focusWhenReady(input, attempts) {
    return new Promise((resolve) => {
        const attempt = (left) => {
            if (!input.isConnected) {
                resolve(false);
                return;
            }
            input.focus();
            if (document.activeElement === input || left <= 0) {
                resolve(document.activeElement === input);
                return;
            }
            requestAnimationFrame(() => attempt(left - 1));
        };
        attempt(attempts);
    });
}
