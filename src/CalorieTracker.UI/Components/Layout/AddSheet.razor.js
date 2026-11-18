// Keeps Tab inside the sheet while it is open, and puts focus back on Add (the element marked data-sheet-opener) when it closes.
let active = null;

function tabStops(sheet) {
    return [...sheet.querySelectorAll("button, input, a[href], [tabindex]")].filter((element) => {
        if (element.disabled || element.tabIndex < 0) {
            return false;
        }
        // A radio group is one tab stop: the checked radio, or the first when none is checked.
        if (element.type === "radio") {
            const group = [...sheet.querySelectorAll(`input[type="radio"][name="${CSS.escape(element.name)}"]`)];
            return element === (group.find((radio) => radio.checked) ?? group[0]);
        }
        return true;
    });
}

export function open(sheet) {
    close();
    const opener = document.querySelector("[data-sheet-opener]");
    const onKeyDown = (event) => {
        if (event.key !== "Tab") {
            return;
        }
        const stops = tabStops(sheet);
        if (stops.length === 0) {
            event.preventDefault();
            return;
        }
        const first = stops[0];
        const last = stops[stops.length - 1];
        const current = document.activeElement;
        if (!sheet.contains(current)) {
            event.preventDefault();
            (event.shiftKey ? last : first).focus();
        } else if (event.shiftKey && current === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && current === last) {
            event.preventDefault();
            first.focus();
        }
    };
    document.addEventListener("keydown", onKeyDown, true);
    active = { onKeyDown, opener };
    (tabStops(sheet)[0] ?? sheet).focus();
}

export function close() {
    if (active === null) {
        return;
    }
    const { onKeyDown, opener } = active;
    active = null;
    document.removeEventListener("keydown", onKeyDown, true);
    if (opener?.isConnected) {
        opener.focus();
    }
}
