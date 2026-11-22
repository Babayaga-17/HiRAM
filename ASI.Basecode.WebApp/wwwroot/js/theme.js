(() => {
    const root = document.documentElement;
    const preference = window.matchMedia("(prefers-color-scheme: dark)");
    const key = "hiram-theme";
    let savedTheme;

    try {
        savedTheme = localStorage.getItem(key);
    } catch {
        // Storage may be disabled. The toggle still works for this page.
    }

    function applyTheme() {
        const theme = savedTheme === "light" || savedTheme === "dark"
            ? savedTheme
            : preference.matches ? "dark" : "light";
        const isDark = theme === "dark";
        root.dataset.theme = theme;

        const toggle = document.querySelector("[data-theme-toggle]");
        toggle?.setAttribute("aria-pressed", String(isDark));
        toggle?.setAttribute("title", isDark ? "Switch to light mode" : "Switch to dark mode");
    }

    applyTheme();
    preference.addEventListener("change", applyTheme);

    document.addEventListener("DOMContentLoaded", () => {
        const toggle = document.querySelector("[data-theme-toggle]");
        applyTheme();
        toggle?.addEventListener("click", () => {
            savedTheme = root.dataset.theme === "dark" ? "light" : "dark";
            try {
                localStorage.setItem(key, savedTheme);
            } catch {
                // Keep the current page preference in memory.
            }
            applyTheme();
        });
    });

    window.addEventListener("storage", event => {
        if (event.key === key || event.key === null) {
            savedTheme = event.newValue;
            applyTheme();
        }
    });
})();
