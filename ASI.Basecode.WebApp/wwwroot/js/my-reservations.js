(() => {
    "use strict";
    const form = document.querySelector("[data-reservation-filters]");
    if (!form) return;
    form.querySelectorAll("select").forEach(select => {
        select.addEventListener("change", () => form.requestSubmit());
    });
    // GET form omits page so changing either selection always returns to page 1.
    form.querySelector("[data-apply-filters]")?.setAttribute("hidden", "");
})();
