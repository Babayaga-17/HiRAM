(() => {
    const toggle = document.getElementById("menuToggle");
    const sidebar = document.getElementById("appSidebar");
    const shell = sidebar.closest(".app-shell");
    const workspace = shell.querySelector(".app-main");
    const closeButton = sidebar.querySelector(".sidebar-close");
    const compact = window.matchMedia("(max-width: 850px)");

    function syncToggle() {
        const expanded = compact.matches
            ? shell.classList.contains("sidebar-open")
            : !shell.classList.contains("sidebar-collapsed");
        const label = compact.matches
            ? (expanded ? "Close navigation" : "Open navigation")
            : (expanded ? "Collapse navigation" : "Expand navigation");
        toggle.setAttribute("aria-expanded", String(expanded));
        toggle.setAttribute("aria-label", label);
        toggle.title = label;
    }

    function closeSidebar() {
        if (!shell.classList.contains("sidebar-open")) return;
        shell.classList.remove("sidebar-open");
        workspace.inert = false;
        syncToggle();
        toggle.focus();
    }

    toggle.addEventListener("click", () => {
        if (compact.matches) {
            shell.classList.add("sidebar-open");
            workspace.inert = true;
            closeButton.focus({ preventScroll: true });
        } else {
            shell.classList.toggle("sidebar-collapsed");
        }
        syncToggle();
    });

    document.getElementById("sidebarScrim").addEventListener("click", closeSidebar);
    closeButton.addEventListener("click", closeSidebar);
    compact.addEventListener("change", () => {
        closeSidebar();
        syncToggle();
    });

    document.addEventListener("keydown", event => {
        if (!shell.classList.contains("sidebar-open")) return;
        if (event.key === "Escape") {
            closeSidebar();
        } else if (event.key === "Tab") {
            const controls = sidebar.querySelectorAll("a[href], button");
            const first = controls[0];
            const last = controls[controls.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    });

    syncToggle();
})();
