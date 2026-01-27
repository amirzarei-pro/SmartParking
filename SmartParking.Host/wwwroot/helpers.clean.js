
const SIDEBAR_COLLAPSE_KEY = "smartparking.sidebar.collapsed";

function isNarrow() {
    // Keep in sync with CSS @media (max-width: 980px)
    return window.matchMedia("(max-width: 980px)").matches;
}

function setBodyScrollLock(lock) {
    document.documentElement.style.overflow = lock ? "hidden" : "";
}

function openSidebarMobile() {
    if (!isNarrow()) return;
    $("#sidebar")?.classList.add("is-open");
    $("#sidebarOverlay")?.removeAttribute("hidden");
    setBodyScrollLock(true);
}

function closeSidebarMobile() {
    $("#sidebar")?.classList.remove("is-open");
    $("#sidebarOverlay")?.setAttribute("hidden", "");
    setBodyScrollLock(false);
}

function applySidebarCollapsedState() {
    const app = $("#app");
    if (!app) return;

    // Collapse only makes sense on desktop
    if (isNarrow()) {
        app.classList.remove("has-collapsed");
        return;
    }

    const collapsed = localStorage.getItem(SIDEBAR_COLLAPSE_KEY) === "1";
    app.classList.toggle("has-collapsed", collapsed);
}

function toggleSidebarCollapsed() {
    const app = $("#app");
    if (!app) return;

    const next = !app.classList.contains("has-collapsed");
    app.classList.toggle("has-collapsed", next);
    localStorage.setItem(SIDEBAR_COLLAPSE_KEY, next ? "1" : "0");
}
