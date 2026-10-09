(function () {
    const menuToggle = document.getElementById("menuToggle");
    const scrim = document.getElementById("sidebarScrim");
    const sidebar = document.getElementById("appSidebar");

    function closeSidebar() {
        document.body.classList.remove("sidebar-open");
        menuToggle?.setAttribute("aria-expanded", "false");
        menuToggle?.setAttribute("aria-label", "Open navigation");
    }

    menuToggle?.addEventListener("click", function () {
        const isOpen = document.body.classList.toggle("sidebar-open");
        menuToggle.setAttribute("aria-expanded", String(isOpen));
        menuToggle.setAttribute("aria-label", isOpen ? "Close navigation" : "Open navigation");
    });
    scrim?.addEventListener("click", closeSidebar);
    sidebar?.querySelectorAll("a").forEach(link => link.addEventListener("click", closeSidebar));
    document.addEventListener("keydown", event => {
        if (event.key === "Escape") closeSidebar();
    });
})();
