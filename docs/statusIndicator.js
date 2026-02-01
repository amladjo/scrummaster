// Mirrors original scrum-master.html behavior: remove the green check after a few seconds.
window.sm = window.sm || {};

// Keep timeout handles per element so repeated calls don't pile up.
window.sm._statusCheckTimeouts = window.sm._statusCheckTimeouts || {};

window.sm.removeStatusCheckAfterDelay = (elementId, delayMs) => {
    console.log("scheduling clear check icon for", elementId, "in", delayMs ?? 5000, "ms");
    if (!elementId) return;

    // Clear any previous scheduled removal for this element.
    const prev = window.sm._statusCheckTimeouts[elementId];
    if (prev) {
        clearTimeout(prev);
        delete window.sm._statusCheckTimeouts[elementId];
    }

    const timeoutId = setTimeout(() => {
        // Re-query at execution time (the element could have been re-rendered).
        console.log("started clear check icon for", elementId);
        const el = document.getElementById(elementId);
        if (!el) return;

        // Remove only the check icon node; don't touch innerHTML (Blazor-safe).
        const checkIcon = el.querySelector('i.fas.fa-check');
        if (checkIcon && checkIcon.parentNode) {
            checkIcon.parentNode.removeChild(checkIcon);
        }

        delete window.sm._statusCheckTimeouts[elementId];
    }, delayMs ?? 5000);

    window.sm._statusCheckTimeouts[elementId] = timeoutId;
};
